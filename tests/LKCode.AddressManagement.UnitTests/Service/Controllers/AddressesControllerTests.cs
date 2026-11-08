using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Service.Controllers;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace LKCode.AddressManagement.UnitTests.Service.Controllers;

public class AddressesControllerTests
{
    private static readonly DateTimeOffset _createdAt = new(2026, 3, 14, 9, 26, 53, 589, TimeSpan.Zero);

    private static readonly AddressResponse _address = new(
        Guid.CreateVersion7(_createdAt),
        "Erika",
        "Mustermann",
        "Heidestraße",
        "17",
        "51147",
        "Köln",
        "DE",
        "erika.mustermann@example.com",
        _createdAt,
        _createdAt);

    private IAddressService _addressService = null!;
    private AddressesController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _addressService = Substitute.For<IAddressService>();
        _controller = new AddressesController(_addressService);
    }

    [Test]
    public void Constructor_NullAddressService_ThrowsArgumentNullException()
    {
        // Arrange
        IAddressService addressService = null!;

        // Act
        Action act = () => _ = new AddressesController(addressService);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("addressService");
    }

    [Test]
    public async Task GetByIdAsync_KnownId_ReturnsOkWithAddress()
    {
        // Arrange
        _addressService.GetByIdAsync(_address.Id, Arg.Any<CancellationToken>()).Returns(_address);

        // Act
        ActionResult<AddressResponse> result = await _controller.GetByIdAsync(_address.Id, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(_address);
    }

    [Test]
    public async Task GetByIdAsync_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.CreateVersion7(_createdAt);
        _addressService.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((AddressResponse?)null);

        // Act
        ActionResult<AddressResponse> result = await _controller.GetByIdAsync(id, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task GetByIdAsync_AnyId_PassesIdAndCancellationToken()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _controller.GetByIdAsync(_address.Id, cancellationTokenSource.Token);

        // Assert
        await _addressService.Received(1).GetByIdAsync(_address.Id, cancellationTokenSource.Token);
    }
}
