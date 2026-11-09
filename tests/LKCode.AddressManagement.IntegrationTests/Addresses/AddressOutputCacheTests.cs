using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Results;
using LKCode.AddressManagement.IntegrationTests.Fakes;
using LKCode.AddressManagement.Service;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests the server-side output cache of the address endpoints. Every test has its own host, so it starts with an
/// empty database and an empty cache. A response comes from the cache if and only if it has the header <c>Age</c>.
/// </summary>
public class AddressOutputCacheTests
{
    private const string Path = AddressChangeRequests.Path;
    private const string UnknownId = "0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00";
    private const string ExpirationKey = $"{CachingOptions.SectionName}:{nameof(CachingOptions.Expiration)}";

    private static readonly DateTimeOffset _createdAt = new(2026, 3, 14, 9, 26, 53, 589, TimeSpan.Zero);

    private AddressManagementWebApplicationFactory _baseFactory = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _baseFactory = new AddressManagementWebApplicationFactory();
        _factory = _baseFactory.WithRecordingOutputCacheStore();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _baseFactory.DisposeAsync();
    }

    [Test]
    public async Task GetByIdAsync_RepeatedRequest_IsServedFromCacheWithSameAddressAndETag()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        using HttpResponseMessage first = await _client.GetAsync($"{Path}/{created.Id}");

        // Act
        using HttpResponseMessage second = await _client.GetAsync($"{Path}/{created.Id}");

        // Assert
        first.Headers.Age.Should().BeNull();
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.Age.Should().NotBeNull();
        (await second.Content.ReadFromJsonAsync<AddressResponse>()).Should().Be(created);
        second.Headers.ETag?.Tag.Should().Be(created.ToETag());
    }

    [Test]
    public async Task GetByIdAsync_AddressChangedBypassingApi_ReturnsCachedAddress()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        using HttpResponseMessage first = await _client.GetAsync($"{Path}/{created.Id}");
        await UpdateBypassingApiAsync(created.Id);

        // Act
        using HttpResponseMessage response = await _client.GetAsync($"{Path}/{created.Id}");

        // Assert
        response.Headers.Age.Should().NotBeNull();
        (await response.Content.ReadFromJsonAsync<AddressResponse>()).Should().Be(created);
        response.Headers.ETag?.Tag.Should().Be(created.ToETag());
    }

    [Test]
    public async Task GetListAsync_RepeatedRequest_IsServedFromCacheWithSamePage()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        using HttpResponseMessage first = await _client.GetAsync(Path);

        // Act
        using HttpResponseMessage second = await _client.GetAsync(Path);

        // Assert
        first.Headers.Age.Should().BeNull();
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.Age.Should().NotBeNull();
        AddressListResponse? page = await second.Content.ReadFromJsonAsync<AddressListResponse>();
        page!.Items.Should().Equal(created);
    }

    [Test]
    public async Task GetListAsync_AddressCreatedBypassingApi_ReturnsCachedPage()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        using HttpResponseMessage first = await _client.GetAsync(Path);
        await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

        // Act
        using HttpResponseMessage response = await _client.GetAsync(Path);

        // Assert
        response.Headers.Age.Should().NotBeNull();
        AddressListResponse? page = await response.Content.ReadFromJsonAsync<AddressListResponse>();
        page!.Items.Should().Equal(created);
        page.TotalCount.Should().Be(1);
    }

    [TestCase("city=Köln", "city=Berlin")]
    [TestCase("city=Köln", "city=köln")]
    [TestCase("postalCode=511", "postalCode=101")]
    [TestCase("page=1", "page=2")]
    [TestCase("pageSize=1", "pageSize=2")]
    [TestCase("sortBy=city", "sortBy=lastName")]
    [TestCase("sortDirection=asc", "sortDirection=desc")]
    [TestCase("", "city=Köln")]
    [TestCase("city=Köln", "city=Köln&page=1")]
    [TestCase("page=1&pageSize=1", "page=2&pageSize=1")]
    [TestCase("city=Köln", "unknown=1&city=Köln")]
    public async Task GetListAsync_OtherQueryParameters_IsNotServedFromCacheOfFirstQuery(
        string firstQuery,
        string secondQuery)
    {
        // Arrange
        await SeedInCitiesAsync();
        using HttpResponseMessage first = await _client.GetAsync($"{Path}?{firstQuery}");
        using HttpResponseMessage firstAgain = await _client.GetAsync($"{Path}?{firstQuery}");

        // Act
        using HttpResponseMessage second = await _client.GetAsync($"{Path}?{secondQuery}");

        // Assert
        firstAgain.Headers.Age.Should().NotBeNull();
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.Age.Should().BeNull();
    }

    [Test]
    public async Task GetListAsync_DifferentFilters_ReturnsMatchingAddressesOfEachFilter()
    {
        // Arrange
        (AddressResponse koeln, AddressResponse berlin) = await SeedInCitiesAsync();
        using HttpResponseMessage koelnFirst = await _client.GetAsync($"{Path}?city=Köln");

        // Act
        using HttpResponseMessage berlinResponse = await _client.GetAsync($"{Path}?city=Berlin");
        using HttpResponseMessage koelnResponse = await _client.GetAsync($"{Path}?city=Köln");

        // Assert
        (await berlinResponse.Content.ReadFromJsonAsync<AddressListResponse>())!.Items.Should().Equal(berlin);
        koelnResponse.Headers.Age.Should().NotBeNull();
        (await koelnResponse.Content.ReadFromJsonAsync<AddressListResponse>())!.Items.Should().Equal(koeln);
    }

    [Test]
    public async Task GetByIdAsync_Success_StoresResponseWithTagOfAddressForFiveMinutes()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

        // Act
        using HttpResponseMessage response = await _client.GetAsync($"{Path}/{created.Id}");

        // Assert
        StoredCacheEntry entry = _factory.GetRecordingOutputCacheStore().StoredEntries.Should().ContainSingle().Which;
        entry.Tags.Should().Equal($"address-{created.Id}");
        entry.ValidFor.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task GetByIdAsync_IdInOtherFormat_StoresResponseWithTagOfIdInLowercaseWithHyphens()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        string url = $"{Path}/{created.Id.ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant()}";

        // Act
        using HttpResponseMessage response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        StoredCacheEntry entry = _factory.GetRecordingOutputCacheStore().StoredEntries.Should().ContainSingle().Which;
        entry.Tags.Should().Equal($"address-{created.Id:D}");
    }

    [Test]
    public async Task GetListAsync_Success_StoresResponseWithListTagForFiveMinutes()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync($"{Path}?city=Köln&page=2");

        // Assert
        StoredCacheEntry entry = _factory.GetRecordingOutputCacheStore().StoredEntries.Should().ContainSingle().Which;
        entry.Tags.Should().Equal("addresses");
        entry.ValidFor.Should().Be(TimeSpan.FromMinutes(5));
    }

    [TestCase("00:00:42", 42)]
    [TestCase("01:00:00", 3600)]
    [TestCase("1.00:00:00", 86400)]
    public async Task GetAsync_ConfiguredExpiration_StoresResponsesForConfiguredTime(
        string expiration,
        int expectedSeconds)
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = _factory.WithSetting(ExpirationKey, expiration);
        using HttpClient client = factory.CreateClient();
        AddressResponse created = await factory.SeedAddressAsync(AddressPayloads.ValidRequest);

        // Act
        using HttpResponseMessage single = await client.GetAsync($"{Path}/{created.Id}");
        using HttpResponseMessage list = await client.GetAsync(Path);

        // Assert
        factory.GetRecordingOutputCacheStore().StoredEntries.Select(entry => entry.ValidFor)
            .Should().Equal(TimeSpan.FromSeconds(expectedSeconds), TimeSpan.FromSeconds(expectedSeconds));
    }

    [TestCase("00:00:00")]
    [TestCase("-00:00:01")]
    [TestCase("1.00:00:01")]
    public async Task CreateClient_InvalidExpiration_FailsToStart(string expiration)
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = _baseFactory.WithSetting(ExpirationKey, expiration);

        // Act
        Action act = () => factory.CreateClient().Dispose();

        // Assert
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("The setting 'Caching:Expiration' must be greater than 00:00:00 and at most 1.00:00:00.");
    }

    [Test]
    public async Task PostAsync_CachedList_ReturnsListWithNewAddress()
    {
        // Arrange
        AddressResponse existing = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        await CacheAsync(Path);

        // Act
        using HttpResponseMessage post = await _client.PostAsJsonAsync(Path, AddressPayloads.Valid());

        // Assert
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        AddressResponse created = (await post.Content.ReadFromJsonAsync<AddressResponse>())!;
        using HttpResponseMessage response = await _client.GetAsync(Path);
        response.Headers.Age.Should().BeNull();
        AddressListResponse? page = await response.Content.ReadFromJsonAsync<AddressListResponse>();
        page!.Items.Should().BeEquivalentTo([existing, created]);
        page.TotalCount.Should().Be(2);
    }

    [Test]
    public async Task PostAsync_CachedAddress_KeepsAddressCached()
    {
        // Arrange
        AddressResponse existing = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        await CacheAsync($"{Path}/{existing.Id}");

        // Act
        using HttpResponseMessage post = await _client.PostAsJsonAsync(Path, AddressPayloads.Valid());

        // Assert
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        using HttpResponseMessage response = await _client.GetAsync($"{Path}/{existing.Id}");
        response.Headers.Age.Should().NotBeNull();
    }

    [Test]
    public async Task PostAsync_InvalidRequest_KeepsListCached()
    {
        // Arrange
        await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        await CacheAsync(Path);

        // Act
        using HttpResponseMessage post =
            await _client.PostAsJsonAsync(Path, AddressPayloads.ValidWith("firstName", " "));

        // Assert
        post.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using HttpResponseMessage response = await _client.GetAsync(Path);
        response.Headers.Age.Should().NotBeNull();
    }

    [TestCaseSource(nameof(MethodsWithBody))]
    public async Task ChangeAsync_CachedAddress_ReturnsChangedAddressAndETag(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        await CacheAsync($"{Path}/{created.Id}");

        // Act
        using HttpResponseMessage change = await _client.SendValidAsync(method, created.Id, created.ToETag());

        // Assert
        change.StatusCode.Should().Be(HttpStatusCode.OK);
        AddressResponse? changed = await change.Content.ReadFromJsonAsync<AddressResponse>();
        using HttpResponseMessage response = await _client.GetAsync($"{Path}/{created.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Age.Should().BeNull();
        (await response.Content.ReadFromJsonAsync<AddressResponse>()).Should().Be(changed);
        response.Headers.ETag?.Tag.Should().Be(AddressResponseExtensions.ToETag(2));
    }

    [Test]
    public async Task DeleteAsync_CachedAddress_ReturnsNotFound()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        await CacheAsync($"{Path}/{created.Id}");

        // Act
        using HttpResponseMessage deletion =
            await _client.SendValidAsync(HttpMethod.Delete, created.Id, created.ToETag());

        // Assert
        deletion.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using HttpResponseMessage response = await _client.GetAsync($"{Path}/{created.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Age.Should().BeNull();
    }

    [TestCaseSource(nameof(Methods))]
    public async Task ChangeAsync_CachedList_ReturnsListWithCurrentState(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        await CacheAsync(Path);

        // Act
        using HttpResponseMessage change = await _client.SendValidAsync(method, created.Id, "*");

        // Assert
        change.StatusCode.Should().Be(AddressChangeRequests.GetSuccessStatusCode(method));
        using HttpResponseMessage response = await _client.GetAsync(Path);
        response.Headers.Age.Should().BeNull();
        AddressListResponse? page = await response.Content.ReadFromJsonAsync<AddressListResponse>();
        if (method == HttpMethod.Delete)
        {
            page!.Items.Should().BeEmpty();
        }
        else
        {
            page!.Items.Should().Equal((await change.Content.ReadFromJsonAsync<AddressResponse>())!);
        }
    }

    [TestCaseSource(nameof(Methods))]
    public async Task ChangeAsync_OtherCachedAddress_KeepsItCached(HttpMethod method)
    {
        // Arrange
        AddressResponse changed = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        AddressResponse other = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        await CacheAsync($"{Path}/{other.Id}");

        // Act
        using HttpResponseMessage change = await _client.SendValidAsync(method, changed.Id, "*");

        // Assert
        change.StatusCode.Should().Be(AddressChangeRequests.GetSuccessStatusCode(method));
        using HttpResponseMessage response = await _client.GetAsync($"{Path}/{other.Id}");
        response.Headers.Age.Should().NotBeNull();
        (await response.Content.ReadFromJsonAsync<AddressResponse>()).Should().Be(other);
    }

    [TestCase("D")]
    [TestCase("N")]
    [TestCase("B")]
    [TestCase("P")]
    public async Task ChangeAsync_AddressCachedWithIdInOtherFormat_ReturnsChangedAddress(string format)
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        string url = $"{Path}/{created.Id.ToString(format, CultureInfo.InvariantCulture).ToUpperInvariant()}";
        await CacheAsync(url);

        // Act
        using HttpResponseMessage change = await _client.SendValidAsync(HttpMethod.Put, created.Id, "*");

        // Assert
        change.StatusCode.Should().Be(HttpStatusCode.OK);
        using HttpResponseMessage response = await _client.GetAsync(url);
        response.Headers.Age.Should().BeNull();
        (await response.Content.ReadFromJsonAsync<AddressResponse>())!.Version.Should().Be(2);
    }

    [TestCaseSource(nameof(FailedChangeCases))]
    public async Task ChangeAsync_Failed_KeepsAddressAndListCached(
        HttpMethod method,
        string? ifMatch,
        string? body,
        HttpStatusCode expectedStatusCode)
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        await CacheAsync($"{Path}/{created.Id}");
        await CacheAsync(Path);
        HttpContent? content = body is null
            ? AddressChangeRequests.CreateValidContent(method)
            : AddressChangeRequests.CreateContent(method, body);

        // Act
        using HttpResponseMessage change =
            await _client.SendWithIfMatchAsync(method, $"{Path}/{created.Id}", content, ifMatch);

        // Assert
        change.StatusCode.Should().Be(expectedStatusCode);
        using HttpResponseMessage single = await _client.GetAsync($"{Path}/{created.Id}");
        using HttpResponseMessage list = await _client.GetAsync(Path);
        single.Headers.Age.Should().NotBeNull();
        list.Headers.Age.Should().NotBeNull();
    }

    [TestCaseSource(nameof(Methods))]
    public async Task ChangeAsync_UnknownId_KeepsAddressAndListCached(HttpMethod method)
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        await CacheAsync($"{Path}/{created.Id}");
        await CacheAsync(Path);

        // Act
        using HttpResponseMessage change = await _client.SendValidAsync(method, Guid.Parse(UnknownId), "*");

        // Assert
        change.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using HttpResponseMessage single = await _client.GetAsync($"{Path}/{created.Id}");
        using HttpResponseMessage list = await _client.GetAsync(Path);
        single.Headers.Age.Should().NotBeNull();
        list.Headers.Age.Should().NotBeNull();
    }

    [TestCase(UnknownId)]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    [TestCase("not-a-guid")]
    [TestCase("123")]
    public async Task GetByIdAsync_NotFound_IsNotCached(string id)
    {
        // Arrange
        using HttpResponseMessage first = await _client.GetAsync($"{Path}/{id}");

        // Act
        using HttpResponseMessage second = await _client.GetAsync($"{Path}/{id}");

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.NotFound);
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
        second.Headers.Age.Should().BeNull();
        _factory.GetRecordingOutputCacheStore().StoredEntries.Should().BeEmpty();
    }

    [TestCase("pageSize=0")]
    [TestCase("page=abc")]
    [TestCase("sortBy=email")]
    [TestCase("page=0&pageSize=101&sortBy=email&sortDirection=up")]
    public async Task GetListAsync_InvalidParameters_IsNotCached(string query)
    {
        // Arrange
        using HttpResponseMessage first = await _client.GetAsync($"{Path}?{query}");

        // Act
        using HttpResponseMessage second = await _client.GetAsync($"{Path}?{query}");

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        second.Headers.Age.Should().BeNull();
        _factory.GetRecordingOutputCacheStore().StoredEntries.Should().BeEmpty();
    }

    [Test]
    public async Task GetByIdAsync_NotFoundThenFound_ReturnsAddressInsteadOfCachedNotFound()
    {
        // Arrange
        AddressResponse address = CreateAddress();
        IAddressService addressService = Substitute.For<IAddressService>();
        addressService.GetByIdAsync(address.Id, Arg.Any<CancellationToken>()).Returns((AddressResponse?)null, address);
        await using WebApplicationFactory<Program> factory = _factory.WithAddressService(addressService);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage first = await client.GetAsync($"{Path}/{address.Id}");

        // Act
        using HttpResponseMessage second = await client.GetAsync($"{Path}/{address.Id}");

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.NotFound);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.Age.Should().BeNull();
        (await second.Content.ReadFromJsonAsync<AddressResponse>()).Should().Be(address);
    }

    [Test]
    public async Task GetByIdAsync_UnexpectedErrorThenFound_ReturnsAddressInsteadOfCachedError()
    {
        // Arrange
        AddressResponse address = CreateAddress();
        IAddressService addressService = Substitute.For<IAddressService>();
        addressService.GetByIdAsync(address.Id, Arg.Any<CancellationToken>()).Returns(
            _ => Task.FromException<AddressResponse?>(new InvalidOperationException("Test error.")),
            _ => Task.FromResult<AddressResponse?>(address));
        await using WebApplicationFactory<Program> factory = _factory.WithAddressService(addressService);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage first = await client.GetAsync($"{Path}/{address.Id}");

        // Act
        using HttpResponseMessage second = await client.GetAsync($"{Path}/{address.Id}");

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.Age.Should().BeNull();
        (await second.Content.ReadFromJsonAsync<AddressResponse>()).Should().Be(address);
    }

    [Test]
    public async Task GetListAsync_UnexpectedErrorThenSuccess_ReturnsPageInsteadOfCachedError()
    {
        // Arrange
        var page = new AddressListResponse([CreateAddress()], 1, 20, 1, 1);
        IAddressService addressService = Substitute.For<IAddressService>();
        addressService.GetListAsync(Arg.Any<AddressListQuery>(), Arg.Any<CancellationToken>()).Returns(
            _ => Task.FromException<AddressListResponse>(new InvalidOperationException("Test error.")),
            _ => Task.FromResult(page));
        await using WebApplicationFactory<Program> factory = _factory.WithAddressService(addressService);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage first = await client.GetAsync(Path);

        // Act
        using HttpResponseMessage second = await client.GetAsync(Path);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.Age.Should().BeNull();
        (await second.Content.ReadFromJsonAsync<AddressListResponse>())!.Items.Should().Equal(page.Items);
    }

    [TestCase("If-None-Match", "\"1\"")]
    [TestCase("If-None-Match", "W/\"1\"")]
    [TestCase("If-None-Match", "*")]
    [TestCase("If-Modified-Since", "Fri, 01 Jan 2100 00:00:00 GMT")]
    public async Task GetByIdAsync_ConditionalRequestForCachedAddress_Returns200WithCurrentAddress(
        string headerName,
        string value)
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        await CacheAsync($"{Path}/{created.Id}");
        AddressResponse updated = await UpdateBypassingApiAsync(created.Id);

        // Act
        using HttpResponseMessage response = await GetWithHeaderAsync($"{Path}/{created.Id}", headerName, value);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Age.Should().BeNull();
        (await response.Content.ReadFromJsonAsync<AddressResponse>()).Should().Be(updated);
        response.Headers.ETag?.Tag.Should().Be(updated.ToETag());
    }

    [TestCase("If-None-Match", "*")]
    [TestCase("If-None-Match", "\"1\"")]
    [TestCase("If-Modified-Since", "Fri, 01 Jan 2100 00:00:00 GMT")]
    public async Task GetListAsync_ConditionalRequestForCachedList_Returns200WithCurrentPage(
        string headerName,
        string value)
    {
        // Arrange
        await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        await CacheAsync(Path);
        await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);

        // Act
        using HttpResponseMessage response = await GetWithHeaderAsync(Path, headerName, value);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Age.Should().BeNull();
        (await response.Content.ReadFromJsonAsync<AddressListResponse>())!.TotalCount.Should().Be(2);
    }

    [Test]
    public async Task GetByIdAsync_AfterConditionalRequest_IsServedFromCacheWithResponseOfConditionalRequest()
    {
        // Arrange
        AddressResponse created = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        await CacheAsync($"{Path}/{created.Id}");
        AddressResponse updated = await UpdateBypassingApiAsync(created.Id);
        using HttpResponseMessage conditional =
            await GetWithHeaderAsync($"{Path}/{created.Id}", "If-None-Match", created.ToETag());

        // Act
        using HttpResponseMessage response = await _client.GetAsync($"{Path}/{created.Id}");

        // Assert
        response.Headers.Age.Should().NotBeNull();
        (await response.Content.ReadFromJsonAsync<AddressResponse>()).Should().Be(updated);
    }

    private static IEnumerable<HttpMethod> Methods() => AddressChangeRequests.Methods;

    private static IEnumerable<HttpMethod> MethodsWithBody() => AddressChangeRequests.MethodsWithBody;

    /// <summary>
    /// Changes that fail: <c>412</c> (other version), <c>428</c> (no <c>If-Match</c>), <c>400</c> (invalid
    /// <c>If-Match</c>, invalid values) and <c>409</c> (failed <c>test</c>).
    /// </summary>
    private static IEnumerable<TestCaseData> FailedChangeCases()
    {
        foreach (HttpMethod method in AddressChangeRequests.Methods)
        {
            yield return new TestCaseData(method, "\"2\"", null, HttpStatusCode.PreconditionFailed);
            yield return new TestCaseData(method, null, null, HttpStatusCode.PreconditionRequired);
            yield return new TestCaseData(method, "1", null, HttpStatusCode.BadRequest);
        }

        yield return new TestCaseData(
            HttpMethod.Put,
            "*",
            """{"firstName": " "}""",
            HttpStatusCode.BadRequest);
        yield return new TestCaseData(
            HttpMethod.Patch,
            "*",
            """[{"op": "replace", "path": "/firstName", "value": " "}]""",
            HttpStatusCode.BadRequest);
        yield return new TestCaseData(
            HttpMethod.Patch,
            "*",
            """[{"op": "test", "path": "/firstName", "value": "Max"}]""",
            HttpStatusCode.Conflict);
    }

    private static AddressResponse CreateAddress() => new(
        Guid.CreateVersion7(_createdAt),
        "Erika",
        "Mustermann",
        "Heidestraße",
        "17",
        "51147",
        "Köln",
        "DE",
        "erika.mustermann@example.com",
        _createdAt,
        _createdAt,
        1);

    /// <summary>
    /// Requests the <paramref name="url"/> twice and asserts that the second response comes from the cache.
    /// </summary>
    private async Task CacheAsync(string url)
    {
        using HttpResponseMessage first = await _client.GetAsync(url);
        using HttpResponseMessage second = await _client.GetAsync(url);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.Age.Should().NotBeNull();
    }

    private async Task<HttpResponseMessage> GetWithHeaderAsync(string url, string headerName, string value)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation(headerName, value);

        return await _client.SendAsync(request);
    }

    /// <summary>
    /// Changes the address directly through the service of the host, so the output cache does not notice it.
    /// </summary>
    private async Task<AddressResponse> UpdateBypassingApiAsync(Guid id)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        IAddressService addressService = scope.ServiceProvider.GetRequiredService<IAddressService>();
        AddressChangeResult result =
            await addressService.UpdateAsync(id, null, AddressPayloads.UpdatedRequest, CancellationToken.None);

        return result.Address!;
    }

    /// <summary>
    /// Creates an address in Köln (51147) and one in Berlin (10115).
    /// </summary>
    private async Task<(AddressResponse Koeln, AddressResponse Berlin)> SeedInCitiesAsync()
    {
        AddressResponse koeln = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        AddressResponse berlin = await _factory.SeedAddressAsync(
            AddressPayloads.ValidRequest with { City = "Berlin", PostalCode = "10115" });

        return (koeln, berlin);
    }
}
