using System.Net;
using System.Text;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Service.AuthorizationPolicies;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests that the address endpoints require the scope <c>addresses:read</c> to read and <c>addresses:write</c> to
/// change addresses, and answer a valid token without the scope with <c>403 Forbidden</c> and Problem Details.
/// </summary>
public class AddressAuthorizationTests
{
    private const string Read = AddressScopes.Read;
    private const string Write = AddressScopes.Write;

    /// <summary>
    /// The claims of tokens that grant both scopes: as one claim separated by spaces, as JSON array (like
    /// <c>dotnet user-jwts</c>), as <c>scp</c> (like Entra ID) and between other scopes.
    /// </summary>
    private static readonly (string Name, Dictionary<string, object> Claims)[] _claimsWithBothScopes =
    [
        ("scope separated by spaces", new() { ["scope"] = $"{Read} {Write}" }),
        ("scope as array", new() { ["scope"] = new[] { Read, Write } }),
        ("scp separated by spaces", new() { ["scp"] = $"{Write} {Read}" }),
        ("scp as array", new() { ["scp"] = new[] { Write, Read } }),
        ("scope between other scopes", new() { ["scope"] = $"openid {Write} profile {Read} email" }),
        ("scope and scp", new() { ["scope"] = Read, ["scp"] = Write }),
    ];

    /// <summary>
    /// The claims of tokens that look similar but grant none of the scopes.
    /// </summary>
    private static readonly (string Name, Dictionary<string, object> Claims)[] _claimsWithoutScopes =
    [
        ("no claims", []),
        ("empty scope", new() { ["scope"] = string.Empty }),
        ("other scopes", new() { ["scope"] = "openid profile" }),
        ("other case", new() { ["scope"] = "Addresses:Read ADDRESSES:WRITE" }),
        ("longer scopes", new() { ["scope"] = "addresses:readonly addresses:writes" }),
        ("shorter scope", new() { ["scope"] = "addresses" }),
        ("separated by commas", new() { ["scope"] = $"{Read},{Write}" }),
        ("other claim type", new() { ["roles"] = new[] { Read, Write } }),
    ];

    private AddressManagementWebApplicationFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new AddressManagementWebApplicationFactory();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _factory.DisposeAsync();
    }

    [TestCaseSource(nameof(ReadingEndpoints))]
    public async Task SendAsync_ReadingEndpointWithReadScopeOnly_Succeeds(AddressEndpoint endpoint)
    {
        // Act
        using HttpResponseMessage response = await SendAsync(endpoint, TestTokens.WithScopes(Read));

        // Assert
        response.StatusCode.Should().Be(endpoint.SuccessStatusCode);
    }

    [TestCaseSource(nameof(ChangingEndpoints))]
    public async Task SendAsync_ChangingEndpointWithReadScopeOnly_Returns403AndChangesNothing(AddressEndpoint endpoint)
    {
        // Arrange
        AddressResponse address = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        int count = (await _factory.ListAddressesAsync()).Count;

        // Act
        using HttpResponseMessage response = await SendAsync(endpoint, TestTokens.WithScopes(Read), address.Id);

        // Assert
        await response.ShouldBeForbiddenProblemAsync();
        response.Headers.WwwAuthenticate.Should().BeEmpty();
        (await _factory.FindAddressAsync(address.Id)).Should().Be(address);
        (await _factory.ListAddressesAsync()).Should().HaveCount(count);
    }

    [TestCaseSource(nameof(ChangingEndpoints))]
    public async Task SendAsync_ChangingEndpointWithWriteScopeOnly_Succeeds(AddressEndpoint endpoint)
    {
        // Act
        using HttpResponseMessage response = await SendAsync(endpoint, TestTokens.WithScopes(Write));

        // Assert
        response.StatusCode.Should().Be(endpoint.SuccessStatusCode);
    }

    [TestCaseSource(nameof(ReadingEndpoints))]
    public async Task SendAsync_ReadingEndpointWithWriteScopeOnly_Returns403(AddressEndpoint endpoint)
    {
        // Act
        using HttpResponseMessage response = await SendAsync(endpoint, TestTokens.WithScopes(Write));

        // Assert
        await response.ShouldBeForbiddenProblemAsync();
    }

    [TestCaseSource(nameof(BothScopesCases))]
    public async Task SendAsync_TokenGrantingBothScopes_Succeeds(
        AddressEndpoint endpoint,
        Dictionary<string, object> claims)
    {
        // Act
        using HttpResponseMessage response = await SendAsync(endpoint, TestTokens.Create(claims));

        // Assert
        response.StatusCode.Should().Be(endpoint.SuccessStatusCode);
    }

    [TestCaseSource(nameof(WithoutScopesCases))]
    public async Task SendAsync_TokenGrantingNoScope_Returns403AndChangesNothing(
        AddressEndpoint endpoint,
        Dictionary<string, object> claims)
    {
        // Arrange
        AddressResponse address = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        int count = (await _factory.ListAddressesAsync()).Count;

        // Act
        using HttpResponseMessage response = await SendAsync(endpoint, TestTokens.Create(claims), address.Id);

        // Assert
        await response.ShouldBeForbiddenProblemAsync();
        (await _factory.FindAddressAsync(address.Id)).Should().Be(address);
        (await _factory.ListAddressesAsync()).Should().HaveCount(count);
    }

    [TestCaseSource(nameof(EndpointsWithId))]
    public async Task SendAsync_UnknownIdWithoutRequiredScope_Returns403LikeKnownId(AddressEndpoint endpoint)
    {
        // Arrange
        string otherScope = endpoint.Scope == Read ? Write : Read;
        using HttpClient client = _factory.CreateClientWithToken(TestTokens.WithScopes(otherScope));
        using HttpRequestMessage request = endpoint.CreateRequest(Guid.CreateVersion7());

        // Act
        using HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        await response.ShouldBeForbiddenProblemAsync();
    }

    [TestCaseSource(nameof(EndpointsWithId))]
    public async Task SendAsync_UnknownIdWithRequiredScope_Returns404(AddressEndpoint endpoint)
    {
        // Arrange
        using HttpClient client = _factory.CreateClientWithToken(TestTokens.WithScopes(endpoint.Scope));
        using HttpRequestMessage request = endpoint.CreateRequest(Guid.CreateVersion7());

        // Act
        using HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestCase("POST", "text/plain")]
    [TestCase("PUT", "text/plain")]
    [TestCase("PATCH", "application/json")]
    public async Task SendAsync_WrongContentTypeWithReadScopeOnly_Returns415BeforeScopeIsChecked(
        string method,
        string mediaType)
    {
        // Arrange
        AddressResponse address = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        using HttpClient client = _factory.CreateClientWithToken(TestTokens.WithScopes(Read));
        string url = method == "POST" ? AddressChangeRequests.Path : $"{AddressChangeRequests.Path}/{address.Id}";
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent("{}", Encoding.UTF8, mediaType),
        };
        request.Headers.TryAddWithoutValidation("If-Match", "*");

        // Act
        using HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await _factory.FindAddressAsync(address.Id)).Should().Be(address);
    }

    [TestCase("POST", "text/plain")]
    [TestCase("PATCH", "application/json")]
    public async Task SendAsync_WrongContentTypeWithoutToken_Returns401(string method, string mediaType)
    {
        // Arrange
        AddressResponse address = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        using HttpClient client = _factory.CreateAnonymousClient();
        string url = method == "POST" ? AddressChangeRequests.Path : $"{AddressChangeRequests.Path}/{address.Id}";
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent("{}", Encoding.UTF8, mediaType),
        };

        // Act
        using HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        await response.ShouldBeUnauthorizedProblemAsync();
    }

    private static IEnumerable<TestCaseData> ReadingEndpoints() => ToTestCases(AddressEndpoint.Reading);

    private static IEnumerable<TestCaseData> ChangingEndpoints() => ToTestCases(AddressEndpoint.Changing);

    private static IEnumerable<TestCaseData> EndpointsWithId() => ToTestCases(AddressEndpoint.WithId);

    private static IEnumerable<TestCaseData> BothScopesCases() => CombineWithClaims(_claimsWithBothScopes);

    private static IEnumerable<TestCaseData> WithoutScopesCases() => CombineWithClaims(_claimsWithoutScopes);

    private static IEnumerable<TestCaseData> ToTestCases(IEnumerable<AddressEndpoint> endpoints) =>
        endpoints.Select(endpoint => new TestCaseData(endpoint).SetArgDisplayNames(endpoint.ToString()));

    private static IEnumerable<TestCaseData> CombineWithClaims(
        IEnumerable<(string Name, Dictionary<string, object> Claims)> claimCases) =>
        AddressEndpoint.All.SelectMany(endpoint => claimCases.Select(claimCase =>
            new TestCaseData(endpoint, claimCase.Claims).SetArgDisplayNames(endpoint.ToString(), claimCase.Name)));

    /// <summary>
    /// Sends the valid request of the <paramref name="endpoint"/> with the <paramref name="token"/> for the address
    /// with the <paramref name="id"/>, or for a new address if there is none.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(AddressEndpoint endpoint, string token, Guid? id = null)
    {
        Guid addressId = id ?? (await _factory.SeedAddressAsync(AddressPayloads.ValidRequest)).Id;
        using HttpClient client = _factory.CreateClientWithToken(token);
        using HttpRequestMessage request = endpoint.CreateRequest(addressId);

        return await client.SendAsync(request);
    }
}
