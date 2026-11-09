using System.Security.Claims;
using AwesomeAssertions;
using LKCode.AddressManagement.Service;
using LKCode.AddressManagement.Service.AuthorizationPolicies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LKCode.AddressManagement.UnitTests.Service;

public class ServiceCollectionExtensionsTests
{
    private const string ExpirationKey = $"{CachingOptions.SectionName}:{nameof(CachingOptions.Expiration)}";
    private const string BearerSection = "Authentication:Schemes:Bearer";
    private const string Issuer = "https://issuer.example";
    private const string Audience = "https://api.example";
    private const string OtherAudience = "https://other-api.example";

    // The bytes 0 to 31, i.e. a key of 256 bits.
    private const string SigningKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    private const string MissingIssuerMessage =
        "The setting 'Authentication:Schemes:Bearer:ValidIssuer' or 'Authentication:Schemes:Bearer:ValidIssuers' "
        + "must contain at least one issuer of the access tokens.";

    private const string MissingAudienceMessage =
        "The setting 'Authentication:Schemes:Bearer:ValidAudiences' or 'Authentication:Schemes:Bearer:ValidAudience' "
        + "must contain at least one audience of the access tokens.";

    [Test]
    public void AddAddressCaching_MissingSection_UsesExpirationOfFiveMinutes()
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(new ConfigurationBuilder().Build());

        // Act
        CachingOptions options = provider.GetRequiredService<IOptions<CachingOptions>>().Value;

