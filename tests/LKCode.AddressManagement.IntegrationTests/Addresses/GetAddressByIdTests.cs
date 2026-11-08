using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

public class GetAddressByIdTests
{
    private const string ProblemDetailsMediaType = "application/problem+json";
    private const string UnknownId = "0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00";

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

    [Test]
    public async Task GetAsync_KnownId_Returns200WithAddress()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

        // Act
        using HttpResponseMessage response = await _client.GetAsync($"/api/addresses/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/json");
        AddressResponse? body = await response.Content.ReadFromJsonAsync<AddressResponse>();
        body.Should().Be(created);
    }

    [Test]
    public async Task GetAsync_KnownId_ReturnsAllPropertiesInCamelCase()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

        // Act
        using HttpResponseMessage response = await _client.GetAsync($"/api/addresses/{created.Id}");

        // Assert
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateObject().Select(property => property.Name).Should().Equal(
            "id",
            "firstName",
            "lastName",
            "street",
            "houseNumber",
            "postalCode",
            "city",
            "country",
            "email",
            "createdAt",
            "updatedAt");
    }

    [Test]
    public async Task GetAsync_AddressWithoutEmail_ReturnsEmailAsNull()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest with { Email = null });

        // Act
        using HttpResponseMessage response = await _client.GetAsync($"/api/addresses/{created.Id}");

        // Assert
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [TestCase(UnknownId)]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    public async Task GetAsync_UnknownId_Returns404WithProblemDetails(string id)
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync($"/api/addresses/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.NotFound);
        body.GetProperty("title").GetString().Should().Be("Not Found");
        body.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.5");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task GetAsync_UnknownId_ReturnsSameProblemDetailsAsUnknownRoute()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync($"/api/addresses/{UnknownId}");
        using HttpResponseMessage unknownRouteResponse = await _client.GetAsync("/does-not-exist");

        // Assert
        IReadOnlyDictionary<string, string> body = await ReadWithoutTraceIdAsync(response);
        IReadOnlyDictionary<string, string> unknownRouteBody = await ReadWithoutTraceIdAsync(unknownRouteResponse);
        body.Should().Equal(unknownRouteBody);
    }

    [TestCase("not-a-guid")]
    [TestCase("123")]
    [TestCase("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f0")]
    [TestCase("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f000")]
    [TestCase("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1fzz")]
    public async Task GetAsync_InvalidId_Returns404WithProblemDetails(string id)
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync($"/api/addresses/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.NotFound);
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadWithoutTraceIdAsync(
        HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.EnumerateObject()
            .Where(property => property.Name != "traceId")
            .ToDictionary(property => property.Name, property => property.Value.GetRawText());
    }
}
