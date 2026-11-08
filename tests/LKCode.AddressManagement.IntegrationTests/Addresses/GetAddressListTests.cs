using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests listing addresses with the optional filters by city and postal code. All tests read the same addresses,
/// which are created once for the fixture.
/// </summary>
public class GetAddressListTests
{
    private const string Path = "/api/addresses";
    private const string Solingen = "Solingen";
    private const string Koeln = "Köln";
    private const string Muenchen = "München";
    private const string Giessen = "Gießen";
    private const string London = "London";

    private static readonly string[] _allCities = [Solingen, Koeln, Muenchen, Giessen, London];

    private readonly Dictionary<string, AddressResponse> _addressesByCity = new(StringComparer.Ordinal);

    private AddressManagementWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new AddressManagementWebApplicationFactory();
        _client = _factory.CreateClient();

        await SeedAsync(Solingen, "42651", "DE");
        await SeedAsync(Koeln, "50667", "DE");
        await SeedAsync(Muenchen, "80331", "DE");
        await SeedAsync(Giessen, "35390", "DE");
        await SeedAsync(London, "SW1A 1AA", "GB");
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task GetListAsync_NoFilters_Returns200WithAllAddresses()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync(Path);

        // Assert
        await AssertAddressesAsync(response, _allCities);
    }

    [Test]
    public async Task GetListAsync_NoAddresses_Returns200WithEmptyArray()
    {
        // Arrange
        await using var factory = new AddressManagementWebApplicationFactory();
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(Path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/json");
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array);
        body.GetArrayLength().Should().Be(0);
    }

    [Test]
    public async Task GetListAsync_MatchingAddress_ReturnsAllPropertiesInCamelCase()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync($"{Path}?city={Solingen}");

        // Assert
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateArray().Should().ContainSingle()
            .Which.EnumerateObject().Select(property => property.Name).Should().Equal(
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
                "updatedAt",
                "version");
    }

    [TestCase("Solingen")]
    [TestCase("sol")]
    [TestCase("ingen")]
    [TestCase("lin")]
    [TestCase("SOLin")]
    public async Task GetListAsync_PartOfCity_ReturnsMatchingAddress(string city)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(city: city);

        // Assert
        await AssertAddressesAsync(response, Solingen);
    }

    [TestCase("SOLINGEN")]
    [TestCase("solingen")]
    [TestCase("sOlInGeN")]
    public async Task GetListAsync_CityInAnyCase_ReturnsMatchingAddress(string city)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(city: city);

        // Assert
        await AssertAddressesAsync(response, Solingen);
    }

    [TestCase("Köln", Koeln)]
    [TestCase("Koeln", Koeln)]
    [TestCase("Koln", Koeln)]
    [TestCase("KÖLN", Koeln)]
    [TestCase("KOELN", Koeln)]
    [TestCase("köl", Koeln)]
    [TestCase("Muenchen", Muenchen)]
    [TestCase("Munchen", Muenchen)]
    [TestCase("MÜNCHEN", Muenchen)]
    [TestCase("Giessen", Giessen)]
    [TestCase("GIESSEN", Giessen)]
    [TestCase("ieß", Giessen)]
    public async Task GetListAsync_CitySpellingVariant_ReturnsMatchingAddress(string city, string expectedCity)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(city: city);

        // Assert
        await AssertAddressesAsync(response, expectedCity);
    }

    [TestCase(" sol ")]
    [TestCase("\tsolingen\n")]
    public async Task GetListAsync_CityWithSurroundingWhitespace_ReturnsMatchingAddress(string city)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(city: city);

        // Assert
        await AssertAddressesAsync(response, Solingen);
    }

    [TestCase("42651", Solingen)]
    [TestCase("426", Solingen)]
    [TestCase("265", Solingen)]
    [TestCase("651", Solingen)]
    [TestCase(" 42651 ", Solingen)]
    [TestCase("SW1A 1AA", London)]
    [TestCase("sw1a1aa", London)]
    [TestCase("1a 1a", London)]
    [TestCase("A1A", London)]
    public async Task GetListAsync_PartOfPostalCode_ReturnsMatchingAddress(string postalCode, string expectedCity)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(postalCode: postalCode);

        // Assert
        await AssertAddressesAsync(response, expectedCity);
    }

    [Test]
    public async Task GetListAsync_PartOfSeveralPostalCodes_ReturnsAllMatchingAddresses()
    {
        // Act
        using HttpResponseMessage response = await GetAsync(postalCode: "3");

        // Assert
        await AssertAddressesAsync(response, Muenchen, Giessen);
    }

    [TestCase("sol", "426")]
    [TestCase("SOLINGEN", "42651")]
    [TestCase("ingen", "651")]
    public async Task GetListAsync_CityAndPostalCode_ReturnsAddressMatchingBoth(string city, string postalCode)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(city, postalCode);

        // Assert
        await AssertAddressesAsync(response, Solingen);
    }

    [TestCase("Koeln", "42651")]
    [TestCase("sol", "SW1A")]
    public async Task GetListAsync_CityAndPostalCodeOfDifferentAddresses_Returns200WithEmptyArray(
        string city,
        string postalCode)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(city, postalCode);

        // Assert
        await AssertAddressesAsync(response);
    }

    [TestCase("Berlin", null)]
    [TestCase("Solingenx", null)]
    [TestCase("x", null)]
    [TestCase(null, "99999")]
    [TestCase(null, "426512")]
    public async Task GetListAsync_FilterWithoutMatch_Returns200WithEmptyArray(string? city, string? postalCode)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(city, postalCode);

        // Assert
        await AssertAddressesAsync(response);
    }

    [TestCase("?city=")]
    [TestCase("?postalCode=")]
    [TestCase("?city=&postalCode=")]
    [TestCase("?city=%20%20&postalCode=%20")]
    [TestCase("?city=%09")]
    [TestCase("?city")]
    public async Task GetListAsync_EmptyFilters_Returns200WithAllAddresses(string queryString)
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync(Path + queryString);

        // Assert
        await AssertAddressesAsync(response, _allCities);
    }

    [TestCase("?city=&postalCode=426")]
    [TestCase("?city=%20&postalCode=426")]
    [TestCase("?city=sol&postalCode=")]
    [TestCase("?city=sol&postalCode=%20")]
    public async Task GetListAsync_OneEmptyFilter_FiltersByOtherFilterOnly(string queryString)
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync(Path + queryString);

        // Assert
        await AssertAddressesAsync(response, Solingen);
    }

    private async Task SeedAsync(string city, string postalCode, string country)
    {
        AddressResponse address = await _factory.SeedAddressAsync(
            AddressPayloads.ValidRequest with { City = city, PostalCode = postalCode, Country = country });
        _addressesByCity.Add(city, address);
    }

    private Task<HttpResponseMessage> GetAsync(string? city = null, string? postalCode = null)
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
        List<AddressResponse>? body = await response.Content.ReadFromJsonAsync<List<AddressResponse>>();
        body.Should().BeEquivalentTo(expectedCities.Select(city => _addressesByCity[city]));
    }
}
