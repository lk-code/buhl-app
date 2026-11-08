using LKCode.AddressManagement.Core.Addresses.Results;
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
    /// <returns>
    /// The address, or <see langword="null"/> if there is no address with the id or it is deleted.
    /// </returns>
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
    /// match. Deleted addresses are never returned.
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
    /// Saves the changes of an existing address if its stored version is the expected one.
    /// </summary>
    /// <param name="address">The changed address.</param>
    /// <param name="expectedVersion">
    /// The version the stored address must have, i.e. the version before the change; <see langword="null"/> to expect
    /// the version the address had when it was loaded with <see cref="GetByIdAsync"/>.
    /// </param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>
    /// <see cref="ChangeStatus.Success"/> if the changes were saved; <see cref="ChangeStatus.NotFound"/> if the
    /// address is not stored or was deleted in the meantime; <see cref="ChangeStatus.VersionMismatch"/> if its stored
    /// version differs.
    /// </returns>
    /// <remarks>
    /// A deletion is a change, too: an address marked with <see cref="Address.Delete"/> is saved with this method and
    /// is kept, but no longer returned by <see cref="GetByIdAsync"/> and <see cref="ListAsync"/>. The version is
    /// checked when saving, so that a change in the meantime is detected, too. If nothing was saved, the address is no
    /// longer tracked.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is <see langword="null"/>.</exception>
    Task<ChangeStatus> UpdateAsync(Address address, long? expectedVersion, CancellationToken cancellationToken);
}
