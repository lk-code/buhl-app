using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;

namespace LKCode.AddressManagement.IntegrationTests;

/// <summary>
/// Asserts that responses are Problem Details as ASP.NET Core writes them for an error without a body.
/// </summary>
public static class HttpResponseMessageExtensions
{
    public const string ProblemDetailsMediaType = "application/problem+json";

    /// <summary>
    /// Asserts that the response is <c>401 Unauthorized</c> with Problem Details.
    /// </summary>
    public static Task ShouldBeUnauthorizedProblemAsync(this HttpResponseMessage response) =>
        response.ShouldBeProblemDetailsAsync(
            HttpStatusCode.Unauthorized,
            "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            "Unauthorized");

    /// <summary>
    /// Asserts that the response is <c>403 Forbidden</c> with Problem Details.
    /// </summary>
    public static Task ShouldBeForbiddenProblemAsync(this HttpResponseMessage response) =>
        response.ShouldBeProblemDetailsAsync(
            HttpStatusCode.Forbidden,
            "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            "Forbidden");

    /// <summary>
    /// Asserts that the response has the <paramref name="statusCode"/> and Problem Details with it, the
    /// <paramref name="type"/>, the <paramref name="title"/> and a trace id.
    /// </summary>
    public static async Task ShouldBeProblemDetailsAsync(
        this HttpResponseMessage response,
        HttpStatusCode statusCode,
        string type,
        string title)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.StatusCode.Should().Be(statusCode);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be((int)statusCode);
        body.GetProperty("type").GetString().Should().Be(type);
        body.GetProperty("title").GetString().Should().Be(title);
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
