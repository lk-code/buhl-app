using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Queries;
using LKCode.AddressManagement.Core.Addresses.Results;
using LKCode.AddressManagement.Data;
using LKCode.AddressManagement.Data.Repositories;
using LKCode.AddressManagement.Domain.Addresses;
using LKCode.AddressManagement.Domain.Normalizers;
using LKCode.AddressManagement.UnitTests.Domain.Addresses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LKCode.AddressManagement.UnitTests.Data;

public class ServiceCollectionExtensionsTests
{
    private string _databaseName = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseName = Guid.NewGuid().ToString();
    }

    [Test]
    public void AddData_ValidConfiguration_RegistersAddressRepositoryAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddData(CreateConfiguration(_databaseName));

        // Assert
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IAddressRepository))
            .Which.Should().Match<ServiceDescriptor>(descriptor =>
                descriptor.ImplementationType == typeof(AddressRepository)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Test]
    public void AddData_ValidConfiguration_RegistersAddressDbContextAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddData(CreateConfiguration(_databaseName));

        // Assert
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(AddressDbContext))
            .Which.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Test]
    public void AddData_ValidConfiguration_ResolvesAddressRepository()
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(_databaseName);
        using IServiceScope scope = provider.CreateScope();

        // Act
        IAddressRepository repository = scope.ServiceProvider.GetRequiredService<IAddressRepository>();

        // Assert
        repository.Should().BeOfType<AddressRepository>();
    }

    [Test]
    public void AddData_ValidConfiguration_BindsDatabaseName()
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(_databaseName);

        // Act
        DataOptions options = provider.GetRequiredService<IOptions<DataOptions>>().Value;

        // Assert
        options.DatabaseName.Should().Be(_databaseName);
    }

    [Test]
    public void AddData_ValidConfiguration_PassesStartupValidation()
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(_databaseName);
        IStartupValidator startupValidator = provider.GetRequiredService<IStartupValidator>();

        // Act
        Action act = startupValidator.Validate;

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public async Task AddData_SameDatabaseName_SharesDataAcrossScopesAndProviders()
    {
        // Arrange
        Address address = CreateAddress();
        await using ServiceProvider provider = BuildServiceProvider(_databaseName);
        await AddAsync(provider, address);
        await using ServiceProvider otherProvider = BuildServiceProvider(_databaseName);

        // Act
        IReadOnlyList<Address> fromSameProvider = await ListAsync(provider);
        IReadOnlyList<Address> fromOtherProvider = await ListAsync(otherProvider);

        // Assert
        fromSameProvider.Should().ContainSingle().Which.Id.Should().Be(address.Id);
        fromOtherProvider.Should().ContainSingle().Which.Id.Should().Be(address.Id);
    }

    [Test]
    public async Task AddData_DifferentDatabaseName_SeparatesData()
    {
        // Arrange
        await using ServiceProvider provider = BuildServiceProvider(_databaseName);
        await AddAsync(provider, CreateAddress());
        await using ServiceProvider otherProvider = BuildServiceProvider(Guid.NewGuid().ToString());

        // Act
        IReadOnlyList<Address> result = await ListAsync(otherProvider);

        // Assert
        result.Should().BeEmpty();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void AddData_BlankDatabaseName_ThrowsOptionsValidationExceptionOnResolve(string? databaseName)
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(databaseName);
        using IServiceScope scope = provider.CreateScope();

        // Act
        Action act = () => scope.ServiceProvider.GetRequiredService<AddressDbContext>();

        // Assert
        act.Should().Throw<OptionsValidationException>();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void AddData_BlankDatabaseName_FailsStartupValidation(string? databaseName)
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(databaseName);
        IStartupValidator startupValidator = provider.GetRequiredService<IStartupValidator>();

        // Act
        Action act = startupValidator.Validate;

        // Assert
        act.Should().Throw<OptionsValidationException>();
    }

    [Test]
    public void AddData_MissingSection_FailsStartupValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddData(new ConfigurationBuilder().Build());
        using ServiceProvider provider = services.BuildServiceProvider();
        IStartupValidator startupValidator = provider.GetRequiredService<IStartupValidator>();

        // Act
        Action act = startupValidator.Validate;

        // Assert
        act.Should().Throw<OptionsValidationException>();
    }

    [Test]
    public void AddData_Called_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        IServiceCollection result = services.AddData(CreateConfiguration(_databaseName));

        // Assert
        result.Should().BeSameAs(services);
    }

    [Test]
    public void AddData_CalledTwice_RegistersRepositoryAndDbContextOnce()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfiguration configuration = CreateConfiguration(_databaseName);

        // Act
        services.AddData(configuration);
        services.AddData(configuration);

        // Assert
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IAddressRepository));
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(AddressDbContext));
    }

    [Test]
    public void AddData_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;
        IConfiguration configuration = CreateConfiguration(_databaseName);

        // Act
        Action act = () => services.AddData(configuration);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Test]
    public void AddData_NullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfiguration configuration = null!;

        // Act
        Action act = () => services.AddData(configuration);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("configuration");
    }

    private static IConfiguration CreateConfiguration(string? databaseName) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{DataOptions.SectionName}:{nameof(DataOptions.DatabaseName)}"] = databaseName,
        })
        .Build();

    private static ServiceProvider BuildServiceProvider(string? databaseName)
    {
        var services = new ServiceCollection();
        services.AddData(CreateConfiguration(databaseName));

        var providerOptions = new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true };

        return services.BuildServiceProvider(providerOptions);
    }

    private static Address CreateAddress()
    {
        AddressInput input = AddressInput.Valid;

        return Address.Create(
            input.FirstName,
            input.LastName,
            input.Street,
            input.HouseNumber,
            input.PostalCode,
            input.City,
            input.Country,
            input.Email,
            new TextNormalizer(),
            TimeProvider.System);
    }

    private static async Task AddAsync(IServiceProvider provider, Address address)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IAddressRepository repository = scope.ServiceProvider.GetRequiredService<IAddressRepository>();
        await repository.AddAsync(address, CancellationToken.None);
    }

    private static async Task<IReadOnlyList<Address>> ListAsync(IServiceProvider provider)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IAddressRepository repository = scope.ServiceProvider.GetRequiredService<IAddressRepository>();

        var criteria = new AddressListCriteria(
            null,
            null,
            AddressListConstraints.DefaultSortBy,
            AddressListConstraints.DefaultSortDirection,
            AddressListConstraints.DefaultPage,
            AddressListConstraints.MaxPageSize);
        AddressPage page = await repository.ListAsync(criteria, CancellationToken.None);

        return page.Addresses;
    }
}
