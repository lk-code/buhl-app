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
            _startTime.AddHours(2),
            2));
    }

    [Test]
    public void ToResponse_NewAddress_MapsVersionOne()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);

        // Act
        AddressResponse response = address.ToResponse();

        // Assert
        response.Version.Should().Be(1);
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

    [Test]
    public void ToUpdateRequest_Response_MapsAllValues()
    {
        // Arrange
        AddressResponse response = CreateAddress(AddressInput.Changed).ToResponse();

        // Act
        UpdateAddressRequest request = response.ToUpdateRequest();

        // Assert
        request.Should().Be(new UpdateAddressRequest(
            "Max",
            "Musterfrau",
            "Hauptstraße",
            "4a",
            "SW1A 1AA",
            "London",
            "GB",
            "max.musterfrau@example.org"));
    }

    [Test]
    public void ToUpdateRequest_ResponseWithoutEmail_MapsEmailAsNull()
    {
        // Arrange
        AddressResponse response = CreateAddress(AddressInput.Valid with { Email = null }).ToResponse();

        // Act
        UpdateAddressRequest request = response.ToUpdateRequest();

        // Assert
        request.Email.Should().BeNull();
    }

    [Test]
    public void ToUpdateRequest_NullResponse_ThrowsArgumentNullException()
    {
        // Arrange
        AddressResponse response = null!;

        // Act
        Action act = () => response.ToUpdateRequest();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("response");
    }

    [Test]
    public void ToAddress_ValidRequest_CreatesAddressWithAllValues()
    {
        // Arrange
        CreateAddressRequest request = AddressInput.Valid.ToCreateRequest();

        // Act
        var address = request.ToAddress(_textNormalizer, _timeProvider);

        // Assert
        AddressInput.From(address).Should().Be(AddressInput.Valid);
    }

    [Test]
    public void ToAddress_ValidRequest_SetsIdTimestampsAndNormalizedFields()
    {
        // Arrange
        CreateAddressRequest request = AddressInput.Valid.ToCreateRequest();

        // Act
        var address = request.ToAddress(_textNormalizer, _timeProvider);

        // Assert
        address.Id.Version.Should().Be(7);
        address.CreatedAt.Should().Be(_startTime);
        address.UpdatedAt.Should().Be(_startTime);
        address.NormalizedCity.Should().Be("city:Köln");
        address.NormalizedPostalCode.Should().Be("postal:51147");
    }

    [Test]
    public void ToAddress_RequestWithoutEmail_CreatesAddressWithoutEmail()
    {
        // Arrange
        CreateAddressRequest request = (AddressInput.Valid with { Email = null }).ToCreateRequest();

        // Act
        var address = request.ToAddress(_textNormalizer, _timeProvider);

        // Assert
        address.Email.Should().BeNull();
    }

    [Test]
    public void ToAddress_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        CreateAddressRequest request = null!;

        // Act
        Action act = () => request.ToAddress(_textNormalizer, _timeProvider);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("request");
    }

    [Test]
    public void ToAddress_NullTextNormalizer_ThrowsArgumentNullException()
    {
        // Arrange
        CreateAddressRequest request = AddressInput.Valid.ToCreateRequest();

        // Act
        Action act = () => request.ToAddress(null!, _timeProvider);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("textNormalizer");
    }

    [Test]
    public void ToAddress_NullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange
        CreateAddressRequest request = AddressInput.Valid.ToCreateRequest();

        // Act
        Action act = () => request.ToAddress(_textNormalizer, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("timeProvider");
    }

    [Test]
    public void ApplyTo_ValidRequest_ChangesAllValues()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        request.ApplyTo(address, _textNormalizer, _timeProvider);

        // Assert
        AddressInput.From(address).Should().Be(AddressInput.Changed);
    }

    [Test]
    public void ApplyTo_ValidRequest_KeepsIdAndCreatedAtAndSetsUpdatedAt()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        Guid id = address.Id;
        _timeProvider.Advance(TimeSpan.FromHours(2));
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        request.ApplyTo(address, _textNormalizer, _timeProvider);

        // Assert
        address.Id.Should().Be(id);
        address.CreatedAt.Should().Be(_startTime);
        address.UpdatedAt.Should().Be(_startTime.AddHours(2));
    }

    [Test]
    public void ApplyTo_ValidRequest_UpdatesNormalizedFields()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        request.ApplyTo(address, _textNormalizer, _timeProvider);

        // Assert
        address.NormalizedCity.Should().Be("city:London");
        address.NormalizedPostalCode.Should().Be("postal:SW1A 1AA");
    }

    [Test]
    public void ApplyTo_RequestWithoutEmail_RemovesEmail()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        UpdateAddressRequest request = (AddressInput.Changed with { Email = null }).ToUpdateRequest();

        // Act
        request.ApplyTo(address, _textNormalizer, _timeProvider);

        // Assert
        address.Email.Should().BeNull();
    }

    [Test]
    public void ApplyTo_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        UpdateAddressRequest request = null!;

        // Act
        Action act = () => request.ApplyTo(address, _textNormalizer, _timeProvider);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("request");
    }

    [Test]
    public void ApplyTo_NullAddress_ThrowsArgumentNullException()
    {
        // Arrange
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        Action act = () => request.ApplyTo(null!, _textNormalizer, _timeProvider);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("address");
    }

    [Test]
    public void ApplyTo_NullTextNormalizer_ThrowsArgumentNullException()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        Action act = () => request.ApplyTo(address, null!, _timeProvider);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("textNormalizer");
    }

    [Test]
    public void ApplyTo_NullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        Action act = () => request.ApplyTo(address, _textNormalizer, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("timeProvider");
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
