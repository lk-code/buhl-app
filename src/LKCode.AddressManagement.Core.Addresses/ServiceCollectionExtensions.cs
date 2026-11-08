using FluentValidation;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// <remarks>The validators are stateless and therefore registered as singletons.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddCoreAddresses(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IValidator<CreateAddressRequest>, CreateAddressRequestValidator>();
        services.TryAddSingleton<IValidator<UpdateAddressRequest>, UpdateAddressRequestValidator>();

        return services;
    }
}
