using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using LKCode.AddressManagement.Data;
using LKCode.AddressManagement.Service.AuthorizationPolicies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LKCode.AddressManagement.IntegrationTests;

/// <summary>
/// Tests the configuration of the JWT bearer authentication: the host does not start without issuer and audience,
/// and the settings of <c>appsettings.Development.json</c> accept the tokens of <c>dotnet user-jwts</c>.
/// </summary>
public class AuthenticationConfigurationTests
{
    private const string BearerSection = AddressManagementWebApplicationFactory.BearerSection;
    private const string UserJwtsIssuer = "dotnet-user-jwts";

    private const string MissingIssuerMessage =
        "The setting 'Authentication:Schemes:Bearer:ValidIssuer' or 'Authentication:Schemes:Bearer:ValidIssuers' "
        + "must contain at least one issuer of the access tokens.";

    private const string MissingAudienceMessage =
        "The setting 'Authentication:Schemes:Bearer:ValidAudiences' or 'Authentication:Schemes:Bearer:ValidAudience' "
        + "must contain at least one audience of the access tokens.";

    [Test]
    public async Task CreateClient_ProductionWithoutJwtSettings_FailsToStart()
    {
        // Arrange
        await using var baseFactory = new WebApplicationFactory<Program>();
        await using WebApplicationFactory<Program> factory =
            baseFactory.WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production));

        // Act
        Action act = () => factory.CreateClient().Dispose();

        // Assert
        act.Should().Throw<OptionsValidationException>()
            .Which.Failures.Should().Equal(MissingIssuerMessage, MissingAudienceMessage);
    }

    [TestCase("ValidIssuer", MissingIssuerMessage)]
    [TestCase("ValidAudiences:0", MissingAudienceMessage)]
    public async Task CreateClient_ProductionWithoutIssuerOrAudience_FailsToStart(string key, string message)
    {
        // Arrange
        await using var baseFactory = new AddressManagementWebApplicationFactory();
        await using WebApplicationFactory<Program> factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting($"{BearerSection}:{key}", string.Empty);
        });

        // Act
        Action act = () => factory.CreateClient().Dispose();

        // Assert
        act.Should().Throw<OptionsValidationException>().WithMessage(message);
    }

    [TestCase("http://localhost:5180")]
    [TestCase("https://localhost:7180")]
    public async Task GetAsync_DevelopmentSettingsWithTokenOfUserJwts_Succeeds(string audience)
    {
        // Arrange
        await using var baseFactory = new WebApplicationFactory<Program>();
        await using WebApplicationFactory<Program> factory = WithUserJwtsSigningKey(baseFactory);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateUserJwtsToken(audience));

        // Act
        using HttpResponseMessage response = await client.GetAsync("/api/addresses");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetAsync_DevelopmentSettingsWithTokenOfOtherAudience_Returns401()
    {
        // Arrange
        await using var baseFactory = new WebApplicationFactory<Program>();
        await using WebApplicationFactory<Program> factory = WithUserJwtsSigningKey(baseFactory);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateUserJwtsToken("http://localhost:8080"));

        // Act
        using HttpResponseMessage response = await client.GetAsync("/api/addresses");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Creates a factory with the settings of <c>appsettings.Development.json</c>, its own database and the test key
    /// as signing key of <c>dotnet user-jwts</c>, as the tool stores it in the user secrets.
    /// </summary>
    private static WebApplicationFactory<Program> WithUserJwtsSigningKey(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                $"{DataOptions.SectionName}:{nameof(DataOptions.DatabaseName)}",
                $"AddressManagement-{Guid.NewGuid()}");
            builder.UseSetting($"{BearerSection}:SigningKeys:0:Issuer", UserJwtsIssuer);
            builder.UseSetting($"{BearerSection}:SigningKeys:0:Value", TestTokens.SigningKey);
        });

    /// <summary>
    /// Creates a token like <c>dotnet user-jwts create --scope addresses:read --scope addresses:write</c>.
    /// </summary>
    private static string CreateUserJwtsToken(string audience) => TestTokens.Create(
        new Dictionary<string, object>
        {
            ["sub"] = "erika",
            ["scope"] = new[] { AddressScopes.Read, AddressScopes.Write },
        },
        issuer: UserJwtsIssuer,
        audience: audience);
}
