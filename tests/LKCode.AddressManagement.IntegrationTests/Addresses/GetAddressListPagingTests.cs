using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests the pages and the sort order of the address list and the validation of their query parameters. All tests
/// read the addresses A to E, which are created once for the fixture with a fake clock.
/// </summary>
/// <remarks>
/// Each sort field has another order: last name B D E A C, first name A E C B D, city E C D A B, postal code
/// D B A C E, country B E C A D, creation time A B C D E and time of the last change B C E D A, because D and A are
/// changed after all addresses were created.
/// </remarks>
public class GetAddressListPagingTests
{
    private const string Path = "/api/addresses";

    private const string SortByError =
        "'Sort By' must be one of: lastName, firstName, city, postalCode, country, createdAt, updatedAt.";

    private const string SortDirectionError = "'Sort Direction' must be one of: asc, desc.";

    private static readonly DateTimeOffset _startTime = new(2026, 3, 14, 9, 26, 53, 589, TimeSpan.Zero);

    private static readonly string[] _byLastName = ["B", "D", "E", "A", "C"];

    private readonly Dictionary<string, AddressResponse> _addresses = new(StringComparer.Ordinal);

    private AddressManagementWebApplicationFactory _baseFactory = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private FakeTimeProvider _timeProvider = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _timeProvider = new FakeTimeProvider(_startTime);
        _baseFactory = new AddressManagementWebApplicationFactory();
        _factory = WithTimeProvider(_baseFactory, _timeProvider);
        _client = _factory.CreateClient();

