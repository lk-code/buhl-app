using LKCode.AddressManagement.Core.Addresses.Queries;
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
    /// Loads one page of the sorted addresses whose normalized city and normalized postal code contain the filters
    /// of the <paramref name="criteria"/>, and counts all of them.
    /// </summary>
    /// <param name="criteria">The filters, the sort order and the page.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>
    /// The addresses on the page and the number of all matching addresses. The page is empty if it is behind the
    /// end.
    /// </returns>
    /// <remarks>
    /// The filter values must already be normalized like <see cref="Address.NormalizedCity"/> and
    /// <see cref="Address.NormalizedPostalCode"/>; both filters must match. Addresses with the same value of the sort
    /// field are sorted by their id in the same direction, so that the order is stable. Deleted addresses are neither
    /// returned nor counted. Only the addresses on the page are loaded.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="criteria"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The page or the page size of the <paramref name="criteria"/> is less than 1, or its sort field or direction is
    /// not defined.
    /// </exception>
    Task<AddressPage> ListAsync(AddressListCriteria criteria, CancellationToken cancellationToken);

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
