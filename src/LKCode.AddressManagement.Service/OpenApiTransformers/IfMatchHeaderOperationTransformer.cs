using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the required <c>If-Match</c> header of every operation that responds with
/// <c>428 Precondition Required</c> in the OpenAPI document.
/// </summary>
/// <remarks>
/// An operation responds with <c>428 Precondition Required</c> exactly if it requires the <c>If-Match</c> header
/// (RFC 6585, section 3). The header is read by the action itself rather than bound as a parameter, so that its
/// checks keep the order of RFC 9110; therefore it is described here.
/// </remarks>
public sealed class IfMatchHeaderOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// The syntax of the header (RFC 9110, section 13.1.1): <c>*</c> or a comma-separated list of strong or weak
    /// entity tags, each of visible ASCII characters except the quote.
    /// </summary>
    private const string Pattern = """^(\*|(W/)?"[\x21\x23-\x7E]*"(\s*,\s*(W/)?"[\x21\x23-\x7E]*")*)$""";

    private static readonly string _preconditionRequiredStatusCode =
        StatusCodes.Status428PreconditionRequired.ToString(CultureInfo.InvariantCulture);

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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.Responses?.ContainsKey(_preconditionRequiredStatusCode) == true)
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = HeaderNames.IfMatch,
                In = ParameterLocation.Header,
                Required = true,
                Description = "The entity tag of the version to change, as returned in the `ETag` header, e.g. "
                    + "`\"3\"`; several entity tags separated by commas; or `*` for any version. Entity tags are "
                    + "compared strongly, so a weak entity tag such as `W/\"3\"` never matches. A missing header "
                    + "results in `428 Precondition Required`, an invalid one in `400 Bad Request`, and one without "
                    + "the current version in `412 Precondition Failed`.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = Pattern },
            });
        }

        return Task.CompletedTask;
    }
}