        await SeedAsync("A", "Schmidt", "Anna", "Hamburg", "20095", "DE");
        await SeedAsync("B", "Becker", "Paul", "Wien", "1010", "AT");
        await SeedAsync("C", "Wagner", "Lena", "Bern", "3011", "CH");
        await SeedAsync("D", "Fischer", "Tom", "Dresden", "01067", "LU");
        await SeedAsync("E", "Meyer", "Clara", "Aachen", "52062", "BE");
        await TouchAsync("D");
        await TouchAsync("A");
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _baseFactory.DisposeAsync();
    }

    [Test]
    public async Task GetListAsync_NoParameters_ReturnsFirstPageOfTwentySortedByLastName()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync(Path);

        // Assert
        AddressListResponse page = await ReadPageAsync(response);
        page.Items.Should().Equal(_byLastName.Select(key => _addresses[key]));
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(20);
        page.TotalCount.Should().Be(5);
        page.TotalPages.Should().Be(1);
    }

    [TestCase("lastName", "asc", new[] { "B", "D", "E", "A", "C" })]
    [TestCase("lastName", "desc", new[] { "C", "A", "E", "D", "B" })]
    [TestCase("firstName", "asc", new[] { "A", "E", "C", "B", "D" })]
    [TestCase("firstName", "desc", new[] { "D", "B", "C", "E", "A" })]
    [TestCase("city", "asc", new[] { "E", "C", "D", "A", "B" })]
    [TestCase("city", "desc", new[] { "B", "A", "D", "C", "E" })]
    [TestCase("postalCode", "asc", new[] { "D", "B", "A", "C", "E" })]
    [TestCase("postalCode", "desc", new[] { "E", "C", "A", "B", "D" })]
    [TestCase("country", "asc", new[] { "B", "E", "C", "A", "D" })]
    [TestCase("country", "desc", new[] { "D", "A", "C", "E", "B" })]
    [TestCase("createdAt", "asc", new[] { "A", "B", "C", "D", "E" })]
    [TestCase("createdAt", "desc", new[] { "E", "D", "C", "B", "A" })]
    [TestCase("updatedAt", "asc", new[] { "B", "C", "E", "D", "A" })]
    [TestCase("updatedAt", "desc", new[] { "A", "D", "E", "C", "B" })]
    public async Task GetListAsync_SortByAndDirection_ReturnsAddressesInOrder(
        string sortBy,
        string sortDirection,
        string[] expectedOrder)
    {
        // Act
        using HttpResponseMessage response = await GetAsync($"sortBy={sortBy}&sortDirection={sortDirection}");

        // Assert
        await AssertOrderAsync(response, expectedOrder);
    }

    [TestCase("firstName", new[] { "A", "E", "C", "B", "D" })]
    [TestCase("createdAt", new[] { "A", "B", "C", "D", "E" })]
    public async Task GetListAsync_SortByOnly_SortsAscending(string sortBy, string[] expectedOrder)
    {
        // Act
        using HttpResponseMessage response = await GetAsync($"sortBy={sortBy}");

        // Assert
        await AssertOrderAsync(response, expectedOrder);
    }

    [Test]
    public async Task GetListAsync_SortDirectionOnly_SortsByLastName()
    {
        // Act
        using HttpResponseMessage response = await GetAsync("sortDirection=desc");

        // Assert
        await AssertOrderAsync(response, "C", "A", "E", "D", "B");
    }

    [TestCase("sortBy=City&sortDirection=DESC")]
    [TestCase("sortBy=CITY&sortDirection=Desc")]
    [TestCase("sortBy=%20city%20&sortDirection=%09desc%20")]
    public async Task GetListAsync_SortValuesInOtherCaseOrWithWhitespace_AreAccepted(string queryString)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(queryString);

        // Assert
        await AssertOrderAsync(response, "B", "A", "D", "C", "E");
    }

    [TestCase("page=&pageSize=&sortBy=&sortDirection=")]
    [TestCase("page=%20&pageSize=%20&sortBy=%20&sortDirection=%20")]
    [TestCase("page&pageSize&sortBy&sortDirection")]
    public async Task GetListAsync_EmptyParameters_UseDefaults(string queryString)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(queryString);

        // Assert
        AddressListResponse page = await ReadPageAsync(response);
        page.Items.Select(address => address.Id).Should().Equal(Ids(_byLastName));
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(20);
    }

    [TestCase(1, 2, new[] { "B", "D" }, 3)]
    [TestCase(2, 2, new[] { "E", "A" }, 3)]
    [TestCase(3, 2, new[] { "C" }, 3)]
    [TestCase(1, 1, new[] { "B" }, 5)]
    [TestCase(5, 1, new[] { "C" }, 5)]
    [TestCase(1, 5, new[] { "B", "D", "E", "A", "C" }, 1)]
    [TestCase(2, 4, new[] { "C" }, 2)]
    [TestCase(1, 100, new[] { "B", "D", "E", "A", "C" }, 1)]
    public async Task GetListAsync_PageWithinList_ReturnsAddressesOfPage(
        int pageNumber,
        int pageSize,
        string[] expectedKeys,
        int expectedTotalPages)
    {
        // Act
        using HttpResponseMessage response = await GetAsync($"page={pageNumber}&pageSize={pageSize}");

        // Assert
        AddressListResponse page = await ReadPageAsync(response);
        page.Items.Select(address => address.Id).Should().Equal(Ids(expectedKeys));
        page.Page.Should().Be(pageNumber);
        page.PageSize.Should().Be(pageSize);
        page.TotalCount.Should().Be(5);
        page.TotalPages.Should().Be(expectedTotalPages);
    }

    [TestCase(4, 2, 3)]
    [TestCase(6, 1, 5)]
    [TestCase(2, 5, 1)]
    [TestCase(2, 20, 1)]
    [TestCase(2, 100, 1)]
    [TestCase(int.MaxValue, 1, 5)]
    [TestCase(int.MaxValue, 100, 1)]
    public async Task GetListAsync_PageBehindEnd_Returns200WithoutItems(
        int pageNumber,
        int pageSize,
        int expectedTotalPages)
    {
        // Act
        using HttpResponseMessage response = await GetAsync($"page={pageNumber}&pageSize={pageSize}");

        // Assert
        AddressListResponse page = await ReadPageAsync(response);
        page.Items.Should().BeEmpty();
        page.Page.Should().Be(pageNumber);
        page.PageSize.Should().Be(pageSize);
        page.TotalCount.Should().Be(5);
        page.TotalPages.Should().Be(expectedTotalPages);
    }

    [Test]
    public async Task GetListAsync_AllPages_ReturnEveryAddressOnceInOrder()
    {
        // Arrange
        var ids = new List<Guid>();

        // Act
        for (int pageNumber = 1; pageNumber <= 3; pageNumber++)
        {
            using HttpResponseMessage response = await GetAsync($"page={pageNumber}&pageSize=2&sortBy=updatedAt");
            AddressListResponse page = await ReadPageAsync(response);
            ids.AddRange(page.Items.Select(address => address.Id));
        }

        // Assert
        ids.Should().Equal(Ids("B", "C", "E", "D", "A"));
    }

    [TestCase("city=en", new[] { "B", "D", "E" })]
    [TestCase("city=en&page=2&pageSize=2", new[] { "E" })]
    [TestCase("city=en&sortBy=city&sortDirection=desc&pageSize=2", new[] { "B", "D" })]
    [TestCase("postalCode=0&sortBy=postalCode&page=2&pageSize=3", new[] { "C", "E" })]
    [TestCase("city=en&postalCode=10&sortBy=createdAt&sortDirection=desc", new[] { "D", "B" })]
    public async Task GetListAsync_FiltersWithPageAndSortOrder_PagesAndCountsOnlyMatchingAddresses(
        string queryString,
        string[] expectedKeys)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(queryString);

        // Assert
        AddressListResponse page = await ReadPageAsync(response);
        page.Items.Select(address => address.Id).Should().Equal(Ids(expectedKeys));
    }

    [TestCase("city=en", 3, 1)]
    [TestCase("city=en&pageSize=2", 3, 2)]
    [TestCase("postalCode=0&pageSize=3", 5, 2)]
    [TestCase("city=en&postalCode=10", 2, 1)]
    [TestCase("city=Hamburg&page=2", 1, 1)]
    public async Task GetListAsync_Filters_CountsOnlyMatchingAddresses(
        string queryString,
        int expectedTotalCount,
        int expectedTotalPages)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(queryString);

        // Assert
        AddressListResponse page = await ReadPageAsync(response);
        page.TotalCount.Should().Be(expectedTotalCount);
        page.TotalPages.Should().Be(expectedTotalPages);
    }

    [TestCase("city=Berlin")]
    [TestCase("city=Berlin&page=3&pageSize=1")]
    public async Task GetListAsync_FilterWithoutMatch_ReturnsPageWithoutItemsAndPages(string queryString)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(queryString);

        // Assert
        AddressListResponse page = await ReadPageAsync(response);
        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
        page.TotalPages.Should().Be(0);
    }

    [TestCase("asc")]
    [TestCase("desc")]
    public async Task GetListAsync_EqualValues_SortsByIdInSortDirection(string sortDirection)
    {
        // Arrange
        await using var baseFactory = new AddressManagementWebApplicationFactory();
        await using WebApplicationFactory<Program> factory =
            WithTimeProvider(baseFactory, new FakeTimeProvider(_startTime));
        using HttpClient client = factory.CreateClient();
        var ids = new List<Guid>();
        for (int index = 0; index < 4; index++)
        {
            ids.Add((await factory.SeedAddressAsync(AddressPayloads.ValidRequest)).Id);
        }

        // Act
        using HttpResponseMessage response = await client.GetAsync($"{Path}?sortDirection={sortDirection}");

        // Assert
        AddressListResponse page = await ReadPageAsync(response);
        page.Items.Select(address => address.Id)
            .Should().Equal(sortDirection == "asc" ? ids.Order() : ids.OrderDescending());
    }

    [TestCase("page=0", "page")]
    [TestCase("page=-1", "page")]
    [TestCase("page=-2147483648", "page")]
    [TestCase("page=abc", "page")]
    [TestCase("page=1.5", "page")]
    [TestCase("page=2147483648", "page")]
    [TestCase("pageSize=0", "pageSize")]
    [TestCase("pageSize=-1", "pageSize")]
    [TestCase("pageSize=101", "pageSize")]
    [TestCase("pageSize=2147483647", "pageSize")]
    [TestCase("pageSize=abc", "pageSize")]
    [TestCase("pageSize=20.0", "pageSize")]
    [TestCase("sortBy=email", "sortBy")]
    [TestCase("sortBy=id", "sortBy")]
    [TestCase("sortBy=street", "sortBy")]
    [TestCase("sortBy=version", "sortBy")]
    [TestCase("sortBy=normalizedCity", "sortBy")]
    [TestCase("sortBy=1", "sortBy")]
    [TestCase("sortBy=lastName,city", "sortBy")]
    [TestCase("sortBy=last%20name", "sortBy")]
    [TestCase("sortDirection=up", "sortDirection")]
    [TestCase("sortDirection=ascending", "sortDirection")]
    [TestCase("sortDirection=1", "sortDirection")]
    [TestCase("sortDirection=asc,desc", "sortDirection")]
    [TestCase("city=Hamburg&postalCode=20095&pageSize=0", "pageSize")]
    public async Task GetListAsync_InvalidParameter_Returns400WithErrorOfParameter(
        string queryString,
        string expectedKey)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(queryString);

        // Assert
        ValidationProblemDetails problem = await ReadValidationProblemAsync(response);
        problem.Errors.Keys.Should().Equal(expectedKey);
    }

    [Test]
    public async Task GetListAsync_UnknownSortValues_Returns400WithAllowedValues()
    {
        // Act
        using HttpResponseMessage response = await GetAsync("sortBy=email&sortDirection=up");

        // Assert
        ValidationProblemDetails problem = await ReadValidationProblemAsync(response);
        problem.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["sortBy"] = [SortByError],
            ["sortDirection"] = [SortDirectionError],
        });
    }

    [Test]
    public async Task GetListAsync_NoInteger_Returns400WithValueAndParameter()
    {
        // Act
        using HttpResponseMessage response = await GetAsync("page=abc");

        // Assert
        ValidationProblemDetails problem = await ReadValidationProblemAsync(response);
        problem.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["page"] = ["The value 'abc' is not valid."],
        });
    }

    [TestCase("page=0&pageSize=101&sortBy=email&sortDirection=up")]
    [TestCase("page=abc&pageSize=0&sortBy=email&sortDirection=up")]
    [TestCase("page=-1&pageSize=1.5&sortBy=1&sortDirection=1")]
    public async Task GetListAsync_AllParametersInvalid_Returns400WithAllErrors(string queryString)
    {
        // Act
        using HttpResponseMessage response = await GetAsync(queryString);

        // Assert
        ValidationProblemDetails problem = await ReadValidationProblemAsync(response);
        problem.Errors.Keys.Should().BeEquivalentTo("page", "pageSize", "sortBy", "sortDirection");
        problem.Errors.Values.Should().AllSatisfy(messages => messages.Should().ContainSingle());
    }

    private static WebApplicationFactory<Program> WithTimeProvider(
        AddressManagementWebApplicationFactory factory,
        TimeProvider timeProvider) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(timeProvider)));

    private static async Task<AddressListResponse> ReadPageAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/json");
        AddressListResponse? page = await response.Content.ReadFromJsonAsync<AddressListResponse>();
        page.Should().NotBeNull();

        return page;
    }

    private static async Task<ValidationProblemDetails> ReadValidationProblemAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/problem+json");
        ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem.Status.Should().Be((int)HttpStatusCode.BadRequest);

        return problem;
    }

    private Task<HttpResponseMessage> GetAsync(string queryString) => _client.GetAsync($"{Path}?{queryString}");

    private IEnumerable<Guid> Ids(params string[] keys) => keys.Select(key => _addresses[key].Id);

    private async Task AssertOrderAsync(HttpResponseMessage response, params string[] expectedOrder)
    {
        AddressListResponse page = await ReadPageAsync(response);
        page.Items.Select(address => address.Id).Should().Equal(Ids(expectedOrder));
        page.TotalCount.Should().Be(5);
    }

    private async Task SeedAsync(
        string key,
        string lastName,
        string firstName,
        string city,
        string postalCode,
        string country)
    {
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        _addresses[key] = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest with
        {
            LastName = lastName,
            FirstName = firstName,
            City = city,
            PostalCode = postalCode,
            Country = country,
        });
    }

    /// <summary>
    /// Changes the address with an empty JSON Patch document, which keeps its values but sets the time of the change.
    /// </summary>
    private async Task TouchAsync(string key)
    {
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        using HttpResponseMessage response = await _client.SendWithIfMatchAsync(
            HttpMethod.Patch,
            $"{Path}/{_addresses[key].Id}",
            AddressChangeRequests.CreateContent(HttpMethod.Patch, "[]"),
            "*");
        response.EnsureSuccessStatusCode();
        _addresses[key] = (await response.Content.ReadFromJsonAsync<AddressResponse>())!;
    }
}
