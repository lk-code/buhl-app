using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
    /// an empty name causes an <see cref="OptionsValidationException"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddData(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DataOptions>()
            .Bind(configuration.GetSection(DataOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.DatabaseName),
                $"The setting '{DataOptions.SectionName}:{nameof(DataOptions.DatabaseName)}' must not be empty.")
            .ValidateOnStart();

        services.AddDbContext<AddressDbContext>((provider, options) =>
            options.UseInMemoryDatabase(provider.GetRequiredService<IOptions<DataOptions>>().Value.DatabaseName));

        services.TryAddScoped<IAddressRepository, AddressRepository>();

        return services;
    }
}
