using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.UnitTests.Domain.Addresses;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses.Validators;

/// <summary>
/// The tests of the rules shared by all address requests, run for each request type by a derived fixture.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
public abstract class AddressRequestValidatorTests<TRequest>
    where TRequest : IAddressRequest
{
    private static readonly string[] _blankValues = ["", " ", "\t\n"];

    private IValidator<TRequest> _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = CreateValidator();
    }

    [Test]
    public void Validate_ValidRequest_ReturnsNoErrors()
    {
        // Arrange
        TRequest request = CreateRequest(AddressInput.Valid);

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [TestCaseSource(typeof(AddressField), nameof(AddressField.Required))]
    public void Validate_NullRequiredValue_ReturnsSingleErrorForValue(AddressField field)
    {
        // Arrange
        TRequest request = CreateRequest(field.With(AddressInput.Valid, null));

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        ShouldHaveSingleErrorFor(result, field);
    }

    [TestCaseSource(nameof(BlankRequiredValueCases))]
    public void Validate_BlankRequiredValue_ReturnsSingleErrorForValue(AddressField field, string value)
    {
        // Arrange
        TRequest request = CreateRequest(field.With(AddressInput.Valid, value));

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        ShouldHaveSingleErrorFor(result, field);
    }

    [TestCaseSource(typeof(AddressField), nameof(AddressField.All))]
    public void Validate_ValueAtMaxLength_ReturnsNoErrors(AddressField field)
    {
        // Arrange
        TRequest request = CreateRequest(field.With(AddressInput.Valid, field.ValueAtMaxLength));

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [TestCaseSource(typeof(AddressField), nameof(AddressField.All))]
    public void Validate_ValueExceedsMaxLength_ReturnsSingleErrorForValue(AddressField field)
    {
        // Arrange
        TRequest request = CreateRequest(field.With(AddressInput.Valid, field.ValueExceedingMaxLength));

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        ShouldHaveSingleErrorFor(result, field);
    }

    [TestCase("DE")]
    [TestCase("AT")]
    [TestCase("CH")]
    [TestCase("GB")]
    [TestCase("US")]
    [TestCase("AD")]
    [TestCase("ZW")]
    public void Validate_AssignedCountry_ReturnsNoErrors(string country)
    {
        // Arrange
        TRequest request = CreateRequest(AddressInput.Valid with { Country = country });

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [TestCase("GER")]
    [TestCase("D")]
    [TestCase("de1")]
    [TestCase("XX")]
    [TestCase("de")]
    [TestCase("De")]
    [TestCase("D1")]
    [TestCase("1A")]
    [TestCase("D-")]
    [TestCase("ÄÖ")]
    [TestCase("UK")]
    [TestCase("EU")]
    [TestCase("XK")]
    [TestCase("ZZ")]
    public void Validate_InvalidCountry_ReturnsSingleErrorForCountry(string country)
    {
        // Arrange
        TRequest request = CreateRequest(AddressInput.Valid with { Country = country });

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Country");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t\n")]
    public void Validate_MissingEmail_ReturnsNoErrors(string? email)
    {
        // Arrange
        TRequest request = CreateRequest(AddressInput.Valid with { Email = email });

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [TestCase("erika.mustermann@example.com")]
    [TestCase("erika+news@example.com")]
    [TestCase("erika_mustermann@sub.example.co.uk")]
    [TestCase("e@x.io")]
    [TestCase("Erika.Mustermann@Example.COM")]
    [TestCase("jörg@exämple.de")]
    [TestCase("\"erika mustermann\"@example.com")]
    public void Validate_ValidEmail_ReturnsNoErrors(string email)
    {
        // Arrange
        TRequest request = CreateRequest(AddressInput.Valid with { Email = email });

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [TestCase("erika")]
    [TestCase("erika.example.com")]
    [TestCase("@example.com")]
    [TestCase("erika@")]
    [TestCase("erika@@example.com")]
    [TestCase("erika@example@com")]
    [TestCase("erika mustermann@example.com")]
    [TestCase("erika@exa mple.com")]
    [TestCase("erika@example..com")]
    [TestCase(".erika@example.com")]
    [TestCase("erika..mustermann@example.com")]
    [TestCase("erika@example,com")]
    [TestCase("erika;max@example.com")]
    [TestCase("Erika <erika@example.com>")]
    [TestCase("<erika@example.com>")]
    [TestCase("erika(comment)@example.com")]
    [TestCase("erika@example.com, max@example.com")]
    public void Validate_InvalidEmail_ReturnsSingleErrorForEmail(string email)
    {
        // Arrange
        TRequest request = CreateRequest(AddressInput.Valid with { Email = email });

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Email");
    }

    [Test]
    public void Validate_AllValuesInvalid_ReturnsOneErrorForEachValue()
    {
        // Arrange
        AddressInput input = AddressField.All.Aggregate(
            AddressInput.Valid,
            (current, field) => field.With(current, field.ValueExceedingMaxLength));
        TRequest request = CreateRequest(input);

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            AddressField.All.Select(field => field.ParameterName),
            options => options.IgnoringCase());
    }

    /// <summary>
    /// Creates the validator under test.
    /// </summary>
    protected abstract IValidator<TRequest> CreateValidator();

    /// <summary>
    /// Creates a request of the tested type with the given values.
    /// </summary>
    protected abstract TRequest CreateRequest(AddressInput input);

    protected static IEnumerable<TestCaseData> BlankRequiredValueCases() =>
        from field in AddressField.Required
        from value in _blankValues
        select new TestCaseData(field, value);

    private static void ShouldHaveSingleErrorFor(ValidationResult result, AddressField field) =>
        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().BeEquivalentTo(field.ParameterName);
}
