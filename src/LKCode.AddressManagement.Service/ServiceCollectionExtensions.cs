using Microsoft.AspNetCore.OutputCaching;

namespace LKCode.AddressManagement.Service;

/// <summary>
/// Registers the services of the REST host.
/// </summary>
public static class ServiceCollectionExtensions
{
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
    /// The <see cref="CachingOptions.Expiration"/> is the default expiration of the
    /// <see cref="OutputCacheOptions"/>. It is validated on start and when it is read; a value that is not greater
    /// than zero or greater than <see cref="CachingOptions.MaxExpiration"/> causes an
    /// <see cref="Microsoft.Extensions.Options.OptionsValidationException"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddAddressCaching(this IServiceCollection services, IConfiguration configuration)
    {
        throw new NotImplementedException();
    }
}
