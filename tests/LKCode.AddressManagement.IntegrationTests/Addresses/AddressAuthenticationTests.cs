using System.Net.Http.Headers;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Service.AuthorizationPolicies;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests that every address endpoint answers a request without a valid access token with <c>401 Unauthorized</c>,
/// Problem Details and a bearer challenge, and leaves the addresses unchanged.
/// </summary>
public class AddressAuthenticationTests
{
    private const string Expired = "expired";
    private const string NotYetValid = "not yet valid";
    private const string WrongSignature = "wrong signature";
    private const string Unsigned = "unsigned";
    private const string WrongIssuer = "wrong issuer";
    private const string WrongAudience = "wrong audience";
    private const string NoJwt = "no JWT";

    private static readonly string[] _invalidTokens =
        [Expired, NotYetValid, WrongSignature, Unsigned, WrongIssuer, WrongAudience, NoJwt];

    private static readonly (string Name, AuthenticationHeaderValue? Header)[] _missingTokens =
    [
        ("no Authorization header", null),
        ("Basic credentials", new AuthenticationHeaderValue("Basic", "ZXJpa2E6c2VjcmV0")),
        ("Bearer without token", new AuthenticationHeaderValue("Bearer")),
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

    [TestCaseSource(nameof(MissingTokenCases))]
    public async Task SendAsync_NoBearerToken_Returns401WithBearerChallengeAndChangesNothing(
        AddressEndpoint endpoint,
        AuthenticationHeaderValue? authorization)
    {
        // Arrange
        AddressResponse address = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        int count = (await _factory.ListAddressesAsync()).Count;
        using HttpClient client = _factory.CreateClientWithAuthorization(authorization);
        using HttpRequestMessage request = endpoint.CreateRequest(address.Id);

        // Act
        using HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        await response.ShouldBeUnauthorizedProblemAsync();
        response.Headers.WwwAuthenticate.Should().ContainSingle().Which.ToString().Should().Be("Bearer");
        (await _factory.FindAddressAsync(address.Id)).Should().Be(address);
        (await _factory.ListAddressesAsync()).Should().HaveCount(count);
    }

    [TestCaseSource(nameof(InvalidTokenCases))]
    public async Task SendAsync_InvalidToken_Returns401WithInvalidTokenErrorAndChangesNothing(
        AddressEndpoint endpoint,
        string invalidToken)
    {
        // Arrange
        AddressResponse address = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        int count = (await _factory.ListAddressesAsync()).Count;
        using HttpClient client = _factory.CreateClientWithToken(CreateInvalidToken(invalidToken));
        using HttpRequestMessage request = endpoint.CreateRequest(address.Id);

        // Act
        using HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        await response.ShouldBeUnauthorizedProblemAsync();
        AuthenticationHeaderValue challenge = response.Headers.WwwAuthenticate.Should().ContainSingle().Which;
        challenge.Scheme.Should().Be("Bearer");
        challenge.Parameter.Should().StartWith("error=\"invalid_token\"");
        (await _factory.FindAddressAsync(address.Id)).Should().Be(address);
        (await _factory.ListAddressesAsync()).Should().HaveCount(count);
    }

    [TestCaseSource(nameof(Endpoints))]
    public async Task SendAsync_ValidTokenWithBothScopes_Succeeds(AddressEndpoint endpoint)
    {
        // Arrange
        AddressResponse address = await _factory.SeedAddressAsync(AddressPayloads.ValidRequest);
        using HttpClient client =
            _factory.CreateClientWithToken(TestTokens.WithScopes(AddressScopes.Read, AddressScopes.Write));
        using HttpRequestMessage request = endpoint.CreateRequest(address.Id);

        // Act
        using HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(endpoint.SuccessStatusCode);
        response.Headers.WwwAuthenticate.Should().BeEmpty();
    }

    [TestCaseSource(nameof(EndpointsWithId))]
    public async Task SendAsync_UnknownIdWithoutToken_Returns401LikeKnownId(AddressEndpoint endpoint)
    {
        // Arrange
        using HttpClient client = _factory.CreateAnonymousClient();
        using HttpRequestMessage request = endpoint.CreateRequest(Guid.CreateVersion7());

        // Act
        using HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        await response.ShouldBeUnauthorizedProblemAsync();
    }

    [TestCase("not-a-guid")]
    [TestCase("123")]
    public async Task GetAsync_IdThatIsNoGuidWithoutToken_Returns401(string id)
    {
        // Arrange
        using HttpClient client = _factory.CreateAnonymousClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync($"{AddressChangeRequests.Path}/{id}");

        // Assert
        await response.ShouldBeUnauthorizedProblemAsync();
    }

    private static IEnumerable<TestCaseData> Endpoints() => AddressEndpoint.All.Select(endpoint =>
        new TestCaseData(endpoint).SetArgDisplayNames(endpoint.ToString()));

    private static IEnumerable<TestCaseData> EndpointsWithId() => AddressEndpoint.WithId.Select(endpoint =>
        new TestCaseData(endpoint).SetArgDisplayNames(endpoint.ToString()));

    private static IEnumerable<TestCaseData> MissingTokenCases() =>
        AddressEndpoint.All.SelectMany(endpoint => _missingTokens.Select(missing =>
            new TestCaseData(endpoint, missing.Header).SetArgDisplayNames(endpoint.ToString(), missing.Name)));

    private static IEnumerable<TestCaseData> InvalidTokenCases() =>
        AddressEndpoint.All.SelectMany(endpoint => _invalidTokens.Select(invalidToken =>
            new TestCaseData(endpoint, invalidToken).SetArgDisplayNames(endpoint.ToString(), invalidToken)));

    /// <summary>
    /// Creates a token with both scopes that is invalid only for the reason <paramref name="invalidToken"/>.
    /// </summary>
    private static string CreateInvalidToken(string invalidToken)
    {
        var claims = new Dictionary<string, object>
        {
            [TestTokens.ScopeClaimType] = $"{AddressScopes.Read} {AddressScopes.Write}",
        };

        return invalidToken switch
        {
            Expired => TestTokens.Create(claims, notBefore: TimeSpan.FromHours(-2), expires: TimeSpan.FromHours(-1)),
            NotYetValid => TestTokens.Create(claims, notBefore: TimeSpan.FromHours(1), expires: TimeSpan.FromHours(2)),
            WrongSignature => TestTokens.Create(claims, signingKey: TestTokens.OtherSigningKey),
            Unsigned => TestTokens.Create(claims, signingKey: null),
            WrongIssuer => TestTokens.Create(claims, issuer: "https://other-issuer.example"),
            WrongAudience => TestTokens.Create(claims, audience: "https://other-api.example"),
            NoJwt => "not-a-jwt",
            _ => throw new ArgumentOutOfRangeException(nameof(invalidToken), invalidToken, "Unknown invalid token."),
        };
    }
}
