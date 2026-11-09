using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LKCode.AddressManagement.IntegrationTests;

/// <summary>
/// Creates access tokens for the integration tests. The hosts of the <see cref="AddressManagementWebApplicationFactory"/>
/// trust the issuer <see cref="Issuer"/>, the audience <see cref="Audience"/> and the key <see cref="SigningKey"/>.
/// </summary>
public static class TestTokens
{
    public const string Issuer = "address-management-tests";

    public const string Audience = "address-management-api";

    /// <summary>
    /// The key of 256 bits that signs the test tokens, Base64-encoded like the keys of <c>dotnet user-jwts</c>.
    /// </summary>
    public const string SigningKey = "h+2NLcer6AGwnTvlCi/Qp+xiRzs22er30QMw+HcoDac=";

    /// <summary>
    /// Another key of 256 bits, which the hosts do not trust.
    /// </summary>
    public const string OtherSigningKey = "ChS5e0El4t6MlzCqUJVFfEs+dQqyAzJVN99V0+spqcI=";

    public const string ScopeClaimType = "scope";

    private static readonly JsonWebTokenHandler _handler = new();

    /// <summary>
    /// Creates a valid token with the <paramref name="scopes"/> in one claim <c>scope</c>, separated by spaces; a
    /// token without scopes has no scope claim.
    /// </summary>
    public static string WithScopes(params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        Dictionary<string, object> claims = [];
        if (scopes.Length > 0)
        {
            claims[ScopeClaimType] = string.Join(' ', scopes);
        }

        return Create(claims);
    }

    /// <summary>
    /// Creates a token with the <paramref name="claims"/>, which is valid unless the other parameters say otherwise.
    /// </summary>
    /// <param name="claims">The claims besides issuer, audience and times; an array becomes a JSON array.</param>
    /// <param name="issuer">The issuer (<c>iss</c>).</param>
    /// <param name="audience">The audience (<c>aud</c>).</param>
    /// <param name="signingKey">The Base64-encoded key to sign with; <see langword="null"/> for an unsigned token.</param>
    /// <param name="notBefore">The start of the validity relative to now; one minute ago if missing.</param>
    /// <param name="expires">The end of the validity relative to now; in one hour if missing.</param>
    public static string Create(
        IReadOnlyDictionary<string, object> claims,
        string issuer = Issuer,
        string audience = Audience,
        string? signingKey = SigningKey,
        TimeSpan? notBefore = null,
        TimeSpan? expires = null)
    {
        ArgumentNullException.ThrowIfNull(claims);

        DateTime now = TimeProvider.System.GetUtcNow().UtcDateTime;
        DateTime validFrom = now + (notBefore ?? TimeSpan.FromMinutes(-1));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = validFrom,
            NotBefore = validFrom,
            Expires = now + (expires ?? TimeSpan.FromHours(1)),
            Claims = claims.ToDictionary(),
            SigningCredentials = signingKey is null
                ? null
                : new SigningCredentials(
                    new SymmetricSecurityKey(Convert.FromBase64String(signingKey)),
                    SecurityAlgorithms.HmacSha256),
        };

        return _handler.CreateToken(descriptor);
    }
}
