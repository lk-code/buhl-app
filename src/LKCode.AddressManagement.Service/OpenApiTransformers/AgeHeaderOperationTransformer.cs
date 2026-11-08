using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the <c>Age</c> header of the <c>200 OK</c> response of every endpoint with an output cache policy in the
/// OpenAPI document.
/// </summary>
/// <remarks>
/// ASP.NET Core cannot describe response headers through attributes. The output cache stores only responses with
/// <c>200 OK</c> and adds the header <c>Age</c> whenever it answers from the cache (RFC 9111, section 5.1); a
/// response that does not come from the cache has no <c>Age</c>. An endpoint has an output cache policy if its
/// metadata contains an <see cref="OutputCacheAttribute"/>.
/// </remarks>
public sealed class AgeHeaderOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// Adds the optional <c>Age</c> header to the <c>200 OK</c> response of the <paramref name="operation"/> if its
    /// endpoint is cached.
    /// </summary>
    /// <param name="operation">The operation to change.</param>
    /// <param name="context">The context of the operation with the metadata of its endpoint.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="operation"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
