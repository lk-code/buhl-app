using LKCode.AddressManagement.Core.Addresses.Dtos;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the <c>ETag</c> header of every response with a single <see cref="AddressResponse"/> in the OpenAPI
/// document.
/// </summary>
public sealed class ETagHeaderOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// Adds the required <c>ETag</c> header to every response of the <paramref name="operation"/> whose body is an
    /// <see cref="AddressResponse"/>.
    /// </summary>
    /// <param name="operation">The operation to change.</param>
    /// <param name="context">The context of the operation with the types of its responses.</param>
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
