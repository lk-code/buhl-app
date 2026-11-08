using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the <c>Location</c> header of every <c>201 Created</c> response in the OpenAPI document.
/// </summary>
/// <remarks>
/// ASP.NET Core cannot describe response headers through attributes. A <c>201 Created</c> response always carries the
/// URL of the created resource in the <c>Location</c> header (RFC 9110, section 15.3.2), so the header is added to
/// every such response.
/// </remarks>
public sealed class LocationHeaderOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly string _createdStatusCode =
        StatusCodes.Status201Created.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Adds the required <c>Location</c> header to the <c>201 Created</c> response of the
    /// <paramref name="operation"/>, if there is one.
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

        if (operation.Responses?.GetValueOrDefault(_createdStatusCode) is OpenApiResponse createdResponse)
        {
            createdResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
            createdResponse.Headers[HeaderNames.Location] = new OpenApiHeader
            {
                Description = "The absolute URL of the created resource.",
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" },
            };
        }

        return Task.CompletedTask;
    }
}
