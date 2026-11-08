using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the required <c>If-Match</c> header of every operation that responds with
/// <c>428 Precondition Required</c> in the OpenAPI document.
/// </summary>
public sealed class IfMatchHeaderOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// Adds the required <c>If-Match</c> header parameter to the <paramref name="operation"/> if it has a
    /// <c>428 Precondition Required</c> response.
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
