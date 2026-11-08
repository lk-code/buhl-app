using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Domain.Addresses;

namespace LKCode.AddressManagement.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAddressRepository"/>.
/// </summary>
/// <param name="dbContext">The database context.</param>
/// <exception cref="ArgumentNullException"><paramref name="dbContext"/> is <see langword="null"/>.</exception>
public sealed class AddressRepository(AddressDbContext dbContext) : IAddressRepository
{
    private readonly AddressDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<Address?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Address>> ListAsync(
        string? normalizedCity,
        string? normalizedPostalCode,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task AddAsync(Address address, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
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
