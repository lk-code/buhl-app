using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Mappings;
using LKCode.AddressManagement.Data;
using LKCode.AddressManagement.Domain.Addresses;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Reads and stores addresses directly through the services of a host, bypassing HTTP.
/// </summary>
public static class AddressManagementWebApplicationFactoryExtensions
{
    public static async Task<AddressResponse> SeedAddressAsync(
        this WebApplicationFactory<Program> factory,
        CreateAddressRequest request)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IAddressService addressService = scope.ServiceProvider.GetRequiredService<IAddressService>();

        return await addressService.CreateAsync(request, CancellationToken.None);
    }

    public static async Task<AddressResponse?> FindAddressAsync(
        this WebApplicationFactory<Program> factory,
        Guid id)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IAddressService addressService = scope.ServiceProvider.GetRequiredService<IAddressService>();

        return await addressService.GetByIdAsync(id, CancellationToken.None);
    }

    /// <summary>
    /// Reads all stored addresses that are not deleted directly from the database, without paging.
    /// </summary>
    public static async Task<IReadOnlyList<AddressResponse>> ListAddressesAsync(
        this WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AddressDbContext dbContext = scope.ServiceProvider.GetRequiredService<AddressDbContext>();
        List<Address> addresses = await dbContext.Addresses.AsNoTracking().ToListAsync();

        return [.. addresses.Select(address => address.ToResponse())];
    }

    /// <summary>
    /// Reads the stored address with the <paramref name="id"/> directly from the database, including a deleted one.
    /// </summary>
    public static async Task<Address?> FindStoredAddressAsync(
        this WebApplicationFactory<Program> factory,
        Guid id)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        AddressDbContext dbContext = scope.ServiceProvider.GetRequiredService<AddressDbContext>();

        return await dbContext.Addresses
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(address => address.Id == id);
    }
}
