using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Mappings;
using LKCode.AddressManagement.Core.Addresses.Results;
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

    [Test]
    public void ToListResponse_Page_MapsAddressesInOrderWithPageAndCounts()
    {
        // Arrange
        Address first = CreateAddress(AddressInput.Changed);
        Address second = CreateAddress(AddressInput.Valid);
        var addressPage = new AddressPage([first, second], 7);

        // Act
        AddressListResponse response = addressPage.ToListResponse(2, 2);

        // Assert
        response.Items.Should().Equal(first.ToResponse(), second.ToResponse());
        response.Page.Should().Be(2);
        response.PageSize.Should().Be(2);
        response.TotalCount.Should().Be(7);
        response.TotalPages.Should().Be(4);
    }

    [TestCase(0, 20, 0)]
    [TestCase(1, 20, 1)]
    [TestCase(19, 20, 1)]
    [TestCase(20, 20, 1)]
    [TestCase(21, 20, 2)]
    [TestCase(25, 1, 25)]
    [TestCase(100, 100, 1)]
    [TestCase(101, 100, 2)]
    [TestCase(int.MaxValue, 1, int.MaxValue)]
    [TestCase(int.MaxValue, 100, 21_474_837)]
    [TestCase(int.MaxValue, int.MaxValue, 1)]
    public void ToListResponse_TotalCount_ReturnsNumberOfPagesRoundedUp(
        int totalCount,
        int pageSize,
        int expectedTotalPages)
    {
        // Arrange
        var addressPage = new AddressPage([], totalCount);

        // Act
        AddressListResponse response = addressPage.ToListResponse(1, pageSize);

        // Assert
        response.TotalPages.Should().Be(expectedTotalPages);
    }

    [Test]
    public void ToListResponse_PageBehindEnd_KeepsRequestedPageWithoutItems()
    {
        // Arrange
        var addressPage = new AddressPage([], 5);

        // Act
        AddressListResponse response = addressPage.ToListResponse(9, 10);

        // Assert
        response.Should().BeEquivalentTo(new AddressListResponse([], 9, 10, 5, 1));
    }

    [Test]
    public void ToListResponse_NullPage_ThrowsArgumentNullException()
    {
        // Arrange
        AddressPage addressPage = null!;

        // Act
        Action act = () => addressPage.ToListResponse(1, 20);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("addressPage");
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ToListResponse_PageBelowOne_ThrowsArgumentOutOfRangeException(int invalidPage)
    {
        // Arrange
        var addressPage = new AddressPage([], 0);

        // Act
        Action act = () => addressPage.ToListResponse(invalidPage, 20);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("page");
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ToListResponse_PageSizeBelowOne_ThrowsArgumentOutOfRangeException(int invalidPageSize)
    {
        // Arrange
        var addressPage = new AddressPage([], 0);

        // Act
        Action act = () => addressPage.ToListResponse(1, invalidPageSize);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pageSize");
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
