using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Results;
using LKCode.AddressManagement.Domain.Addresses;
using Microsoft.EntityFrameworkCore;

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
    public Task<ChangeStatus> UpdateAsync(Address address, long? expectedVersion, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<ChangeStatus> DeleteAsync(Address address, long? expectedVersion, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
