using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Domain.Addresses;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests listing addresses when some of them are deleted. All tests read the same addresses, which are created once
/// for the fixture: Solingen and Köln each have an active and a deleted address with the same values, Berlin only a
/// deleted one.
/// </summary>
public class GetAddressListWithDeletedAddressesTests
{
    private const string Path = "/api/addresses";
    private const string Solingen = "Solingen";
    private const string Koeln = "Köln";
    private const string Berlin = "Berlin";

    private readonly Dictionary<string, AddressResponse> _activeByCity = new(StringComparer.Ordinal);
    private readonly List<AddressResponse> _deleted = [];

    private AddressManagementWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new AddressManagementWebApplicationFactory();
        _client = _factory.CreateClient();

        await SeedDeletedAsync(Solingen, "42651");
        _activeByCity.Add(Solingen, await SeedAsync(_factory, Solingen, "42651"));
        _activeByCity.Add(Koeln, await SeedAsync(_factory, Koeln, "50667"));
        await SeedDeletedAsync(Koeln, "50667");
        await SeedDeletedAsync(Berlin, "10115");
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task GetListAsync_NoFilters_ReturnsOnlyActiveAddresses()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync(Path);

        // Assert
        await AssertAddressesAsync(response, Solingen, Koeln);
        foreach (AddressResponse deleted in _deleted)
        {
            Address? stored = await _factory.FindStoredAddressAsync(deleted.Id);
            stored.Should().NotBeNull();
            stored!.IsDeleted.Should().BeTrue();
        }
    }

    [TestCase("Solingen", null, Solingen)]
    [TestCase("sol", null, Solingen)]
    [TestCase("KOELN", null, Koeln)]
    [TestCase(null, "42651", Solingen)]
    [TestCase(null, "506", Koeln)]
    [TestCase("Köln", "50667", Koeln)]
    public async Task GetListAsync_FilterMatchingActiveAndDeletedAddresses_ReturnsOnlyActiveAddress(
        string? city,
        string? postalCode,
        string expectedCity)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(city, postalCode);

        // Assert
        await AssertAddressesAsync(response, expectedCity);
    }

    [TestCase("Berlin", null)]
    [TestCase("erl", null)]
    [TestCase(null, "10115")]
    [TestCase("Berlin", "10115")]
    public async Task GetListAsync_FilterMatchingOnlyDeletedAddress_Returns200WithEmptyArray(
        string? city,
        string? postalCode)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(city, postalCode);

        // Assert
        await AssertAddressesAsync(response);
    }

    [Test]
    public async Task GetListAsync_AllAddressesDeleted_Returns200WithEmptyArray()
    {
        // Arrange
        await using var factory = new AddressManagementWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        foreach (string city in new[] { Solingen, Koeln })
        {
            AddressResponse address = await SeedAsync(factory, city, "12345");
            using HttpResponseMessage deletion =
                await client.SendValidAsync(HttpMethod.Delete, address.Id, address.ToETag());
            deletion.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // Act
        using HttpResponseMessage response = await client.GetAsync(Path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(0);
        body.GetProperty("totalCount").GetInt32().Should().Be(0);
        body.GetProperty("totalPages").GetInt32().Should().Be(0);
    }

    private static Task<AddressResponse> SeedAsync(
        AddressManagementWebApplicationFactory factory,
        string city,
        string postalCode) =>
        factory.SeedAddressAsync(AddressPayloads.ValidRequest with { City = city, PostalCode = postalCode });

    /// <summary>
    /// Creates an address and deletes it through the API.
    /// </summary>
    private async Task SeedDeletedAsync(string city, string postalCode)
    {
        AddressResponse address = await SeedAsync(_factory, city, postalCode);
        using HttpResponseMessage deletion =
            await _client.SendValidAsync(HttpMethod.Delete, address.Id, address.ToETag());
        deletion.EnsureSuccessStatusCode();
        _deleted.Add(address);
    }

    private Task<HttpResponseMessage> GetAsync(string? city, string? postalCode)
    {
        var parameters = new List<string>();
        if (city is not null)
        {
            parameters.Add($"city={Uri.EscapeDataString(city)}");
        }

        if (postalCode is not null)
        {
            parameters.Add($"postalCode={Uri.EscapeDataString(postalCode)}");
        }

        return _client.GetAsync($"{Path}?{string.Join('&', parameters)}");
    }

    private async Task AssertAddressesAsync(HttpResponseMessage response, params string[] expectedCities)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/json");
        AddressListResponse? body = await response.Content.ReadFromJsonAsync<AddressListResponse>();
        body.Should().NotBeNull();
        body.Items.Should().BeEquivalentTo(expectedCities.Select(city => _activeByCity[city]));
        body.TotalCount.Should().Be(expectedCities.Length);
    }
}
