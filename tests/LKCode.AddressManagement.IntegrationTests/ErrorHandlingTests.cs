using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;

namespace LKCode.AddressManagement.IntegrationTests;

public class ErrorHandlingTests
{
    private const string ProblemDetailsMediaType = "application/problem+json";

    private AddressManagementWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new AddressManagementWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [TestCase("GET", "/does-not-exist")]
    [TestCase("POST", "/does-not-exist")]
    [TestCase("DELETE", "/api/unknown/0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00")]
    public async Task SendAsync_UnknownRoute_Returns404(string method, string path)
    {
        // Arrange
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        // Act
        using HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestCase("GET", "/does-not-exist")]
    [TestCase("POST", "/does-not-exist")]
    [TestCase("DELETE", "/api/unknown/0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00")]
    public async Task SendAsync_UnknownRoute_ReturnsProblemDetails(string method, string path)
    {
        // Arrange
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        // Act
        using HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.NotFound);
        body.GetProperty("title").GetString().Should().Be("Not Found");
        body.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.5");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
