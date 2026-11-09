using System.Globalization;
using LKCode.AddressManagement.Service.OutputCachePolicies;
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
}
