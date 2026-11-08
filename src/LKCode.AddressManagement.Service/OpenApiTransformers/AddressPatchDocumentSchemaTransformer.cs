using LKCode.AddressManagement.Core.Addresses.Dtos;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the JSON Patch documents (RFC 6902) that change an address in the OpenAPI schema of
/// <see cref="JsonPatchDocument{TModel}"/> of <see cref="UpdateAddressRequest"/>.
/// </summary>
public sealed class AddressPatchDocumentSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// Restricts <c>path</c> and <c>from</c> of all operations to the fields of an address and <c>value</c> to a
    /// string or <see langword="null"/>, if the <paramref name="schema"/> describes a patch document of an address;
    /// other schemas stay unchanged.
    /// </summary>
    /// <param name="schema">The schema to change.</param>
    /// <param name="context">The context of the schema with the type it describes.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="schema"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
