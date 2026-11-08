using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Mappings;
using LKCode.AddressManagement.Domain.Addresses;
using LKCode.AddressManagement.Domain.Contracts;
using LKCode.AddressManagement.UnitTests.Domain.Addresses;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses.Mappings;

public class AddressMappingExtensionsTests
{
    private static readonly DateTimeOffset _startTime = new(2026, 3, 14, 9, 26, 53, 589, TimeSpan.Zero);

    private ITextNormalizer _textNormalizer = null!;
    private FakeTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _textNormalizer = Substitute.For<ITextNormalizer>();
        _textNormalizer.Normalize(Arg.Any<string>()).Returns(call => "city:" + call.Arg<string>());
        _textNormalizer.NormalizePostalCode(Arg.Any<string>()).Returns(call => "postal:" + call.Arg<string>());
        _timeProvider = new FakeTimeProvider(_startTime);
    }

    [Test]
    public void ToResponse_Address_MapsAllValues()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        _timeProvider.Advance(TimeSpan.FromHours(2));
        AddressInput input = AddressInput.Changed;
        address.Update(
            input.FirstName,
            input.LastName,
            input.Street,
            input.HouseNumber,
            input.PostalCode,
            input.City,
            input.Country,
            input.Email,
            _textNormalizer,
            _timeProvider);

        // Act
        AddressResponse response = address.ToResponse();

        // Assert
        response.Should().Be(new AddressResponse(
            address.Id,
            "Max",
            "Musterfrau",
            "Hauptstraße",
            "4a",
            "SW1A 1AA",
            "London",
            "GB",
            "max.musterfrau@example.org",
            _startTime,
            _startTime.AddHours(2)));
    }

    [Test]
    public void ToResponse_AddressWithoutEmail_MapsEmailAsNull()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid with { Email = null });

        // Act
        AddressResponse response = address.ToResponse();

        // Assert
        response.Email.Should().BeNull();
    }

    [Test]
    public void ToResponse_NullAddress_ThrowsArgumentNullException()
    {
        // Arrange
        Address address = null!;

        // Act
        Action act = () => address.ToResponse();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("address");
    }

    private Address CreateAddress(AddressInput input) => Address.Create(
        input.FirstName,
        input.LastName,
        input.Street,
        input.HouseNumber,
        input.PostalCode,
        input.City,
        input.Country,
        input.Email,
        _textNormalizer,
        _timeProvider);
}
