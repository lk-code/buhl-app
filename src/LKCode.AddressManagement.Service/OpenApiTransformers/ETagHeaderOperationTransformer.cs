using System.Globalization;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.Service.OpenApiTransformers;

/// <summary>
/// Describes the <c>ETag</c> header of every response with a single <see cref="AddressResponse"/> in the OpenAPI
/// document.
/// </summary>
/// <remarks>
/// ASP.NET Core cannot describe response headers through attributes. Every response with a single address carries
/// the strong entity tag of its version, e.g. <c>"3"</c> (RFC 9110, section 8.8.3); a list of addresses does not.
/// </remarks>
public sealed class ETagHeaderOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// The pattern of the strong entity tag of a version: a positive integer without leading zeros in quotes.
    /// </summary>
    private const string Pattern = """^"[1-9][0-9]*"$""";

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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        IEnumerable<ApiResponseType> addressResponseTypes = context.Description.SupportedResponseTypes
            .Where(responseType => responseType.Type == typeof(AddressResponse));
        foreach (ApiResponseType responseType in addressResponseTypes)
        {
            string statusCode = responseType.StatusCode.ToString(CultureInfo.InvariantCulture);
            if (operation.Responses?.GetValueOrDefault(statusCode) is OpenApiResponse response)
            {
                response.Headers ??= new Dictionary<string, IOpenApiHeader>();
                response.Headers[HeaderNames.ETag] = new OpenApiHeader
                {
                    Description = "The strong entity tag of the version of the address, e.g. `\"3\"`. A change of the "
                        + "address requires it in the `If-Match` header.",
                    Required = true,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = Pattern },
                };
            }
        }

        return Task.CompletedTask;
    }
}
