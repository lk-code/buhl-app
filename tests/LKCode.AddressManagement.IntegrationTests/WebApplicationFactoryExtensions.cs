using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Data.Repositories;
using LKCode.AddressManagement.IntegrationTests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OutputCaching;
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

    /// <summary>
    /// Creates a factory whose output cache store records every stored response; the store is the
    /// <see cref="RecordingOutputCacheStore"/> returned by <see cref="GetRecordingOutputCacheStore"/>.
    /// </summary>
    public static WebApplicationFactory<Program> WithRecordingOutputCacheStore(
        this WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            ServiceDescriptor original = services.Single(descriptor =>
                descriptor.ServiceType == typeof(IOutputCacheStore));
            Func<IServiceProvider, object> createOriginal = original.ImplementationFactory
                ?? throw new InvalidOperationException("The output cache store is not registered with a factory.");
            services.Remove(original);
            services.AddSingleton<IOutputCacheStore>(provider =>
                new RecordingOutputCacheStore((IOutputCacheStore)createOriginal(provider)));
        }));
    }

    /// <summary>
    /// Gets the store of a factory created by <see cref="WithRecordingOutputCacheStore"/>.
    /// </summary>
    public static RecordingOutputCacheStore GetRecordingOutputCacheStore(this WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return (RecordingOutputCacheStore)factory.Services.GetRequiredService<IOutputCacheStore>();
    }

    /// <summary>
    /// Creates a factory whose host uses the <paramref name="addressService"/> instead of the real service.
    /// </summary>
    public static WebApplicationFactory<Program> WithAddressService(
        this WebApplicationFactory<Program> factory,
        IAddressService addressService)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped(_ => addressService)));
    }

    /// <summary>
    /// Creates a factory whose host has the configuration value <paramref name="value"/> under the
    /// <paramref name="key"/>.
    /// </summary>
    public static WebApplicationFactory<Program> WithSetting(
        this WebApplicationFactory<Program> factory,
        string key,
        string value)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.WithWebHostBuilder(builder => builder.UseSetting(key, value));
    }
}
