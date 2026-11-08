using FluentValidation.Results;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace LKCode.AddressManagement.Service.Extensions;

/// <summary>
/// Checks and applies JSON Patch documents (RFC 6902) that change the values of an address.
/// </summary>
public static class AddressPatchDocumentExtensions
{
    /// <summary>
    /// Checks that every operation of the <paramref name="patchDocument"/> can be applied to the values of an
    /// address, before the address is loaded.
    /// </summary>
    /// <param name="patchDocument">The patch document to check.</param>
    /// <returns>The result with an error for every invalid member of every operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="patchDocument"/> is <see langword="null"/>.</exception>
    public static ValidationResult ValidateOperations(this JsonPatchDocument<UpdateAddressRequest> patchDocument) =>
        throw new NotImplementedException();

    /// <summary>
    /// Applies the operations of the <paramref name="patchDocument"/> to the <paramref name="request"/>.
    /// </summary>
    /// <param name="patchDocument">The patch document, checked by <see cref="ValidateOperations"/>.</param>
    /// <param name="request">The values of the address, which are changed in place.</param>
    /// <param name="failedTestIndex">The index of the first <c>test</c> operation that failed; otherwise -1.</param>
    /// <returns><see langword="true"/> if all operations were applied; <see langword="false"/> if a test failed.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="patchDocument"/> or <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    public static bool TryApplyTo(
        this JsonPatchDocument<UpdateAddressRequest> patchDocument,
        UpdateAddressRequest request,
        out int failedTestIndex) =>
        throw new NotImplementedException();
}
