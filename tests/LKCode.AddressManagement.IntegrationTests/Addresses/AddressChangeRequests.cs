using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// The requests that change an existing address and therefore require an <c>If-Match</c> header: <c>PUT</c>,
/// <c>PATCH</c> and <c>DELETE</c>.
/// </summary>
public static class AddressChangeRequests
{
    public const string Path = "/api/addresses";

    public const string JsonPatchMediaType = "application/json-patch+json";

    /// <summary>
    /// Gets a valid JSON Patch document that sets the city to <c>Berlin</c>.
    /// </summary>
    public const string ValidPatch = """[{"op": "replace", "path": "/city", "value": "Berlin"}]""";

    /// <summary>
    /// Gets <c>PUT</c>, <c>PATCH</c> and <c>DELETE</c>.
    /// </summary>
    public static IEnumerable<HttpMethod> Methods { get; } = [HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete];

    /// <summary>
    /// Gets the methods with a body: <c>PUT</c> and <c>PATCH</c>.
    /// </summary>
    public static IEnumerable<HttpMethod> MethodsWithBody { get; } = [HttpMethod.Put, HttpMethod.Patch];

    /// <summary>
    /// Creates a valid body of the <paramref name="method"/>; <see langword="null"/> for <c>DELETE</c>.
    /// </summary>
    public static HttpContent? CreateValidContent(HttpMethod method) =>
        method == HttpMethod.Put ? JsonContent.Create(AddressPayloads.Updated())
        : method == HttpMethod.Patch ? CreateContent(method, ValidPatch)
        : null;

    /// <summary>
    /// Creates a body with the <paramref name="text"/> and the media type of the <paramref name="method"/>:
    /// <c>application/json</c> for <c>PUT</c>, <c>application/json-patch+json</c> for <c>PATCH</c>.
    /// </summary>
    public static HttpContent CreateContent(HttpMethod method, string text)
    {
        var content = new StringContent(text, Encoding.UTF8);
        content.Headers.ContentType =
            MediaTypeHeaderValue.Parse(method == HttpMethod.Patch ? JsonPatchMediaType : "application/json");

        return content;
    }

    /// <summary>
    /// Gets the status code of a successful request with the <paramref name="method"/>.
    /// </summary>
    public static HttpStatusCode GetSuccessStatusCode(HttpMethod method) =>
        method == HttpMethod.Delete ? HttpStatusCode.NoContent : HttpStatusCode.OK;

    /// <summary>
    /// Sends a valid request with the <paramref name="method"/> for the address with the <paramref name="id"/>.
    /// </summary>
    public static Task<HttpResponseMessage> SendValidAsync(
        this HttpClient client,
        HttpMethod method,
        Guid id,
        params IReadOnlyCollection<string> ifMatchValues) =>
        client.SendWithIfMatchAsync(method, $"{Path}/{id}", CreateValidContent(method), ifMatchValues);
}
