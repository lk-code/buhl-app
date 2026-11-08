using LKCode.AddressManagement.Core.Addresses.Contracts;
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
        CancellationToken cancellationToken)
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

        return await query.ToListAsync(cancellationToken);
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
