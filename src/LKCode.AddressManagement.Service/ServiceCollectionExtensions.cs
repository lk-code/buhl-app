using System.Globalization;
using LKCode.AddressManagement.Service.AuthorizationPolicies;
using LKCode.AddressManagement.Service.OutputCachePolicies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace LKCode.AddressManagement.Service;

/// <summary>
/// Registers the services of the REST host.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly string _invalidExpirationMessage = string.Create(
        CultureInfo.InvariantCulture,
        $"The setting '{CachingOptions.SectionName}:{nameof(CachingOptions.Expiration)}' must be greater than "
        + $"{TimeSpan.Zero:c} and at most {CachingOptions.MaxExpiration:c}.");

    /// <summary>
    /// Adds the server-side output cache for addresses to the <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="configuration">
    /// The configuration that may contain the section <see cref="CachingOptions.SectionName"/> with the
    /// <see cref="CachingOptions"/>.
    /// </param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Registers the policies <see cref="AddressOutputCachePolicyNames.ById"/> and
    /// <see cref="AddressOutputCachePolicyNames.List"/> for the endpoints. The
    /// <see cref="CachingOptions.Expiration"/> is the default expiration of the <see cref="OutputCacheOptions"/>.
    /// It is validated on start and when it is read; a value that is not greater than zero or greater than
    /// <see cref="CachingOptions.MaxExpiration"/> causes an <see cref="OptionsValidationException"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddAddressCaching(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<CachingOptions>()
            .Bind(configuration.GetSection(CachingOptions.SectionName))
            .Validate(
                options => options.Expiration > TimeSpan.Zero && options.Expiration <= CachingOptions.MaxExpiration,
                _invalidExpirationMessage)
            .ValidateOnStart();

        services.AddOutputCache();
        services.AddOptions<OutputCacheOptions>()
            .Configure<IOptions<CachingOptions>>((outputCacheOptions, cachingOptions) =>
            {
                outputCacheOptions.DefaultExpirationTimeSpan = cachingOptions.Value.Expiration;

                // Both policies extend the default policy: only GET and HEAD requests without authorization and
                // only responses with 200 OK and without cookies are cached.
                outputCacheOptions.AddPolicy(
                    AddressOutputCachePolicyNames.ById,
                    policy => policy
                        .AddPolicy<ConditionalRequestBypassPolicy>()
                        .AddPolicy<AddressIdTagPolicy>());
                outputCacheOptions.AddPolicy(
                    AddressOutputCachePolicyNames.List,
                    policy => policy
                        .AddPolicy<ConditionalRequestBypassPolicy>()
                        .SetVaryByQuery("*")
                        .Tag(AddressCacheTags.List));
            });

        return services;
    }

    /// <summary>
    /// Adds the authentication with JWT bearer tokens to the <paramref name="services"/>; the scheme
    /// <see cref="JwtBearerDefaults.AuthenticationScheme"/> is the default scheme.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// The options of the scheme come from the configuration section <c>Authentication:Schemes:Bearer</c>, the
    /// section that <c>dotnet user-jwts</c> writes: e.g. <c>ValidIssuer</c>, <c>ValidAudiences</c>,
    /// <c>SigningKeys</c> or <c>Authority</c>. The claims keep the types of the token (no mapping of inbound claims),
    /// so that the scope claims <c>scope</c> and <c>scp</c> can be read. At least one valid issuer and one valid
    /// audience are required; without them, the issuer or the audience of a token would not be validated. They are
    /// validated on start and when the options are read; a missing value causes an
    /// <see cref="OptionsValidationException"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddJwtBearerAuthentication(this IServiceCollection services)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Adds the authorization policies of the address endpoints to the <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// The policies <see cref="AddressAuthorizationPolicyNames.Read"/> and
    /// <see cref="AddressAuthorizationPolicyNames.Write"/> require an authenticated user with the scope
    /// <see cref="AddressScopes.Read"/> or <see cref="AddressScopes.Write"/>, evaluated by the
    /// <see cref="ScopeAuthorizationHandler"/>. Every endpoint without a policy of its own requires an authenticated
    /// user (fallback policy), unless it allows anonymous access.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddAddressAuthorization(this IServiceCollection services)
    {
        throw new NotImplementedException();
    }
}
