using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Data.Repositories;
using LKCode.AddressManagement.IntegrationTests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LKCode.AddressManagement.IntegrationTests;

public static class WebApplicationFactoryExtensions
{
    /// <summary>
    /// Creates a factory whose host additionally contains the <see cref="ThrowingController"/>. The controller is
    /// not part of the regular hosts, so it does not appear in their OpenAPI document.
    /// </summary>
    public static WebApplicationFactory<Program> WithThrowingController(this WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ThrowingController).Assembly)));
    }

    /// <summary>
    /// Creates a factory whose host holds every change and deletion of an address back until all participants of
    /// the <paramref name="rendezvous"/> want to save; see <see cref="RendezvousAddressRepository"/>.
    /// </summary>
    public static WebApplicationFactory<Program> WithRendezvousBeforeSaving(
        this WebApplicationFactory<Program> factory,
        Rendezvous rendezvous)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<AddressRepository>();
            services.AddScoped<IAddressRepository>(provider => new RendezvousAddressRepository(
                provider.GetRequiredService<AddressRepository>(),
                rendezvous));
        }));
    }
}
