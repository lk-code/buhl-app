using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Sanitizers;
using LKCode.AddressManagement.UnitTests.Domain.Addresses;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses.Sanitizers;

public class AddressRequestSanitizerExtensionsTests
{
    private static readonly string[] _blankValues = ["", " ", "\t\n"];

    private static readonly string[] _lowerCaseCountries = ["de", "De", "dE", " de "];

    /// <summary>
    /// Both request types, each sanitized through its own overload of <c>Sanitize</c>.
    /// </summary>
    public static IReadOnlyList<RequestKind> Kinds { get; } =
    [
        new(nameof(CreateAddressRequest), input => input.ToCreateRequest().Sanitize().ToInput()),
        new(nameof(UpdateAddressRequest), input => input.ToUpdateRequest().Sanitize().ToInput()),
    ];

    [TestCaseSource(nameof(Kinds))]
    public void Sanitize_CleanValues_KeepsAllValues(RequestKind kind)
    {
        // Arrange
        AddressInput input = AddressInput.Valid;

        // Act
        AddressInput result = kind.Sanitize(input);

        // Assert
        result.Should().Be(input);
    }

    [TestCaseSource(nameof(Kinds))]
    public void Sanitize_SurroundingWhitespace_TrimsAllValues(RequestKind kind)
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
        AddressInput result = kind.Sanitize(input);

        // Assert
        result.Should().Be(AddressInput.Valid);
    }

    [TestCaseSource(nameof(Kinds))]
    public void Sanitize_InnerWhitespace_KeepsInnerWhitespace(RequestKind kind)
    {
        // Arrange
        AddressInput input = AddressInput.Valid with { FirstName = "Anna  Lena", Street = "Unter den Linden" };

        // Act
        AddressInput result = kind.Sanitize(input);

        // Assert
        result.Should().Be(input);
    }

    [TestCaseSource(nameof(LowerCaseCountryCases))]
    public void Sanitize_LowerCaseCountry_ConvertsCountryToUpperCase(RequestKind kind, string country)
    {
        // Arrange
        AddressInput input = AddressInput.Valid with { Country = country };

        // Act
        AddressInput result = kind.Sanitize(input);

        // Assert
        result.Country.Should().Be("DE");
    }

    [TestCaseSource(nameof(BlankEmailCases))]
    public void Sanitize_BlankEmail_SetsEmailToNull(RequestKind kind, string email)
    {
        // Arrange
        AddressInput input = AddressInput.Valid with { Email = email };

        // Act
        AddressInput result = kind.Sanitize(input);

        // Assert
        result.Email.Should().BeNull();
    }

    [TestCaseSource(nameof(Kinds))]
    public void Sanitize_NullEmail_KeepsEmailNull(RequestKind kind)
    {
        // Arrange
        AddressInput input = AddressInput.Valid with { Email = null };

        // Act
        AddressInput result = kind.Sanitize(input);

        // Assert
        result.Email.Should().BeNull();
    }

    [TestCaseSource(nameof(NullRequiredValueCases))]
    public void Sanitize_NullRequiredValue_SetsEmptyString(RequestKind kind, AddressField field)
    {
        // Arrange
        AddressInput input = field.With(AddressInput.Valid, null);

        // Act
        AddressInput result = kind.Sanitize(input);

        // Assert
        result.Should().Be(field.With(AddressInput.Valid, string.Empty));
    }

    [TestCaseSource(nameof(ValueExceedingMaxLengthCases))]
    public void Sanitize_ValueExceedsMaxLength_KeepsValueForValidation(RequestKind kind, AddressField field)
    {
        // Arrange
        AddressInput input = field.With(AddressInput.Valid, field.ValueExceedingMaxLength);

        // Act
        AddressInput result = kind.Sanitize(input);

        // Assert
        result.Should().Be(input);
    }

    [Test]
    public void Sanitize_NullCreateRequest_ThrowsArgumentNullException()
    {
        // Arrange
        CreateAddressRequest request = null!;

        // Act
        Action act = () => request.Sanitize();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("request");
    }

    [Test]
    public void Sanitize_NullUpdateRequest_ThrowsArgumentNullException()
    {
        // Arrange
        UpdateAddressRequest request = null!;

        // Act
        Action act = () => request.Sanitize();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("request");
    }

    private static IEnumerable<TestCaseData> LowerCaseCountryCases() =>
        from kind in Kinds
        from country in _lowerCaseCountries
        select new TestCaseData(kind, country);

    private static IEnumerable<TestCaseData> BlankEmailCases() =>
        from kind in Kinds
        from email in _blankValues
        select new TestCaseData(kind, email);

    private static IEnumerable<TestCaseData> NullRequiredValueCases() =>
        from kind in Kinds
        from field in AddressField.Required
        select new TestCaseData(kind, field);

    private static IEnumerable<TestCaseData> ValueExceedingMaxLengthCases() =>
        from kind in Kinds
        from field in AddressField.All
        select new TestCaseData(kind, field);

    /// <summary>
    /// A request type with the conversion from the test values through <c>Sanitize</c> back to test values.
    /// </summary>
    public sealed record RequestKind(string Name, Func<AddressInput, AddressInput> Sanitize)
    {
        public override string ToString() => Name;
    }
}
