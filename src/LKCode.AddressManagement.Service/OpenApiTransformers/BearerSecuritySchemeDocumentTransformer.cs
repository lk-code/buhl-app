using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the authentication with JWT bearer tokens as security scheme
/// <see cref="JwtBearerDefaults.AuthenticationScheme"/> in the components of the OpenAPI document.
/// </summary>
/// <remarks>
/// The <see cref="SecurityRequirementOperationTransformer"/> refers to the scheme from every protected operation.
/// The scheme is registered in the document, so that these references can be resolved.
/// </remarks>
public sealed class BearerSecuritySchemeDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// Adds the security scheme <see cref="JwtBearerDefaults.AuthenticationScheme"/> to the
    /// <paramref name="document"/>.
    /// </summary>
    /// <param name="document">The document to change.</param>
    /// <param name="context">The context of the document.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="document"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
