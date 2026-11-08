using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests the strong entity tag in the <c>ETag</c> header of every response with a single address: its version in
/// quotes, e.g. <c>"3"</c>.
/// </summary>
public class AddressETagTests
{
    private const string Path = "/api/addresses";
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
    public async Task GetAsync_KnownId_ReturnsStrongETagOfVersion()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await _client.GetAsync($"{Path}/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertETag(response, "\"1\"");
        (await response.Content.ReadFromJsonAsync<AddressResponse>())!.Version.Should().Be(1);
    }

    [Test]
    public async Task GetAsync_UnknownId_ReturnsNoETag()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync($"{Path}/{UnknownId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.ETag.Should().BeNull();
    }

    [Test]
    public async Task GetListAsync_Addresses_ReturnsNoETag()
    {
        // Arrange
        await SeedAsync();

        // Act
        using HttpResponseMessage response = await _client.GetAsync(Path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().BeNull();
    }

    [Test]
    public async Task PostAsync_ValidAddress_ReturnsETagOfFirstVersion()
    {
        // Act
        using HttpResponseMessage response = await _client.PostAsJsonAsync(Path, AddressPayloads.Valid());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        AssertETag(response, "\"1\"");
        (await response.Content.ReadFromJsonAsync<AddressResponse>())!.Version.Should().Be(1);
    }

    [Test]
    public async Task PostAsync_InvalidAddress_ReturnsNoETag()
    {
        // Act
        using HttpResponseMessage response =
            await _client.PostAsJsonAsync(Path, AddressPayloads.ValidWith("country", "XX"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.ETag.Should().BeNull();
    }

    [TestCaseSource(typeof(AddressChangeRequests), nameof(AddressChangeRequests.MethodsWithBody))]
    public async Task ChangeAsync_ValidChange_ReturnsETagOfNextVersion(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response = await _client.SendValidAsync(method, created.Id, created.ToETag());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertETag(response, "\"2\"");
        (await response.Content.ReadFromJsonAsync<AddressResponse>())!.Version.Should().Be(2);
    }

    [TestCaseSource(typeof(AddressChangeRequests), nameof(AddressChangeRequests.MethodsWithBody))]
    public async Task ChangeAsync_InvalidBody_ReturnsNoETag(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        string body = method == HttpMethod.Put
            ? AddressPayloads.ValidWith("country", "XX").ToJsonString()
            : """[{"op": "replace", "path": "/country", "value": "XX"}]""";

        // Act
        using HttpResponseMessage response = await _client.SendWithIfMatchAsync(
            method,
            $"{Path}/{created.Id}",
            AddressChangeRequests.CreateContent(method, body),
            created.ToETag());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.ETag.Should().BeNull();
    }

    [Test]
    public async Task DeleteAsync_KnownId_ReturnsNoETag()
    {
        // Arrange
        AddressResponse created = await SeedAsync();

        // Act
        using HttpResponseMessage response =
            await _client.SendValidAsync(HttpMethod.Delete, created.Id, created.ToETag());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.ETag.Should().BeNull();
    }

    [Test]
    public async Task ChangeAsync_SeveralChangesWithETagOfLastResponse_IncreaseVersionByOneEach()
    {
        // Arrange
        AddressResponse created = await SeedAsync();
        string eTag = created.ToETag();
        var statusCodes = new List<HttpStatusCode>();
        var eTags = new List<string>();

        // Act
        foreach (HttpMethod method in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Put })
        {
            using HttpResponseMessage response = await _client.SendValidAsync(method, created.Id, eTag);
            eTag = response.Headers.ETag?.ToString() ?? string.Empty;
            statusCodes.Add(response.StatusCode);
            eTags.Add(eTag);
        }

        using HttpResponseMessage getResponse = await _client.GetAsync($"{Path}/{created.Id}");

        // Assert
        statusCodes.Should().AllBeEquivalentTo(HttpStatusCode.OK);
        eTags.Should().Equal("\"2\"", "\"3\"", "\"4\"");
        AssertETag(getResponse, "\"4\"");
    }

    private static void AssertETag(HttpResponseMessage response, string expected)
    {
        EntityTagHeaderValue? eTag = response.Headers.ETag;
        eTag.Should().NotBeNull();
        eTag.IsWeak.Should().BeFalse();
        eTag.Tag.Should().Be(expected);
    }

    private Task<AddressResponse> SeedAsync() => _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
}
