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
}
