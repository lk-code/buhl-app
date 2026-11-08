using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using FluentValidation.Results;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson.Operations;

namespace LKCode.AddressManagement.Service.Extensions;

/// <summary>
/// Checks and applies JSON Patch documents (RFC 6902) that change the values of an address.
/// </summary>
/// <remarks>
/// The library applies an operation even if its members are invalid: it treats a missing <c>op</c> as <c>add</c>,
/// ignores the case of <c>op</c> and of the names in a path, and throws an <see cref="ArgumentNullException"/> for a
/// missing <c>path</c> or <c>from</c>. It also reports a <c>test</c> of an unknown path like a <c>test</c> whose value
/// differs. Therefore the operations are checked strictly before they are applied.
/// </remarks>
public static class AddressPatchDocumentExtensions
{
    private const string AddOperation = "add";
    private const string RemoveOperation = "remove";
    private const string ReplaceOperation = "replace";
    private const string MoveOperation = "move";
    private const string CopyOperation = "copy";
    private const string TestOperation = "test";

    private const string OperationError =
        "The operation must be 'add', 'remove', 'replace', 'move', 'copy' or 'test'.";

    private const string PathError = "The path must point to a field of the address, e.g. '/firstName'.";

    private const string FromError =
        "The location to move or copy from must point to a field of the address, e.g. '/firstName'.";

    private const string ValueError = "The value must be a string or null.";

    private static readonly FrozenSet<string> _operations = FrozenSet.Create(
        StringComparer.Ordinal,
        AddOperation,
        RemoveOperation,
        ReplaceOperation,
        MoveOperation,
        CopyOperation,
        TestOperation);

    /// <summary>
    /// Checks that every operation of the <paramref name="patchDocument"/> can be applied to the values of an
    /// address, before the address is loaded.
    /// </summary>
    /// <param name="patchDocument">The patch document to check.</param>
    /// <returns>
    /// The result with an error for every invalid member of every operation, keyed by its JSON path, e.g.
    /// <c>$[1].path</c>.
    /// </returns>
    /// <remarks>
    /// An operation is valid if
    /// <list type="bullet">
    /// <item><c>op</c> is one of the six operations of RFC 6902 in lowercase,</item>
    /// <item><c>path</c> and, for <c>move</c> and <c>copy</c>, <c>from</c> point to a field of
    /// <see cref="UpdateAddressRequest"/>, with its exact name in JSON (RFC 6901 is case-sensitive), and</item>
    /// <item><c>value</c> of <c>add</c>, <c>replace</c> and <c>test</c> is a string or <see langword="null"/>; the
    /// library cannot tell a missing value from <see langword="null"/>.</item>
    /// </list>
    /// Members that an operation does not use are ignored. If <c>op</c> is invalid, only <c>path</c> is checked
    /// additionally, because the other members depend on the operation.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="patchDocument"/> is <see langword="null"/>.</exception>
    public static ValidationResult ValidateOperations(this JsonPatchDocument<UpdateAddressRequest> patchDocument)
    {
        ArgumentNullException.ThrowIfNull(patchDocument);

        IReadOnlyList<string> fieldPaths =
            patchDocument.SerializerOptions.GetPropertyJsonPointers(typeof(UpdateAddressRequest));
        var errors = new List<ValidationFailure>();
        for (int index = 0; index < patchDocument.Operations.Count; index++)
        {
            Operation<UpdateAddressRequest> operation = patchDocument.Operations[index];
            bool isKnownOperation = operation.op is not null && _operations.Contains(operation.op);
            if (!isKnownOperation)
            {
                errors.Add(CreateFailure(index, "op", OperationError));
            }

            if (!IsFieldPath(operation.path, fieldPaths))
            {
                errors.Add(CreateFailure(index, "path", PathError));
            }

            if (!isKnownOperation)
            {
                continue;
            }

            if (operation.op is MoveOperation or CopyOperation && !IsFieldPath(operation.from, fieldPaths))
            {
                errors.Add(CreateFailure(index, "from", FromError));
            }

            if (operation.op is AddOperation or ReplaceOperation or TestOperation && !IsStringOrNull(operation.value))
            {
                errors.Add(CreateFailure(index, "value", ValueError));
            }
        }

        return new ValidationResult(errors);
    }

    /// <summary>
    /// Applies the operations of the <paramref name="patchDocument"/> to the <paramref name="request"/>.
    /// </summary>
    /// <param name="patchDocument">The patch document, checked by <see cref="ValidateOperations"/>.</param>
    /// <param name="request">The values of the address, which are changed in place.</param>
    /// <param name="failedTestIndex">The index of the first <c>test</c> operation that failed; otherwise -1.</param>
    /// <returns>
    /// <see langword="true"/> if all operations were applied; <see langword="false"/> if a test failed.
    /// </returns>
    /// <remarks>
    /// <c>remove</c> sets a value to <see langword="null"/>. If a test fails, the <paramref name="request"/> may be
    /// changed partially and must be discarded: the library applies the following operations nevertheless.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="patchDocument"/> or <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// An operation other than <c>test</c> failed, which is not possible for a checked patch document.
    /// </exception>
    public static bool TryApplyTo(
        this JsonPatchDocument<UpdateAddressRequest> patchDocument,
        UpdateAddressRequest request,
        out int failedTestIndex)
    {
        ArgumentNullException.ThrowIfNull(patchDocument);
        ArgumentNullException.ThrowIfNull(request);

        // The library reports every failed operation and continues with the next one; only the first one counts.
        JsonPatchError? firstError = null;
        patchDocument.ApplyTo(request, error => firstError ??= error);
        if (firstError is null)
        {
            failedTestIndex = -1;

            return true;
        }

        int index = patchDocument.Operations.FindIndex(operation => ReferenceEquals(operation, firstError.Operation));
        if (firstError.Operation.OperationType != OperationType.Test)
        {
            // The message of the library is not included: it may contain values of the address.
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"The JSON Patch operation {index} ('{firstError.Operation.op}') failed despite the check."));
        }

        failedTestIndex = index;

        return false;
    }

    private static bool IsFieldPath(string? path, IReadOnlyList<string> fieldPaths) =>
        path is not null && fieldPaths.Contains(path, StringComparer.Ordinal);

    private static bool IsStringOrNull(object? value) =>
        value is null or string or JsonElement { ValueKind: JsonValueKind.String or JsonValueKind.Null };

    private static ValidationFailure CreateFailure(int index, string member, string errorMessage) =>
        new(string.Create(CultureInfo.InvariantCulture, $"$[{index}].{member}"), errorMessage);
}
