using Microsoft.Extensions.DependencyInjection;

namespace LKCode.AddressManagement.Core.Addresses;

/// <summary>
/// Registers the services of the address business logic.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the validators of the address requests to the <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddCoreAddresses(this IServiceCollection services)
    {
        throw new NotImplementedException();
    }
}
