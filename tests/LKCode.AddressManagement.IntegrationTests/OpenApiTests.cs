using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace LKCode.AddressManagement.IntegrationTests;

public class OpenApiTests
{
    private const string OpenApiDocumentPath = "/openapi/v1.json";
    private const string ScalarPath = "/scalar";

    private AddressManagementWebApplicationFactory _factory = null!;
    private WebApplicationFactory<Program> _productionFactory = null!;
    private HttpClient _client = null!;
    private HttpClient _productionClient = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new AddressManagementWebApplicationFactory();
        _productionFactory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production));
        _client = _factory.CreateClient();
        _productionClient = _productionFactory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _productionClient.Dispose();
        _client.Dispose();
        await _productionFactory.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task GetAsync_OpenApiDocument_Returns200()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync(OpenApiDocumentPath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetAsync_OpenApiDocument_ReturnsOpenApi3Document()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync(OpenApiDocumentPath);

        // Assert
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/json");
        JsonElement document = await response.Content.ReadFromJsonAsync<JsonElement>();
        document.GetProperty("openapi").GetString().Should().StartWith("3.");
        document.GetProperty("info").GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        document.TryGetProperty("paths", out _).Should().BeTrue();
    }

    [Test]
    public async Task GetAsync_OpenApiDocumentInProduction_Returns200()
    {
        // Act
        using HttpResponseMessage response = await _productionClient.GetAsync(OpenApiDocumentPath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetAsync_ScalarInDevelopment_ReturnsHtmlPage()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync(ScalarPath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("text/html");
        string html = await response.Content.ReadAsStringAsync();
        html.Should().Contain(OpenApiDocumentPath);
    }

    [Test]
    public async Task GetAsync_ScalarInProduction_Returns404WithProblemDetails()
    {
        // Act
        using HttpResponseMessage response = await _productionClient.GetAsync(ScalarPath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/problem+json");
    }

    [Test]
    public async Task GetAsync_UnknownOpenApiDocument_Returns404()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v2.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
