using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LKCode.AddressManagement.Data;

/// <summary>
/// Registers the services of the data project.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the database context and the repositories to the <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="configuration">
    /// The configuration that contains the section <see cref="DataOptions.SectionName"/> with the
    /// <see cref="DataOptions"/>.
    /// </param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// The database name is read when a database context is created. It is validated on start and when it is read;
    /// an empty name causes an <see cref="Microsoft.Extensions.Options.OptionsValidationException"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddData(this IServiceCollection services, IConfiguration configuration)
    {
        throw new NotImplementedException();
    }
}
