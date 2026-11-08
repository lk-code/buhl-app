using LKCode.AddressManagement.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LKCode.AddressManagement.IntegrationTests;

/// <summary>
/// Hosts the service in memory for integration tests. Every instance uses its own in-memory database, so tests
/// and fixtures that create their own factory do not affect each other.
/// </summary>
public sealed class AddressManagementWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Gets the unique name of the in-memory database of this instance.
    /// </summary>
    public string DatabaseName { get; } = $"AddressManagement-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting($"{DataOptions.SectionName}:{nameof(DataOptions.DatabaseName)}", DatabaseName);
    }
}
