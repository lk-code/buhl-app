using System.Linq.Expressions;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Queries;
using LKCode.AddressManagement.Core.Addresses.Results;
using LKCode.AddressManagement.Domain.Addresses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LKCode.AddressManagement.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAddressRepository"/>.
/// </summary>
/// <param name="dbContext">The database context.</param>
/// <exception cref="ArgumentNullException"><paramref name="dbContext"/> is <see langword="null"/>.</exception>
public sealed class AddressRepository(AddressDbContext dbContext) : IAddressRepository
{
    private readonly AddressDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc />
    public Task<Address?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Addresses.FirstOrDefaultAsync(address => address.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Address>> ListAsync(
        string? normalizedCity,
        string? normalizedPostalCode,
        CancellationToken cancellationToken) =>
        await Filter(normalizedCity, normalizedPostalCode).ToListAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Two queries run in the database: one counts the matching addresses, the other loads only the addresses of the
    /// page by sorting, skipping and taking on the <see cref="IQueryable{T}"/>. The second query is skipped if the
    /// page is behind the end; this also prevents an overflow of the number of skipped addresses.
    /// </remarks>
    public async Task<AddressPage> ListAsync(AddressListCriteria criteria, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentOutOfRangeException.ThrowIfLessThan(criteria.Page, AddressListConstraints.MinPage);
        ArgumentOutOfRangeException.ThrowIfLessThan(criteria.PageSize, AddressListConstraints.MinPageSize);

        IQueryable<Address> filtered = Filter(criteria.NormalizedCity, criteria.NormalizedPostalCode);
        IQueryable<Address> sorted = Sort(filtered, criteria.SortBy, criteria.SortDirection);

        int totalCount = await filtered.CountAsync(cancellationToken);
        long skip = (long)(criteria.Page - 1) * criteria.PageSize;
        if (skip >= totalCount)
        {
            return new AddressPage([], totalCount);
        }

        List<Address> addresses = await sorted
            .Skip((int)skip)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return new AddressPage(addresses, totalCount);
    }

    /// <inheritdoc />
    public async Task AddAsync(Address address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        _dbContext.Addresses.Add(address);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<ChangeStatus> UpdateAsync(Address address, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        EntityEntry<Address> entry = _dbContext.Addresses.Update(address);

        return SaveWithExpectedVersionAsync(entry, expectedVersion, cancellationToken);
    }

    /// <summary>
    /// Sorts the <paramref name="query"/> by the <paramref name="sortBy"/> field and then by the id, both in the
    /// <paramref name="direction"/>, so that addresses with the same value always have the same order.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sortBy"/> or <paramref name="direction"/> is not defined.
    /// </exception>
    private static IOrderedQueryable<Address> Sort(
        IQueryable<Address> query,
        AddressSortField sortBy,
        SortDirection direction) =>
        sortBy switch
        {
            AddressSortField.LastName => Sort(query, address => address.LastName, direction),
            AddressSortField.FirstName => Sort(query, address => address.FirstName, direction),
            AddressSortField.City => Sort(query, address => address.City, direction),
            AddressSortField.PostalCode => Sort(query, address => address.PostalCode, direction),
            AddressSortField.Country => Sort(query, address => address.Country, direction),
            AddressSortField.CreatedAt => Sort(query, address => address.CreatedAt, direction),
            AddressSortField.UpdatedAt => Sort(query, address => address.UpdatedAt, direction),
            _ => throw new ArgumentOutOfRangeException(
                nameof(sortBy),
                sortBy,
                $"The value is no defined {nameof(AddressSortField)}."),
        };

    private static IOrderedQueryable<Address> Sort<TKey>(
        IQueryable<Address> query,
        Expression<Func<Address, TKey>> key,
        SortDirection direction) =>
        direction switch
        {
            SortDirection.Ascending => query.OrderBy(key).ThenBy(address => address.Id),
            SortDirection.Descending => query.OrderByDescending(key).ThenByDescending(address => address.Id),
            _ => throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                $"The value is no defined {nameof(SortDirection)}."),
        };

    /// <summary>
    /// Builds the untracked query of the active addresses whose normalized city and normalized postal code contain the
    /// given values; a blank value is ignored.
    /// </summary>
    private IQueryable<Address> Filter(string? normalizedCity, string? normalizedPostalCode)
    {
        IQueryable<Address> query = _dbContext.Addresses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalizedCity))
        {
            query = query.Where(address => address.NormalizedCity.Contains(normalizedCity));
        }

        if (!string.IsNullOrWhiteSpace(normalizedPostalCode))
        {
            query = query.Where(address => address.NormalizedPostalCode.Contains(normalizedPostalCode));
        }

        return query;
    }

    /// <summary>
    /// Saves the change of the <paramref name="entry"/> if the stored version equals the expected version.
    /// </summary>
    /// <remarks>
    /// <see cref="Address.Version"/> is a concurrency token: EF Core saves the change only if the stored version equals
    /// the original value of the entry. Setting the original value to the expected version makes EF Core check the
    /// version of the client instead of the loaded one. A failed check is only reported through a
    /// <see cref="DbUpdateConcurrencyException"/>; it is an expected outcome and is returned as status. An address
    /// that was deleted in the meantime is reported as not found, like one that was never stored.
    /// </remarks>
    private async Task<ChangeStatus> SaveWithExpectedVersionAsync(
        EntityEntry<Address> entry,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (expectedVersion is long version)
        {
            entry.Property(address => address.Version).OriginalValue = version;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ChangeStatus.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            PropertyValues? storedValues = await entry.GetDatabaseValuesAsync(cancellationToken);

            // The change was not saved and must not be saved by a later call of SaveChanges.
            entry.State = EntityState.Detached;

            // The stored values are read without the query filter, so a deleted address is found, too.
            return storedValues is null || storedValues.GetValue<bool>(nameof(Address.IsDeleted))
                ? ChangeStatus.NotFound
                : ChangeStatus.VersionMismatch;
        }
    }
}
