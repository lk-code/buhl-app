using System.Text.Json;
using AwesomeAssertions;
using FluentValidation.Results;
using LKCode.AddressManagement.Service.Extensions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LKCode.AddressManagement.UnitTests.Service.Extensions;

public class ModelStateDictionaryExtensionsTests
{
    private const string FirstNameError = "'First Name' must not be empty.";
    private const string CountryError = "'Country' must consist of exactly two uppercase letters.";

    [Test]
    public void AddValidationErrors_NullModelState_ThrowsArgumentNullException()
    {
        // Arrange
        ModelStateDictionary modelState = null!;

        // Act
        Action act = () => modelState.AddValidationErrors(new ValidationResult(), JsonNamingPolicy.CamelCase);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("modelState");
    }

    [Test]
    public void AddValidationErrors_NullValidationResult_ThrowsArgumentNullException()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        ValidationResult validationResult = null!;

        // Act
        Action act = () => modelState.AddValidationErrors(validationResult, JsonNamingPolicy.CamelCase);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("validationResult");
    }

    [Test]
    public void AddValidationErrors_ValidResult_KeepsModelStateValid()
    {
        // Arrange
        var modelState = new ModelStateDictionary();

        // Act
        modelState.AddValidationErrors(new ValidationResult(), JsonNamingPolicy.CamelCase);

        // Assert
        modelState.IsValid.Should().BeTrue();
        modelState.Should().BeEmpty();
    }

    [Test]
    public void AddValidationErrors_CamelCasePolicy_AddsAllErrorsUnderCamelCaseNames()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        var validationResult = new ValidationResult(
        [
            new ValidationFailure("FirstName", FirstNameError),
            new ValidationFailure("Country", CountryError),
        ]);

        // Act
        modelState.AddValidationErrors(validationResult, JsonNamingPolicy.CamelCase);

        // Assert
        modelState.IsValid.Should().BeFalse();
        modelState.Keys.Should().BeEquivalentTo("firstName", "country");
        GetErrorMessages(modelState, "firstName").Should().Equal(FirstNameError);
        GetErrorMessages(modelState, "country").Should().Equal(CountryError);
    }

    [TestCase("FirstName", null, "FirstName")]
    [TestCase("FirstName", "camelCase", "firstName")]
    [TestCase("FirstName", "snake_case", "first_name")]
    [TestCase("FirstName", "kebab-case", "first-name")]
    [TestCase("", "camelCase", "")]
    [TestCase("", "snake_case", "")]
    public void AddValidationErrors_NamingPolicy_ConvertsPropertyName(
        string propertyName,
        string? namingPolicyName,
        string expectedKey)
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        var validationResult = new ValidationResult([new ValidationFailure(propertyName, FirstNameError)]);

        // Act
        modelState.AddValidationErrors(validationResult, GetNamingPolicy(namingPolicyName));

        // Assert
        modelState.Keys.Should().Equal(expectedKey);
        GetErrorMessages(modelState, expectedKey).Should().Equal(FirstNameError);
    }

    [Test]
    public void AddValidationErrors_SeveralErrorsForOneProperty_AddsAllErrorsInOrder()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        var validationResult = new ValidationResult(
        [
            new ValidationFailure("Country", CountryError),
            new ValidationFailure("Country", "'Country' must be an assigned ISO 3166-1 alpha-2 country code."),
        ]);

        // Act
        modelState.AddValidationErrors(validationResult, JsonNamingPolicy.CamelCase);

        // Assert
        GetErrorMessages(modelState, "country").Should().Equal(
            CountryError,
            "'Country' must be an assigned ISO 3166-1 alpha-2 country code.");
    }

    [Test]
    public void AddValidationErrors_ExistingErrors_KeepsExistingErrors()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.street", "The input was not valid.");
        var validationResult = new ValidationResult([new ValidationFailure("FirstName", FirstNameError)]);

        // Act
        modelState.AddValidationErrors(validationResult, JsonNamingPolicy.CamelCase);

        // Assert
        modelState.Keys.Should().BeEquivalentTo("$.street", "firstName");
    }

    private static JsonNamingPolicy? GetNamingPolicy(string? name) => name switch
    {
        "camelCase" => JsonNamingPolicy.CamelCase,
        "snake_case" => JsonNamingPolicy.SnakeCaseLower,
        "kebab-case" => JsonNamingPolicy.KebabCaseLower,
        _ => null,
    };

    private static IEnumerable<string> GetErrorMessages(ModelStateDictionary modelState, string key) =>
        modelState[key]!.Errors.Select(error => error.ErrorMessage);
}
