using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Reads and stores addresses directly through the <see cref="IAddressService"/> of a host, bypassing HTTP.
/// </summary>
public static class AddressManagementWebApplicationFactoryExtensions
{
    public static async Task<AddressResponse> SeedAddressAsync(
        this AddressManagementWebApplicationFactory factory,
        CreateAddressRequest request)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IAddressService addressService = scope.ServiceProvider.GetRequiredService<IAddressService>();

        return await addressService.CreateAsync(request, CancellationToken.None);
    }

    public static async Task<AddressResponse?> FindAddressAsync(
        this AddressManagementWebApplicationFactory factory,
        Guid id)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IAddressService addressService = scope.ServiceProvider.GetRequiredService<IAddressService>();

        return await addressService.GetByIdAsync(id, CancellationToken.None);
    }

    public static async Task<IReadOnlyList<AddressResponse>> ListAddressesAsync(
        this AddressManagementWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IAddressService addressService = scope.ServiceProvider.GetRequiredService<IAddressService>();

        return await addressService.GetListAsync(new AddressListQuery(null, null), CancellationToken.None);
    }
}