        // Assert
        options.Expiration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Test]
    public void AddAddressCaching_MissingSection_SetsDefaultExpirationOfOutputCacheToFiveMinutes()
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(new ConfigurationBuilder().Build());

        // Act
        OutputCacheOptions options = provider.GetRequiredService<IOptions<OutputCacheOptions>>().Value;

        // Assert
        options.DefaultExpirationTimeSpan.Should().Be(TimeSpan.FromMinutes(5));
    }

    [TestCase("00:00:42", 42)]
    [TestCase("01:30:00", 5400)]
    public void AddAddressCaching_ConfiguredExpiration_BindsExpiration(string expiration, int expectedSeconds)
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(expiration);

        // Act
        CachingOptions options = provider.GetRequiredService<IOptions<CachingOptions>>().Value;

        // Assert
        options.Expiration.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [TestCase("00:00:42", 42)]
    [TestCase("01:30:00", 5400)]
    public void AddAddressCaching_ConfiguredExpiration_IsDefaultExpirationOfOutputCache(
        string expiration,
        int expectedSeconds)
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(expiration);

        // Act
        OutputCacheOptions options = provider.GetRequiredService<IOptions<OutputCacheOptions>>().Value;

        // Assert
        options.DefaultExpirationTimeSpan.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [TestCase("00:00:00.001")]
    [TestCase("00:00:01")]
    [TestCase("00:05:00")]
    [TestCase("23:59:59")]
    [TestCase("1.00:00:00")]
    public void AddAddressCaching_ValidExpiration_PassesStartupValidation(string expiration)
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(expiration);
        IStartupValidator startupValidator = provider.GetRequiredService<IStartupValidator>();

        // Act
        Action act = startupValidator.Validate;

        // Assert
        act.Should().NotThrow();
    }

    [TestCase("00:00:00")]
    [TestCase("-00:00:00.001")]
    [TestCase("-00:00:01")]
    [TestCase("-1.00:00:00")]
    [TestCase("1.00:00:00.001")]
    [TestCase("1.00:00:01")]
    [TestCase("10675199.02:48:05.4775807")]
    public void AddAddressCaching_InvalidExpiration_FailsStartupValidation(string expiration)
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(expiration);
        IStartupValidator startupValidator = provider.GetRequiredService<IStartupValidator>();

        // Act
        Action act = startupValidator.Validate;

        // Assert
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("The setting 'Caching:Expiration' must be greater than 00:00:00 and at most 1.00:00:00.");
    }

    [TestCase("00:00:00")]
    [TestCase("-00:00:01")]
    [TestCase("1.00:00:01")]
    public void AddAddressCaching_InvalidExpiration_ThrowsOptionsValidationExceptionOnResolveOfOutputCacheOptions(
        string expiration)
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(expiration);

        // Act
        Action act = () => _ = provider.GetRequiredService<IOptions<OutputCacheOptions>>().Value;

        // Assert
        act.Should().Throw<OptionsValidationException>();
    }

    [Test]
    public void AddAddressCaching_ValidConfiguration_ResolvesOutputCacheStoreAsSingleton()
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider("00:05:00");

        // Act
        IOutputCacheStore store = provider.GetRequiredService<IOutputCacheStore>();

        // Assert
        store.Should().BeSameAs(provider.GetRequiredService<IOutputCacheStore>());
    }

    [Test]
    public void AddAddressCaching_Called_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        IServiceCollection result = services.AddAddressCaching(CreateConfiguration("00:05:00"));

        // Assert
        result.Should().BeSameAs(services);
    }

    [Test]
    public void AddAddressCaching_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;
        IConfiguration configuration = CreateConfiguration("00:05:00");

        // Act
        Action act = () => services.AddAddressCaching(configuration);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Test]
    public void AddAddressCaching_NullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfiguration configuration = null!;

        // Act
        Action act = () => services.AddAddressCaching(configuration);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("configuration");
    }

    [Test]
    public async Task AddJwtBearerAuthentication_Called_RegistersJwtBearerAsDefaultScheme()
    {
        // Arrange
        using ServiceProvider provider = BuildAuthenticationServiceProvider(CreateBearerSettings());
        IAuthenticationSchemeProvider schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        // Act
        AuthenticationScheme? authenticateScheme = await schemeProvider.GetDefaultAuthenticateSchemeAsync();
        AuthenticationScheme? challengeScheme = await schemeProvider.GetDefaultChallengeSchemeAsync();
        AuthenticationScheme? forbidScheme = await schemeProvider.GetDefaultForbidSchemeAsync();

        // Assert
        authenticateScheme.Should().NotBeNull();
        authenticateScheme.Name.Should().Be("Bearer");
        authenticateScheme.HandlerType.Should().Be<JwtBearerHandler>();
        challengeScheme?.Name.Should().Be("Bearer");
        forbidScheme?.Name.Should().Be("Bearer");
    }

    [TestCase(null)]
    [TestCase("true")]
    [TestCase("false")]
    public void AddJwtBearerAuthentication_MapInboundClaimsSetting_DisablesMappingOfInboundClaims(string? setting)
    {
        // Arrange
        Dictionary<string, string?> settings = CreateBearerSettings();
        settings[$"{BearerSection}:MapInboundClaims"] = setting;
        using ServiceProvider provider = BuildAuthenticationServiceProvider(settings);

        // Act
        JwtBearerOptions options = GetJwtBearerOptions(provider);

        // Assert
        options.MapInboundClaims.Should().BeFalse();
    }

    [Test]
    public void AddJwtBearerAuthentication_BearerSection_BindsIssuerAudiencesAndSigningKeyOfIssuer()
    {
        // Arrange
        Dictionary<string, string?> settings = CreateBearerSettings();
        settings[$"{BearerSection}:ValidAudiences:1"] = OtherAudience;
        settings[$"{BearerSection}:SigningKeys:1:Issuer"] = "https://other-issuer.example";
        settings[$"{BearerSection}:SigningKeys:1:Value"] = Convert.ToBase64String(new byte[32]);
        using ServiceProvider provider = BuildAuthenticationServiceProvider(settings);

        // Act
        TokenValidationParameters parameters = GetJwtBearerOptions(provider).TokenValidationParameters;

        // Assert
        parameters.ValidateIssuer.Should().BeTrue();
        parameters.ValidIssuers.Prepend(parameters.ValidIssuer).Should().Equal(Issuer);
        parameters.ValidateAudience.Should().BeTrue();
        parameters.ValidAudiences.Should().Equal(Audience, OtherAudience);
        parameters.IssuerSigningKeys.Should().ContainSingle()
            .Which.Should().BeOfType<SymmetricSecurityKey>()
            .Which.Key.Should().Equal(Convert.FromBase64String(SigningKey));
    }

    [TestCase("ValidIssuer", "ValidAudiences:0")]
    [TestCase("ValidIssuers:0", "ValidAudience")]
    [TestCase("ValidIssuers:0", "ValidAudiences:0")]
    [TestCase("ValidIssuer", "ValidAudience")]
    public void AddJwtBearerAuthentication_IssuerAndAudience_PassesStartupValidation(
        string issuerKey,
        string audienceKey)
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            [$"{BearerSection}:{issuerKey}"] = Issuer,
            [$"{BearerSection}:{audienceKey}"] = Audience,
        };
        using ServiceProvider provider = BuildAuthenticationServiceProvider(settings);
        IStartupValidator startupValidator = provider.GetRequiredService<IStartupValidator>();

        // Act
        Action act = startupValidator.Validate;

        // Assert
        act.Should().NotThrow();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void AddJwtBearerAuthentication_MissingIssuer_FailsStartupValidation(string? issuer)
    {
        // Arrange
        Dictionary<string, string?> settings = CreateBearerSettings();
        settings[$"{BearerSection}:ValidIssuer"] = issuer;
        using ServiceProvider provider = BuildAuthenticationServiceProvider(settings);
        IStartupValidator startupValidator = provider.GetRequiredService<IStartupValidator>();

        // Act
        Action act = startupValidator.Validate;

        // Assert
        act.Should().Throw<OptionsValidationException>().WithMessage(MissingIssuerMessage);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void AddJwtBearerAuthentication_MissingAudience_FailsStartupValidation(string? audience)
    {
        // Arrange
        Dictionary<string, string?> settings = CreateBearerSettings();
        settings[$"{BearerSection}:ValidAudiences:0"] = audience;
        using ServiceProvider provider = BuildAuthenticationServiceProvider(settings);
        IStartupValidator startupValidator = provider.GetRequiredService<IStartupValidator>();

        // Act
        Action act = startupValidator.Validate;

        // Assert
        act.Should().Throw<OptionsValidationException>().WithMessage(MissingAudienceMessage);
    }

    [Test]
    public void AddJwtBearerAuthentication_MissingSection_FailsStartupValidationForIssuerAndAudience()
    {
        // Arrange
        using ServiceProvider provider = BuildAuthenticationServiceProvider([]);
        IStartupValidator startupValidator = provider.GetRequiredService<IStartupValidator>();

        // Act
        Action act = startupValidator.Validate;

        // Assert
        act.Should().Throw<OptionsValidationException>()
            .Which.Failures.Should().Equal(MissingIssuerMessage, MissingAudienceMessage);
    }

    [Test]
    public void AddJwtBearerAuthentication_MissingIssuer_ThrowsOptionsValidationExceptionOnResolveOfOptions()
    {
        // Arrange
        using ServiceProvider provider = BuildAuthenticationServiceProvider([]);

        // Act
        Action act = () => GetJwtBearerOptions(provider);

        // Assert
        act.Should().Throw<OptionsValidationException>();
    }

    [Test]
    public void AddJwtBearerAuthentication_Called_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        IServiceCollection result = services.AddJwtBearerAuthentication();

        // Assert
        result.Should().BeSameAs(services);
    }

    [Test]
    public void AddJwtBearerAuthentication_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        Action act = () => services.AddJwtBearerAuthentication();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [TestCase("AddressesRead", "addresses:read")]
    [TestCase("AddressesWrite", "addresses:write")]
    public async Task AddAddressAuthorization_AddressPolicy_RequiresAuthenticatedUserWithScope(
        string policyName,
        string scope)
    {
        // Arrange
        using ServiceProvider provider = BuildAuthorizationServiceProvider();
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy? policy = await policyProvider.GetPolicyAsync(policyName);

        // Assert
        policy.Should().NotBeNull();
        policy.AuthenticationSchemes.Should().BeEmpty();
        policy.Requirements.Should().HaveCount(2);
        policy.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().Should().ContainSingle();
        policy.Requirements.OfType<ScopeRequirement>().Should().ContainSingle().Which.Scope.Should().Be(scope);
    }

    [Test]
    public void AddAddressAuthorization_PolicyNames_AreAddressesReadAndAddressesWrite()
    {
        // Act
        string[] names = [AddressAuthorizationPolicyNames.Read, AddressAuthorizationPolicyNames.Write];

        // Assert
        names.Should().Equal("AddressesRead", "AddressesWrite");
    }

    [Test]
    public async Task AddAddressAuthorization_FallbackPolicy_RequiresAuthenticatedUserOnly()
    {
        // Arrange
        using ServiceProvider provider = BuildAuthorizationServiceProvider();
        IAuthorizationPolicyProvider policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy? policy = await policyProvider.GetFallbackPolicyAsync();

        // Assert
        policy.Should().NotBeNull();
        policy.AuthenticationSchemes.Should().BeEmpty();
        policy.Requirements.Should().ContainSingle().Which.Should().BeOfType<DenyAnonymousAuthorizationRequirement>();
    }

    [TestCase("AddressesRead", "addresses:read", true)]
    [TestCase("AddressesRead", "addresses:read addresses:write", true)]
    [TestCase("AddressesRead", "addresses:write", false)]
    [TestCase("AddressesRead", "", false)]
    [TestCase("AddressesWrite", "addresses:write", true)]
    [TestCase("AddressesWrite", "addresses:write addresses:read", true)]
    [TestCase("AddressesWrite", "addresses:read", false)]
    [TestCase("AddressesWrite", "", false)]
    public async Task AddAddressAuthorization_AuthenticatedUser_IsAuthorizedOnlyWithScopeOfPolicy(
        string policyName,
        string scopes,
        bool expected)
    {
        // Arrange
        using ServiceProvider provider = BuildAuthorizationServiceProvider();
        IAuthorizationService authorizationService = provider.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("scope", scopes)], "Bearer"));

        // Act
        AuthorizationResult result = await authorizationService.AuthorizeAsync(user, policyName);

        // Assert
        result.Succeeded.Should().Be(expected);
    }

    [TestCase("AddressesRead")]
    [TestCase("AddressesWrite")]
    public async Task AddAddressAuthorization_UnauthenticatedUserWithScopes_IsNotAuthorized(string policyName)
    {
        // Arrange
        using ServiceProvider provider = BuildAuthorizationServiceProvider();
        IAuthorizationService authorizationService = provider.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("scope", "addresses:read addresses:write")]));

        // Act
        AuthorizationResult result = await authorizationService.AuthorizeAsync(user, policyName);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Test]
    public void AddAddressAuthorization_CalledTwice_RegistersScopeAuthorizationHandlerOnce()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAddressAuthorization();
        services.AddAddressAuthorization();
        using ServiceProvider provider = services.BuildServiceProvider();

        // Act
        IEnumerable<IAuthorizationHandler> handlers = provider.GetServices<IAuthorizationHandler>();

        // Assert
        handlers.OfType<ScopeAuthorizationHandler>().Should().ContainSingle();
    }

    [Test]
    public void AddAddressAuthorization_Called_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        IServiceCollection result = services.AddAddressAuthorization();

        // Assert
        result.Should().BeSameAs(services);
    }

    [Test]
    public void AddAddressAuthorization_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        Action act = () => services.AddAddressAuthorization();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    private static Dictionary<string, string?> CreateBearerSettings() => new()
    {
        [$"{BearerSection}:ValidIssuer"] = Issuer,
        [$"{BearerSection}:ValidAudiences:0"] = Audience,
        [$"{BearerSection}:SigningKeys:0:Issuer"] = Issuer,
        [$"{BearerSection}:SigningKeys:0:Value"] = SigningKey,
    };

    private static ServiceProvider BuildAuthenticationServiceProvider(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddLogging();
        services.AddJwtBearerAuthentication();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static ServiceProvider BuildAuthorizationServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAddressAuthorization();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static JwtBearerOptions GetJwtBearerOptions(ServiceProvider provider) =>
        provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

    private static IConfiguration CreateConfiguration(string expiration) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [ExpirationKey] = expiration })
        .Build();

    private static ServiceProvider BuildServiceProvider(string expiration) =>
        BuildServiceProvider(CreateConfiguration(expiration));

    private static ServiceProvider BuildServiceProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddAddressCaching(configuration);

        var providerOptions = new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true };

        return services.BuildServiceProvider(providerOptions);
    }
}
