using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the Problem Details (RFC 9457) in the OpenAPI schemas of <see cref="ProblemDetails"/> and the types
/// derived from it, such as <see cref="ValidationProblemDetails"/>, as this API writes them.
/// </summary>
/// <remarks>
/// Every Problem Details response of the API contains the members <c>type</c>, <c>title</c>, <c>status</c> and
/// <c>traceId</c>, a response with validation errors additionally <c>errors</c>. The generated schema does not know
/// the extension member <c>traceId</c> and describes <c>status</c> as integer or string, because the JSON options
/// allow reading numbers from strings; the API always writes a number.
/// </remarks>
public sealed class ProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// Describes the members of the Problem Details in the <paramref name="schema"/> if it describes a type that is
    /// assignable to <see cref="ProblemDetails"/>; other schemas stay unchanged.
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
