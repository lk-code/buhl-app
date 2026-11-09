using System.Net.Http.Headers;
using LKCode.AddressManagement.Data;
using LKCode.AddressManagement.Service.AuthorizationPolicies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LKCode.AddressManagement.IntegrationTests;

/// <summary>
/// Hosts the service in memory for integration tests. Every instance uses its own in-memory database, so tests
/// and fixtures that create their own factory do not affect each other.
/// </summary>
/// <remarks>
/// The host trusts the tokens of <see cref="TestTokens"/>; its configuration of the JWT bearer authentication has the
/// structure that <c>dotnet user-jwts</c> writes. Every client created by the factory or a factory derived from it
/// sends a valid token with the scopes <see cref="AddressScopes.Read"/> and <see cref="AddressScopes.Write"/>, unless
/// a test replaces it, e.g. with <see cref="WebApplicationFactoryExtensions.CreateAnonymousClient"/>.
/// </remarks>
public sealed class AddressManagementWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string BearerSection = "Authentication:Schemes:Bearer";

    /// <summary>
    /// Gets the unique name of the in-memory database of this instance.
    /// </summary>
    public string DatabaseName { get; } = $"AddressManagement-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting($"{DataOptions.SectionName}:{nameof(DataOptions.DatabaseName)}", DatabaseName);
        builder.UseSetting($"{BearerSection}:ValidIssuer", TestTokens.Issuer);
        builder.UseSetting($"{BearerSection}:ValidAudiences:0", TestTokens.Audience);
        builder.UseSetting($"{BearerSection}:SigningKeys:0:Issuer", TestTokens.Issuer);
        builder.UseSetting($"{BearerSection}:SigningKeys:0:Value", TestTokens.SigningKey);
    }

    protected override void ConfigureClient(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokens.WithScopes(AddressScopes.Read, AddressScopes.Write));
    }
}
