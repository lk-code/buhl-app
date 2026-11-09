using AwesomeAssertions;
using LKCode.AddressManagement.Service;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LKCode.AddressManagement.UnitTests.Service;

public class ServiceCollectionExtensionsTests
{
    private const string ExpirationKey = $"{CachingOptions.SectionName}:{nameof(CachingOptions.Expiration)}";

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
