using LKCode.AddressManagement.Core.Addresses.Dtos;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the numbers of the OpenAPI schema of <see cref="AddressListResponse"/> as the API writes them.
/// </summary>
public sealed class AddressListResponseSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// Describes the page, the page size and the counts as 32-bit integers with their ranges and requires all
    /// members, if the <paramref name="schema"/> describes <see cref="AddressListResponse"/>; other schemas stay
    /// unchanged.
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
