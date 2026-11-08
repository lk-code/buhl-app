using AwesomeAssertions;
using LKCode.AddressManagement.Domain;
using LKCode.AddressManagement.Domain.Contracts;
using LKCode.AddressManagement.Domain.Normalizers;
using Microsoft.Extensions.DependencyInjection;

namespace LKCode.AddressManagement.UnitTests.Domain;

public class ServiceCollectionExtensionsTests
{
    [Test]
    public void AddDomain_Called_RegistersTextNormalizerAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDomain();

        // Assert
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(ITextNormalizer))
            .Which.Should().Match<ServiceDescriptor>(descriptor =>
                descriptor.ImplementationType == typeof(TextNormalizer)
                && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Test]
    public void AddDomain_Called_ResolvesSameTextNormalizerInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDomain();
        using ServiceProvider provider = services.BuildServiceProvider();

        // Act
        ITextNormalizer first = provider.GetRequiredService<ITextNormalizer>();
        ITextNormalizer second = provider.GetRequiredService<ITextNormalizer>();

        // Assert
        first.Should().BeOfType<TextNormalizer>();
        second.Should().BeSameAs(first);
    }

    [Test]
    public void AddDomain_Called_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        IServiceCollection result = services.AddDomain();

        // Assert
        result.Should().BeSameAs(services);
    }

    [Test]
    public void AddDomain_CalledTwice_RegistersTextNormalizerOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDomain();
        services.AddDomain();

        // Assert
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(ITextNormalizer));
    }

    [Test]
    public void AddDomain_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        Action act = () => services.AddDomain();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }
}
