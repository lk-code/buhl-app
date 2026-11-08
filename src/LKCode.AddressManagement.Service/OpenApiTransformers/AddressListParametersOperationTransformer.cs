using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the paging and sorting query parameters of the address list in the OpenAPI document: their types,
/// ranges, allowed values and defaults.
/// </summary>
public sealed class AddressListParametersOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// Replaces the schemas of the query parameters <c>page</c>, <c>pageSize</c>, <c>sortBy</c> and
    /// <c>sortDirection</c> of the <paramref name="operation"/>; other parameters stay unchanged.
    /// </summary>
    /// <param name="operation">The operation to change.</param>
    /// <param name="context">The context of the operation.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is <see langword="null"/>.</exception>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
