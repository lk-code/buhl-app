using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Queries;
using LKCode.AddressManagement.Core.Addresses.Results;

namespace LKCode.AddressManagement.Core.Addresses.Contracts;

/// <summary>
/// Reads, creates, changes and deletes addresses.
/// </summary>
/// <remarks>
/// The requests are expected to be sanitized and validated before; invalid values are a programming error. Expected
/// cases such as an unknown id are reported through the return value, not through exceptions.
/// </remarks>
public interface IAddressService
{
    /// <summary>
    /// Gets the address with the given id.
    /// </summary>
    /// <param name="id">The id of the address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>The address, or <see langword="null"/> if there is no address with the id.</returns>
    Task<AddressResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one page of the sorted addresses whose city and postal code contain the filters of the
    /// <paramref name="query"/>.
    /// </summary>
    /// <param name="query">
    /// The sanitized and validated filters, page and sort order. The filters are normalized before searching; a
    /// <see langword="null"/> or blank filter is ignored. Missing values of the page and the sort order stand for the
    /// defaults of <see cref="AddressListConstraints"/>.
    /// </param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>
    /// The addresses on the page and the number of all matching addresses and pages. The page is empty if none matches
    /// or it is behind the end. Addresses with the same value of the sort field are sorted by their id in the same
    /// direction.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page or the page size is out of its range.</exception>
    /// <exception cref="ArgumentException">The sort field or the sort direction is unknown.</exception>
    Task<AddressListResponse> GetListAsync(AddressListQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new address and saves it.
    /// </summary>
    /// <param name="request">The sanitized and validated values of the new address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>The new address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A value of the request is invalid.</exception>
    Task<AddressResponse> CreateAsync(CreateAddressRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces all values of the address with the given id and saves it if it still has the expected version.
    /// </summary>
    /// <param name="id">The id of the address.</param>
    /// <param name="expectedVersion">
    /// The version the client expects the address to have; <see langword="null"/> to change the address in any
    /// version. Even then, a change by someone else between loading and saving is detected.
    /// </param>
    /// <param name="request">The sanitized and validated new values of the address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>
    /// The result with the changed address; or with the status <see cref="ChangeStatus.NotFound"/> if there is no
    /// address with the id, or <see cref="ChangeStatus.VersionMismatch"/> if its version differs.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A value of the request is invalid. The address stays unchanged.
    /// </exception>
    Task<AddressChangeResult> UpdateAsync(
        Guid id,
        long? expectedVersion,
        UpdateAddressRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the address with the given id if it still has the expected version.
    /// </summary>
    /// <remarks>
    /// The address is only marked as deleted and kept. Afterwards, it behaves as if it had never existed: it is
    /// neither found by <see cref="GetByIdAsync"/> nor listed by <see cref="GetListAsync"/>, and it can neither be
    /// changed nor deleted again.
    /// </remarks>
    /// <param name="id">The id of the address.</param>
    /// <param name="expectedVersion">
    /// The version the client expects the address to have; <see langword="null"/> to delete the address in any
    /// version. Even then, a change by someone else between loading and deleting is detected.
    /// </param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>
    /// <see cref="ChangeStatus.Success"/> if the address was deleted; <see cref="ChangeStatus.NotFound"/> if there is
    /// no address with the id or it is already deleted; <see cref="ChangeStatus.VersionMismatch"/> if its version
    /// differs.
    /// </returns>
    Task<ChangeStatus> DeleteAsync(Guid id, long? expectedVersion, CancellationToken cancellationToken);
}
