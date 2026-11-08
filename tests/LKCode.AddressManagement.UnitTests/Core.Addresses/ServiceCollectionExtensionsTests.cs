using AwesomeAssertions;
using FluentValidation;
using LKCode.AddressManagement.Core.Addresses;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Validators;
using Microsoft.Extensions.DependencyInjection;

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
    public void AddCoreAddresses_Called_ResolvesValidators()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCoreAddresses();
        var providerOptions = new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true };
        using ServiceProvider provider = services.BuildServiceProvider(providerOptions);

        // Act
        IValidator<CreateAddressRequest> createValidator =
            provider.GetRequiredService<IValidator<CreateAddressRequest>>();
        IValidator<UpdateAddressRequest> updateValidator =
            provider.GetRequiredService<IValidator<UpdateAddressRequest>>();

        // Assert
        createValidator.Should().BeOfType<CreateAddressRequestValidator>();
        updateValidator.Should().BeOfType<UpdateAddressRequestValidator>();
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
}
