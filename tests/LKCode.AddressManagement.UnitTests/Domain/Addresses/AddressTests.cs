using System.Globalization;
using AwesomeAssertions;
using LKCode.AddressManagement.Domain.Addresses;
using LKCode.AddressManagement.Domain.Contracts;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace LKCode.AddressManagement.UnitTests.Domain.Addresses;

public class AddressTests
{
    private static readonly DateTimeOffset _startTime = new(2026, 3, 14, 9, 26, 53, 589, TimeSpan.Zero);

    private static readonly string[] _blankValues = ["", " ", "\t\n"];

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
    public void Create_ValidInput_SetsAllFields()
    {
        // Arrange
        AddressInput input = AddressInput.Valid;

        // Act
        Address address = Create(input);

        // Assert
        AddressInput.From(address).Should().Be(input);
    }

    [Test]
    public void Create_ValidInput_CreatesVersion7IdWithCurrentTimestamp()
    {
        // Arrange
        AddressInput input = AddressInput.Valid;

        // Act
        Address address = Create(input);

        // Assert
        address.Id.Version.Should().Be(7);
        GetUnixTimeMilliseconds(address.Id).Should().Be(_startTime.ToUnixTimeMilliseconds());
    }

    [Test]
    public void Create_CalledTwiceAtSameTime_CreatesDifferentIds()
    {
        // Arrange
        AddressInput input = AddressInput.Valid;

        // Act
        Address first = Create(input);
        Address second = Create(input);

        // Assert
        second.Id.Should().NotBe(first.Id);
    }

    [Test]
    public void Create_ValidInput_SetsCreatedAtAndUpdatedAtToCurrentTime()
    {
        // Arrange
        AddressInput input = AddressInput.Valid;

        // Act
        Address address = Create(input);

        // Assert
        address.CreatedAt.Should().Be(_startTime);
        address.UpdatedAt.Should().Be(_startTime);
    }

    [Test]
    public void Create_ValidInput_SetsNormalizedFieldsFromTrimmedValues()
    {
        // Arrange
        AddressInput input = AddressInput.Valid with { City = "  Köln\t", PostalCode = " 51147 " };

        // Act
        Address address = Create(input);

        // Assert
        address.NormalizedCity.Should().Be("city:Köln");
        address.NormalizedPostalCode.Should().Be("postal:51147");
    }

    [Test]
    public void Create_SurroundingWhitespace_TrimsAllValues()
    {
        // Arrange
        var input = new AddressInput(
            "  Erika ",
            "\tMustermann",
            "Heidestraße\n",
            " 17 ",
            " 51147\t",
            "\r\nKöln ",
            " DE ",
            "  erika.mustermann@example.com ");

        // Act
        Address address = Create(input);

        // Assert
        AddressInput.From(address).Should().Be(AddressInput.Valid);
    }

    [TestCase("de")]
    [TestCase("De")]
    [TestCase("dE")]
    [TestCase(" de ")]
    public void Create_LowerCaseCountry_ConvertsCountryToUpperCase(string country)
    {
        // Arrange
        AddressInput input = AddressInput.Valid with { Country = country };

        // Act
        Address address = Create(input);

        // Assert
        address.Country.Should().Be("DE");
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t\n")]
    public void Create_BlankEmail_SetsEmailToNull(string email)
    {
        // Arrange
        AddressInput input = AddressInput.Valid with { Email = email };

        // Act
        Address address = Create(input);

        // Assert
        address.Email.Should().BeNull();
    }

    [Test]
    public void Create_NullEmail_SetsEmailToNull()
    {
        // Arrange
        AddressInput input = AddressInput.Valid with { Email = null };

        // Act
        Address address = Create(input);

        // Assert
        address.Email.Should().BeNull();
    }

    [TestCaseSource(typeof(AddressField), nameof(AddressField.All))]
    public void Create_ValueAtMaxLength_KeepsValue(AddressField field)
    {
        // Arrange
        AddressInput input = field.With(AddressInput.Valid, field.ValueAtMaxLength);

        // Act
        Address address = Create(input);

        // Assert
        AddressInput.From(address).Should().Be(input);
    }

    [TestCaseSource(typeof(AddressField), nameof(AddressField.All))]
    public void Create_ValueAtMaxLengthWithSurroundingWhitespace_KeepsTrimmedValue(AddressField field)
    {
        // Arrange
        AddressInput input = field.With(AddressInput.Valid, $"  {field.ValueAtMaxLength}\t");

        // Act
        Address address = Create(input);

        // Assert
        AddressInput.From(address).Should().Be(field.With(AddressInput.Valid, field.ValueAtMaxLength));
    }

    [TestCaseSource(typeof(AddressField), nameof(AddressField.All))]
    public void Create_ValueExceedsMaxLength_ThrowsArgumentException(AddressField field)
    {
        // Arrange
        AddressInput input = field.With(AddressInput.Valid, field.ValueExceedingMaxLength);

        // Act
        Action act = () => Create(input);

        // Assert
        act.Should().ThrowExactly<ArgumentException>().WithParameterName(field.ParameterName);
    }

    [TestCaseSource(nameof(BlankRequiredFieldCases))]
    public void Create_BlankRequiredValue_ThrowsArgumentException(AddressField field, string value)
    {
        // Arrange
        AddressInput input = field.With(AddressInput.Valid, value);

        // Act
        Action act = () => Create(input);

        // Assert
        act.Should().ThrowExactly<ArgumentException>().WithParameterName(field.ParameterName);
    }

    [TestCaseSource(typeof(AddressField), nameof(AddressField.Required))]
    public void Create_NullRequiredValue_ThrowsArgumentNullException(AddressField field)
    {
        // Arrange
        AddressInput input = field.With(AddressInput.Valid, null);

        // Act
        Action act = () => Create(input);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName(field.ParameterName);
    }

    [Test]
    public void Create_NullTextNormalizer_ThrowsArgumentNullException()
    {
        // Arrange
        AddressInput input = AddressInput.Valid;

        // Act
        Action act = () => Address.Create(
            input.FirstName,
            input.LastName,
            input.Street,
            input.HouseNumber,
            input.PostalCode,
            input.City,
            input.Country,
            input.Email,
            null!,
            _timeProvider);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("textNormalizer");
    }

    [Test]
    public void Create_NullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange
        AddressInput input = AddressInput.Valid;

        // Act
        Action act = () => Address.Create(
            input.FirstName,
            input.LastName,
            input.Street,
            input.HouseNumber,
            input.PostalCode,
            input.City,
            input.Country,
            input.Email,
            _textNormalizer,
            null!);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("timeProvider");
    }

    private static IEnumerable<TestCaseData> BlankRequiredFieldCases() =>
        from field in AddressField.Required
        from value in _blankValues
        select new TestCaseData(field, value);

    /// <summary>
    /// Reads the Unix timestamp in milliseconds from the first 48 bits of a version 7 GUID.
    /// </summary>
    private static long GetUnixTimeMilliseconds(Guid id)
    {
        string timestampHex = id.ToString("N", CultureInfo.InvariantCulture)[..12];

        return long.Parse(timestampHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private Address Create(AddressInput input) => Address.Create(
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
