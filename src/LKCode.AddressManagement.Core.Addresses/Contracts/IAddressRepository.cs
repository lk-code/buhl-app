using LKCode.AddressManagement.Domain.Addresses;

namespace LKCode.AddressManagement.Core.Addresses.Contracts;

/// <summary>
/// Loads and stores addresses.
/// </summary>
public interface IAddressRepository
{
    /// <summary>
    /// Loads the address with the given id.
    /// </summary>
    /// <param name="id">The id of the address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>The address, or <see langword="null"/> if there is no address with the id.</returns>
    /// <remarks>The address is tracked, so changes to it can be saved with <see cref="UpdateAsync"/>.</remarks>
    Task<Address?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Loads all addresses whose normalized city and normalized postal code contain the given values.
    /// </summary>
    /// <param name="normalizedCity">
    /// The normalized part of the city to search for, or <see langword="null"/>, empty or whitespace to ignore the
    /// city.
    /// </param>
    /// <param name="normalizedPostalCode">
    /// The normalized part of the postal code to search for, or <see langword="null"/>, empty or whitespace to ignore
    /// the postal code.
    /// </param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>The matching addresses in no particular order; an empty list if none matches.</returns>
    /// <remarks>
    /// The filter values are compared with <see cref="Address.NormalizedCity"/> and
    /// <see cref="Address.NormalizedPostalCode"/> and must already be normalized in the same way. Both filters must
    /// match.
    /// </remarks>
    Task<IReadOnlyList<Address>> ListAsync(
        string? normalizedCity,
        string? normalizedPostalCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new address and saves it.
    /// </summary>
    /// <param name="address">The new address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A task that completes when the address is saved.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is <see langword="null"/>.</exception>
    Task AddAsync(Address address, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the changes of an existing address.
    /// </summary>
    /// <param name="address">The changed address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A task that completes when the changes are saved.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is <see langword="null"/>.</exception>
    Task UpdateAsync(Address address, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an existing address and saves the deletion.
    /// </summary>
    /// <param name="address">The address to delete.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A task that completes when the deletion is saved.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is <see langword="null"/>.</exception>
    Task DeleteAsync(Address address, CancellationToken cancellationToken);
}
