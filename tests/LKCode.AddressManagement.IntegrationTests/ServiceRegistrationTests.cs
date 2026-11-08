using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Data;
using LKCode.AddressManagement.Domain.Contracts;
using LKCode.AddressManagement.Domain.Normalizers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LKCode.AddressManagement.IntegrationTests;

public class ServiceRegistrationTests
{
    private AddressManagementWebApplicationFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new AddressManagementWebApplicationFactory();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _factory.DisposeAsync();
    }

    [Test]
    public void Services_Factory_UseDatabaseNameOfFactory()
    {
        // Act
        DataOptions options = _factory.Services.GetRequiredService<IOptions<DataOptions>>().Value;

        // Assert
        options.DatabaseName.Should().Be(_factory.DatabaseName);
    }

    [Test]
    public async Task Services_WithoutDatabaseNameSetting_UseConfiguredDatabaseName()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Program>();

        // Act
        DataOptions options = factory.Services.GetRequiredService<IOptions<DataOptions>>().Value;

        // Assert
        options.DatabaseName.Should().Be("AddressManagement");
    }

    [Test]
    public async Task Services_Scope_ResolvesAddressService()
    {
        // Arrange
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        // Act
        IAddressService addressService = scope.ServiceProvider.GetRequiredService<IAddressService>();

        // Assert
        addressService.Should().BeOfType<AddressService>();
    }

    [Test]
    public void Services_Root_ResolvesTextNormalizer()
    {
        // Act
        ITextNormalizer textNormalizer = _factory.Services.GetRequiredService<ITextNormalizer>();

        // Assert
        textNormalizer.Should().BeOfType<TextNormalizer>();
    }

    [Test]
    public void Services_Root_ResolvesSystemTimeProvider()
    {
        // Act
        TimeProvider timeProvider = _factory.Services.GetRequiredService<TimeProvider>();

        // Assert
        timeProvider.Should().BeSameAs(TimeProvider.System);
    }

    [Test]
    public async Task Services_TwoFactories_UseSeparateDatabases()
    {
        // Arrange
        await using var factory = new AddressManagementWebApplicationFactory();
        await using var otherFactory = new AddressManagementWebApplicationFactory();
        AddressResponse created = await CreateAddressAsync(factory);

        // Act
        IReadOnlyList<AddressResponse> fromSameFactory = await ListAddressesAsync(factory);
        IReadOnlyList<AddressResponse> fromOtherFactory = await ListAddressesAsync(otherFactory);

        // Assert
        otherFactory.DatabaseName.Should().NotBe(factory.DatabaseName);
        fromSameFactory.Should().ContainSingle().Which.Id.Should().Be(created.Id);
        fromOtherFactory.Should().BeEmpty();
    }

    private static async Task<AddressResponse> CreateAddressAsync(AddressManagementWebApplicationFactory factory)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IAddressService addressService = scope.ServiceProvider.GetRequiredService<IAddressService>();
        var request = new CreateAddressRequest(
            "Erika",
            "Mustermann",
            "Heidestraße",
            "17",
            "51147",
            "Köln",
            "DE",
            "erika.mustermann@example.com");

        return await addressService.CreateAsync(request, CancellationToken.None);
    }

    private static async Task<IReadOnlyList<AddressResponse>> ListAddressesAsync(
        AddressManagementWebApplicationFactory factory)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IAddressService addressService = scope.ServiceProvider.GetRequiredService<IAddressService>();

        AddressListResponse page =
            await addressService.GetListAsync(new AddressListQuery(null, null), CancellationToken.None);

        return page.Items;
    }
}
