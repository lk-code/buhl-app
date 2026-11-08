using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

public class DeleteAddressTests
{
    private const string Path = "/api/addresses";
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
    public async Task DeleteAsync_KnownId_Returns204WithoutBody()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

        // Act
        using HttpResponseMessage response = await DeleteAsync(created);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Content.Headers.ContentType.Should().BeNull();
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task DeleteAsync_KnownId_RemovesOnlyThisAddress()
    {
        // Arrange
        AddressResponse deleted = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        AddressResponse kept = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

        // Act
        using HttpResponseMessage response = await DeleteAsync(deleted);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        IReadOnlyList<AddressResponse> stored = await _factory.ListAddressesAsync();
        stored.Should().NotContain(address => address.Id == deleted.Id);
        stored.Should().ContainEquivalentOf(kept);
    }

    [Test]
    public async Task DeleteAsync_KnownId_AddressIsNotInListAnymore()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

        // Act
        using HttpResponseMessage response = await DeleteAsync(created);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        IReadOnlyList<AddressResponse>? listed = await _client.GetFromJsonAsync<IReadOnlyList<AddressResponse>>(Path);
        listed.Should().NotContain(address => address.Id == created.Id);
    }

    [Test]
    public async Task GetAsync_DeletedAddress_Returns404WithProblemDetails()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        using HttpResponseMessage deleteResponse = await DeleteAsync(created);

        // Act
        using HttpResponseMessage response = await _client.GetAsync($"{Path}/{created.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertNotFoundAsync(response);
    }

    [Test]
    public async Task DeleteAsync_DeletedAddress_Returns404WithProblemDetails()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        using HttpResponseMessage firstResponse = await DeleteAsync(created);

        // Act
        using HttpResponseMessage response = await DeleteAsync(created);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertNotFoundAsync(response);
    }

    [Test]
    public async Task DeleteAsync_DeletedAddress_ReturnsSameProblemDetailsAsUnknownId()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        using HttpResponseMessage firstResponse = await DeleteAsync(created);

        // Act
        using HttpResponseMessage deletedDeleteResponse = await DeleteAsync(created);
        using HttpResponseMessage deletedGetResponse = await _client.GetAsync($"{Path}/{created.Id}");
        using HttpResponseMessage unknownDeleteResponse = await DeleteAsync(UnknownId);
        using HttpResponseMessage unknownGetResponse = await _client.GetAsync($"{Path}/{UnknownId}");

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadWithoutTraceIdAsync(deletedDeleteResponse))
            .Should().Equal(await ReadWithoutTraceIdAsync(unknownDeleteResponse));
        (await ReadWithoutTraceIdAsync(deletedGetResponse))
            .Should().Equal(await ReadWithoutTraceIdAsync(unknownGetResponse));
    }

    [TestCase(UnknownId)]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    public async Task DeleteAsync_UnknownId_Returns404WithProblemDetails(string id)
    {
        // Act
        using HttpResponseMessage response = await DeleteAsync(id);

        // Assert
        await AssertNotFoundAsync(response);
    }

    [Test]
    public async Task DeleteAsync_UnknownId_ReturnsSameProblemDetailsAsUnknownRoute()
    {
        // Act
        using HttpResponseMessage response = await DeleteAsync(UnknownId);
        using HttpResponseMessage unknownRouteResponse = await _client.DeleteAsync("/does-not-exist");

        // Assert
        IReadOnlyDictionary<string, string> body = await ReadWithoutTraceIdAsync(response);
        IReadOnlyDictionary<string, string> unknownRouteBody = await ReadWithoutTraceIdAsync(unknownRouteResponse);
        body.Should().Equal(unknownRouteBody);
    }

    [Test]
    public async Task DeleteAsync_UnknownId_DoesNotDeleteAnyAddress()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        int countBefore = (await _factory.ListAddressesAsync()).Count;

        // Act
        using HttpResponseMessage response = await DeleteAsync(UnknownId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        IReadOnlyList<AddressResponse> stored = await _factory.ListAddressesAsync();
        stored.Should().HaveCount(countBefore).And.ContainEquivalentOf(created);
    }

    [TestCase("not-a-guid")]
    [TestCase("123")]
    [TestCase("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f0")]
    [TestCase("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f000")]
    [TestCase("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1fzz")]
    public async Task DeleteAsync_InvalidId_Returns404WithProblemDetails(string id)
    {
        // Act
        using HttpResponseMessage response = await DeleteAsync(id);

        // Assert
        await AssertNotFoundAsync(response);
    }

    private static async Task AssertNotFoundAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (response.Content.Headers.ContentType?.MediaType).Should().Be(ProblemDetailsMediaType);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.NotFound);
        body.GetProperty("title").GetString().Should().Be("Not Found");
        body.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.5");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadWithoutTraceIdAsync(
        HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.EnumerateObject()
            .Where(property => property.Name != "traceId")
            .ToDictionary(property => property.Name, property => property.Value.GetRawText());
    }

    /// <summary>
    /// Deletes the <paramref name="address"/> with the entity tag of its version in the <c>If-Match</c> header.
    /// </summary>
    private Task<HttpResponseMessage> DeleteAsync(AddressResponse address) =>
        DeleteAsync(address.Id.ToString(), address.ToETag());

    private Task<HttpResponseMessage> DeleteAsync(string id, string ifMatch = "\"1\"") =>
        _client.SendWithIfMatchAsync(HttpMethod.Delete, $"{Path}/{id}", content: null, ifMatch);
}
