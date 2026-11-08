using LKCode.AddressManagement.Core.Addresses.Contracts;
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
    public Task<IReadOnlyList<Address>> ListAsync(
        string? normalizedCity,
        string? normalizedPostalCode,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task AddAsync(Address address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        _dbContext.Addresses.Add(address);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateAsync(Address address, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task DeleteAsync(Address address, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
