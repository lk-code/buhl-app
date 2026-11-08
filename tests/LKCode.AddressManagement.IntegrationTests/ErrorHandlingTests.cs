using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.IntegrationTests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LKCode.AddressManagement.IntegrationTests;

public class ErrorHandlingTests
{
    private const string ProblemDetailsMediaType = "application/problem+json";

    private AddressManagementWebApplicationFactory _factory = null!;
    private WebApplicationFactory<Program> _throwingFactory = null!;
    private HttpClient _client = null!;
    private HttpClient _throwingClient = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new AddressManagementWebApplicationFactory();
        _throwingFactory = _factory.WithThrowingController();
        _client = _factory.CreateClient();
        _throwingClient = _throwingFactory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _throwingClient.Dispose();
        _client.Dispose();
        await _throwingFactory.DisposeAsync();
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

    [Test]
    public async Task GetAsync_UnexpectedException_Returns500()
    {
        // Act
        using HttpResponseMessage response = await _throwingClient.GetAsync(ThrowingController.Path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Test]
    public async Task GetAsync_UnexpectedException_ReturnsProblemDetails()
    {
        // Act
        using HttpResponseMessage response = await _throwingClient.GetAsync(ThrowingController.Path);

        // Assert
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.InternalServerError);
        body.GetProperty("title").GetString().Should().Be("An error occurred while processing your request.");
        body.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.6.1");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task GetAsync_UnexpectedException_DoesNotExposeExceptionDetails()
    {
        // Act
        using HttpResponseMessage response = await _throwingClient.GetAsync(ThrowingController.Path);

        // Assert
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("type", "title", "status", "traceId");
        string text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain(ThrowingController.ExceptionMessage)
            .And.NotContain(nameof(InvalidOperationException))
            .And.NotContain(nameof(ThrowingController));
    }

    [Test]
    public async Task GetAsync_UnexpectedExceptionForHtmlClient_ReturnsProblemDetailsWithoutExceptionDetails()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, ThrowingController.Path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        // Act
        using HttpResponseMessage response = await _throwingClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("type", "title", "status", "traceId");
    }
}
