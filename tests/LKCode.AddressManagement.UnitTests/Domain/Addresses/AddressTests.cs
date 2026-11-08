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
    public void Create_ValidInput_SetsVersionToOne()
    {
        // Arrange
        AddressInput input = AddressInput.Valid;

        // Act
        Address address = Create(input);

        // Assert
        address.Version.Should().Be(1);
    }

    [Test]
    public void Create_ValidInput_IsNotDeleted()
    {
        // Arrange
        AddressInput input = AddressInput.Valid;

        // Act
        Address address = Create(input);

        // Assert
        address.IsDeleted.Should().BeFalse();
        address.DeletedAt.Should().BeNull();
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

    [Test]
    public void Update_ValidInput_ChangesAllFields()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        AddressInput input = AddressInput.Changed;

        // Act
        Update(address, input);

        // Assert
        AddressInput.From(address).Should().Be(input);
    }

    [Test]
    public void Update_ValidInput_KeepsIdAndCreatedAt()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        Guid id = address.Id;
        _timeProvider.Advance(TimeSpan.FromHours(2));

        // Act
        Update(address, AddressInput.Changed);

        // Assert
        address.Id.Should().Be(id);
        address.CreatedAt.Should().Be(_startTime);
    }

    [Test]
    public void Update_ValidInput_SetsUpdatedAtToCurrentTime()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        _timeProvider.Advance(TimeSpan.FromHours(2));

        // Act
        Update(address, AddressInput.Changed);

        // Assert
        address.UpdatedAt.Should().Be(_startTime.AddHours(2));
    }

    [TestCase(1, 2)]
    [TestCase(2, 3)]
    [TestCase(5, 6)]
    public void Update_ValidInput_IncrementsVersionByOne(int updates, long expectedVersion)
    {
        // Arrange
        Address address = Create(AddressInput.Valid);

        // Act
        for (int update = 0; update < updates; update++)
        {
            Update(address, update % 2 == 0 ? AddressInput.Changed : AddressInput.Valid);
        }

        // Assert
        address.Version.Should().Be(expectedVersion);
    }

    [Test]
    public void Update_SameValues_IncrementsVersion()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);

        // Act
        Update(address, AddressInput.Valid);

        // Assert
        address.Version.Should().Be(2);
    }

    [Test]
    public void Update_ValidInput_UpdatesNormalizedFieldsFromTrimmedValues()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        AddressInput input = AddressInput.Changed with { City = " London\t", PostalCode = "  SW1A 1AA " };

        // Act
        Update(address, input);

        // Assert
        address.NormalizedCity.Should().Be("city:London");
        address.NormalizedPostalCode.Should().Be("postal:SW1A 1AA");
    }

    [Test]
    public void Update_SurroundingWhitespace_TrimsAllValues()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        var input = new AddressInput(
            " Max  ",
            "Musterfrau\t",
            "\nHauptstraße",
            " 4a ",
            "\tSW1A 1AA ",
            " London\r\n",
            "  GB ",
            " max.musterfrau@example.org\t");

        // Act
        Update(address, input);

        // Assert
        AddressInput.From(address).Should().Be(AddressInput.Changed);
    }

    [TestCase("gb")]
    [TestCase("Gb")]
    [TestCase("gB")]
    [TestCase(" gb ")]
    public void Update_LowerCaseCountry_ConvertsCountryToUpperCase(string country)
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        AddressInput input = AddressInput.Changed with { Country = country };

        // Act
        Update(address, input);

        // Assert
        address.Country.Should().Be("GB");
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t\n")]
    public void Update_BlankEmail_RemovesEmail(string email)
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        AddressInput input = AddressInput.Changed with { Email = email };

        // Act
        Update(address, input);

        // Assert
        address.Email.Should().BeNull();
    }

    [Test]
    public void Update_NullEmail_RemovesEmail()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        AddressInput input = AddressInput.Changed with { Email = null };

        // Act
        Update(address, input);

        // Assert
        address.Email.Should().BeNull();
    }

    [TestCaseSource(typeof(AddressField), nameof(AddressField.All))]
    public void Update_ValueAtMaxLength_KeepsValue(AddressField field)
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        AddressInput input = field.With(AddressInput.Changed, field.ValueAtMaxLength);

        // Act
        Update(address, input);

        // Assert
        AddressInput.From(address).Should().Be(input);
    }

    [TestCaseSource(typeof(AddressField), nameof(AddressField.All))]
    public void Update_ValueAtMaxLengthWithSurroundingWhitespace_KeepsTrimmedValue(AddressField field)
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        AddressInput input = field.With(AddressInput.Changed, $"\t{field.ValueAtMaxLength}  ");

        // Act
        Update(address, input);

        // Assert
        AddressInput.From(address).Should().Be(field.With(AddressInput.Changed, field.ValueAtMaxLength));
    }

    [TestCaseSource(typeof(AddressField), nameof(AddressField.All))]
    public void Update_ValueExceedsMaxLength_ThrowsArgumentException(AddressField field)
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        AddressInput input = field.With(AddressInput.Changed, field.ValueExceedingMaxLength);

        // Act
        Action act = () => Update(address, input);

        // Assert
        act.Should().ThrowExactly<ArgumentException>().WithParameterName(field.ParameterName);
    }

    [TestCaseSource(nameof(BlankRequiredFieldCases))]
    public void Update_BlankRequiredValue_ThrowsArgumentException(AddressField field, string value)
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        AddressInput input = field.With(AddressInput.Changed, value);

        // Act
        Action act = () => Update(address, input);

        // Assert
        act.Should().ThrowExactly<ArgumentException>().WithParameterName(field.ParameterName);
    }

    [TestCaseSource(typeof(AddressField), nameof(AddressField.Required))]
    public void Update_NullRequiredValue_ThrowsArgumentNullException(AddressField field)
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        AddressInput input = field.With(AddressInput.Changed, null);

        // Act
        Action act = () => Update(address, input);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName(field.ParameterName);
    }

    [TestCaseSource(typeof(AddressField), nameof(AddressField.All))]
    public void Update_InvalidValue_LeavesAddressUnchanged(AddressField field)
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        var expected = AddressState.From(address);
        _timeProvider.Advance(TimeSpan.FromHours(2));
        AddressInput input = field.With(AddressInput.Changed, field.ValueExceedingMaxLength);

        // Act
        Action act = () => Update(address, input);

        // Assert
        act.Should().Throw<ArgumentException>();
        AddressState.From(address).Should().Be(expected);
    }

    [Test]
    public void Update_DeletedAddress_ThrowsInvalidOperationExceptionAndLeavesAddressUnchanged()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        address.Delete(_timeProvider);
        var expected = AddressState.From(address);
        _timeProvider.Advance(TimeSpan.FromHours(2));

        // Act
        Action act = () => Update(address, AddressInput.Changed);

        // Assert
        act.Should().ThrowExactly<InvalidOperationException>();
        AddressState.From(address).Should().Be(expected);
    }

    [Test]
    public void Update_NullTextNormalizer_ThrowsArgumentNullException()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        AddressInput input = AddressInput.Changed;

        // Act
        Action act = () => address.Update(
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
    public void Update_NullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        AddressInput input = AddressInput.Changed;

        // Act
        Action act = () => address.Update(
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

    [Test]
    public void Delete_NotDeleted_MarksAddressAsDeletedAtCurrentTime()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        _timeProvider.Advance(TimeSpan.FromHours(2));

        // Act
        address.Delete(_timeProvider);

        // Assert
        address.IsDeleted.Should().BeTrue();
        address.DeletedAt.Should().Be(_startTime.AddHours(2));
    }

    [Test]
    public void Delete_NotDeleted_SetsUpdatedAtToDeletionTime()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        _timeProvider.Advance(TimeSpan.FromHours(2));

        // Act
        address.Delete(_timeProvider);

        // Assert
        address.UpdatedAt.Should().Be(_startTime.AddHours(2));
    }

    [TestCase(0, 2)]
    [TestCase(1, 3)]
    [TestCase(4, 6)]
    public void Delete_NotDeleted_IncrementsVersionByOne(int updates, long expectedVersion)
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        for (int update = 0; update < updates; update++)
        {
            Update(address, update % 2 == 0 ? AddressInput.Changed : AddressInput.Valid);
        }

        // Act
        address.Delete(_timeProvider);

        // Assert
        address.Version.Should().Be(expectedVersion);
    }

    [Test]
    public void Delete_NotDeleted_KeepsIdValuesNormalizedFieldsAndCreatedAt()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        var before = AddressState.From(address);
        _timeProvider.Advance(TimeSpan.FromHours(2));

        // Act
        address.Delete(_timeProvider);

        // Assert
        AddressState.From(address).Should().Be(before with
        {
            UpdatedAt = _startTime.AddHours(2),
            Version = 2,
            IsDeleted = true,
            DeletedAt = _startTime.AddHours(2),
        });
    }

    [Test]
    public void Delete_AlreadyDeleted_ThrowsInvalidOperationExceptionAndLeavesAddressUnchanged()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        address.Delete(_timeProvider);
        var expected = AddressState.From(address);
        _timeProvider.Advance(TimeSpan.FromHours(2));

        // Act
        Action act = () => address.Delete(_timeProvider);

        // Assert
        act.Should().ThrowExactly<InvalidOperationException>();
        AddressState.From(address).Should().Be(expected);
    }

    [Test]
    public void Delete_NullTimeProvider_ThrowsArgumentNullExceptionAndLeavesAddressUnchanged()
    {
        // Arrange
        Address address = Create(AddressInput.Valid);
        var expected = AddressState.From(address);

        // Act
        Action act = () => address.Delete(null!);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("timeProvider");
        AddressState.From(address).Should().Be(expected);
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

    private void Update(Address address, AddressInput input) => address.Update(
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

    /// <summary>
    /// All values of an address, to compare its state before and after an operation.
    /// </summary>
    private sealed record AddressState(
        Guid Id,
        AddressInput Input,
        string NormalizedCity,
        string NormalizedPostalCode,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        long Version,
        bool IsDeleted,
        DateTimeOffset? DeletedAt)
    {
        public static AddressState From(Address address) => new(
            address.Id,
            AddressInput.From(address),
            address.NormalizedCity,
            address.NormalizedPostalCode,
            address.CreatedAt,
            address.UpdatedAt,
            address.Version,
            address.IsDeleted,
            address.DeletedAt);
    }
}
