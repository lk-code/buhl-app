using AwesomeAssertions;
using FluentValidation;
using LKCode.AddressManagement.Core.Addresses;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Validators;
using LKCode.AddressManagement.Domain.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses;

public class ServiceCollectionExtensionsTests
{
    [Test]
    public void AddCoreAddresses_Called_RegistersCreateAddressRequestValidatorAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCoreAddresses();

        // Assert
        services.Should().ContainSingle(descriptor =>
                descriptor.ServiceType == typeof(IValidator<CreateAddressRequest>))
            .Which.Should().Match<ServiceDescriptor>(descriptor =>
                descriptor.ImplementationType == typeof(CreateAddressRequestValidator)
                && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Test]
    public void AddCoreAddresses_Called_RegistersUpdateAddressRequestValidatorAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCoreAddresses();

        // Assert
        services.Should().ContainSingle(descriptor =>
                descriptor.ServiceType == typeof(IValidator<UpdateAddressRequest>))
            .Which.Should().Match<ServiceDescriptor>(descriptor =>
                descriptor.ImplementationType == typeof(UpdateAddressRequestValidator)
                && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Test]
    public void AddCoreAddresses_Called_RegistersAddressListQueryValidatorAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCoreAddresses();

        // Assert
        services.Should().ContainSingle(descriptor =>
                descriptor.ServiceType == typeof(IValidator<AddressListQuery>))
            .Which.Should().Match<ServiceDescriptor>(descriptor =>
                descriptor.ImplementationType == typeof(AddressListQueryValidator)
                && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Test]
    public void AddCoreAddresses_Called_RegistersAddressServiceAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCoreAddresses();

        // Assert
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IAddressService))
            .Which.Should().Match<ServiceDescriptor>(descriptor =>
                descriptor.ImplementationType == typeof(AddressService)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Test]
    public void AddCoreAddresses_Called_ResolvesAddressService()
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        // Act
        IAddressService addressService = scope.ServiceProvider.GetRequiredService<IAddressService>();

        // Assert
        addressService.Should().BeOfType<AddressService>();
    }

    [Test]
    public void AddCoreAddresses_Called_ResolvesValidators()
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider();

        // Act
        IValidator<CreateAddressRequest> createValidator =
            provider.GetRequiredService<IValidator<CreateAddressRequest>>();
        IValidator<UpdateAddressRequest> updateValidator =
            provider.GetRequiredService<IValidator<UpdateAddressRequest>>();
        IValidator<AddressListQuery> listQueryValidator =
            provider.GetRequiredService<IValidator<AddressListQuery>>();

        // Assert
        createValidator.Should().BeOfType<CreateAddressRequestValidator>();
        updateValidator.Should().BeOfType<UpdateAddressRequestValidator>();
        listQueryValidator.Should().BeOfType<AddressListQueryValidator>();
    }

    [Test]
    public void AddCoreAddresses_Called_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        IServiceCollection result = services.AddCoreAddresses();

        // Assert
        result.Should().BeSameAs(services);
    }

    [Test]
    public void AddCoreAddresses_CalledTwice_RegistersValidatorsOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCoreAddresses();
        services.AddCoreAddresses();

        // Assert
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IValidator<CreateAddressRequest>));
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IValidator<UpdateAddressRequest>));
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IValidator<AddressListQuery>));
    }

    [Test]
    public void AddCoreAddresses_CalledTwice_RegistersAddressServiceOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCoreAddresses();
        services.AddCoreAddresses();

        // Assert
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IAddressService));
    }

    [Test]
    public void AddCoreAddresses_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        Action act = () => services.AddCoreAddresses();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    /// <summary>
    /// Builds a provider with the services of <c>AddCoreAddresses()</c> and substitutes for their dependencies, which
    /// the host, <c>AddDomain()</c> and <c>AddData()</c> register.
    /// </summary>
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddCoreAddresses();
        services.AddScoped(_ => Substitute.For<IAddressRepository>());
        services.AddSingleton(Substitute.For<ITextNormalizer>());
        services.AddSingleton(TimeProvider.System);
        services.AddLogging();

        var providerOptions = new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true };

        return services.BuildServiceProvider(providerOptions);
    }
}
