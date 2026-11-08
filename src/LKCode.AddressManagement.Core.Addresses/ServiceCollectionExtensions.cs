using FluentValidation;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Validators;
using LKCode.AddressManagement.Domain.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LKCode.AddressManagement.Core.Addresses;

/// <summary>
/// Registers the services of the address business logic.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the validators of the address requests and of the address list query and the
    /// <see cref="IAddressService"/> to the <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// The validators are stateless and therefore registered as singletons. The address service is scoped like the
    /// <see cref="IAddressRepository"/> it uses. The repository, the <see cref="ITextNormalizer"/>, the
    /// <see cref="TimeProvider"/> and the logging must be registered separately.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddCoreAddresses(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IValidator<CreateAddressRequest>, CreateAddressRequestValidator>();
        services.TryAddSingleton<IValidator<UpdateAddressRequest>, UpdateAddressRequestValidator>();
        services.TryAddSingleton<IValidator<AddressListQuery>, AddressListQueryValidator>();
        services.TryAddScoped<IAddressService, AddressService>();

        return services;
    }
}
