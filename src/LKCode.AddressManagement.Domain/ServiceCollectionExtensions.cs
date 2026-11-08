using Microsoft.Extensions.DependencyInjection;

namespace LKCode.AddressManagement.Domain;

/// <summary>
/// Registers the services of the domain project.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the domain services to the <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddDomain(this IServiceCollection services) =>
        throw new NotImplementedException();
}
