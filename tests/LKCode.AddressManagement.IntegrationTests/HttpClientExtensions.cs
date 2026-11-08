using Microsoft.Net.Http.Headers;

namespace LKCode.AddressManagement.IntegrationTests;

/// <summary>
/// Sends requests with an <c>If-Match</c> header.
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// Sends a request with the <paramref name="ifMatch"/> header; <see langword="null"/> to send no header.
    /// </summary>
    /// <remarks>The header is added without validation, so that invalid values can be sent, too.</remarks>
    public static Task<HttpResponseMessage> SendWithIfMatchAsync(
        this HttpClient client,
        HttpMethod method,
        string url,
        HttpContent? content,
        string? ifMatch) =>
        client.SendWithIfMatchAsync(method, url, content, ifMatch is null ? [] : [ifMatch]);

    /// <summary>
    /// Sends a request with one <c>If-Match</c> header line for each of the <paramref name="ifMatchValues"/>; no
    /// header if there are none.
    /// </summary>
    public static async Task<HttpResponseMessage> SendWithIfMatchAsync(
        this HttpClient client,
        HttpMethod method,
        string url,
        HttpContent? content,
        IReadOnlyCollection<string> ifMatchValues)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(ifMatchValues);

        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (ifMatchValues.Count > 0)
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.IfMatch, ifMatchValues);
        }

        return await client.SendAsync(request);
    }
}
