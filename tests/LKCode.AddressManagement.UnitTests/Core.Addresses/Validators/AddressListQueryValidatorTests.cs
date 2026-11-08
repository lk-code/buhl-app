using AwesomeAssertions;
using FluentValidation.Results;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Queries;
using LKCode.AddressManagement.Core.Addresses.Validators;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses.Validators;

public class AddressListQueryValidatorTests
{
    private const string SortByError =
        "'Sort By' must be one of: lastName, firstName, city, postalCode, country, createdAt, updatedAt.";

    private const string SortDirectionError = "'Sort Direction' must be one of: asc, desc.";

    private static readonly AddressListQuery _emptyQuery = new(null, null);

    private readonly AddressListQueryValidator _validator = new();

    [Test]
    public void Validate_NoValues_ReturnsNoErrors()
    {
        // Act
        ValidationResult result = _validator.Validate(_emptyQuery);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_AllValuesValid_ReturnsNoErrors()
    {
        // Arrange
        var query = new AddressListQuery("Köln", "51147", 3, 50, "city", "desc");

        // Act
        ValidationResult result = _validator.Validate(query);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(1000)]
    [TestCase(int.MaxValue)]
    public void Validate_PageAtLeastOne_ReturnsNoErrors(int page)
    {
        // Arrange
        AddressListQuery query = _emptyQuery with { Page = page };

        // Act
        ValidationResult result = _validator.Validate(query);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void Validate_PageBelowOne_ReturnsSingleErrorForPage(int page)
    {
        // Arrange
        AddressListQuery query = _emptyQuery with { Page = page };

        // Act
        ValidationResult result = _validator.Validate(query);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(AddressListQuery.Page));
    }

    [TestCase(AddressListConstraints.MinPageSize)]
    [TestCase(2)]
    [TestCase(AddressListConstraints.DefaultPageSize)]
    [TestCase(99)]
    [TestCase(AddressListConstraints.MaxPageSize)]
    public void Validate_PageSizeInRange_ReturnsNoErrors(int pageSize)
    {
        // Arrange
        AddressListQuery query = _emptyQuery with { PageSize = pageSize };

        // Act
        ValidationResult result = _validator.Validate(query);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(101)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public void Validate_PageSizeOutOfRange_ReturnsSingleErrorForPageSize(int pageSize)
    {
        // Arrange
        AddressListQuery query = _emptyQuery with { PageSize = pageSize };

        // Act
        ValidationResult result = _validator.Validate(query);

        // Assert
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(AddressListQuery.PageSize));
    }

    [TestCase("lastName")]
    [TestCase("firstName")]
    [TestCase("city")]
    [TestCase("postalCode")]
    [TestCase("country")]
    [TestCase("createdAt")]
    [TestCase("updatedAt")]
    [TestCase("LastName")]
    [TestCase("CREATEDAT")]
    [TestCase("postalcode")]
    public void Validate_KnownSortBy_ReturnsNoErrors(string sortBy)
    {
        // Arrange
        AddressListQuery query = _emptyQuery with { SortBy = sortBy };

        // Act
        ValidationResult result = _validator.Validate(query);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [TestCase("id")]
    [TestCase("street")]
    [TestCase("email")]
    [TestCase("version")]
    [TestCase("last_name")]
    [TestCase("1")]
    [TestCase("lastName,city")]
    public void Validate_UnknownSortBy_ReturnsSingleErrorWithAllowedValues(string sortBy)
    {
        // Arrange
        AddressListQuery query = _emptyQuery with { SortBy = sortBy };

        // Act
        ValidationResult result = _validator.Validate(query);

        // Assert
        ValidationFailure error = result.Errors.Should().ContainSingle().Subject;
        error.PropertyName.Should().Be(nameof(AddressListQuery.SortBy));
        error.ErrorMessage.Should().Be(SortByError);
    }

    [TestCase("asc")]
    [TestCase("desc")]
    [TestCase("ASC")]
    [TestCase("Desc")]
    public void Validate_KnownSortDirection_ReturnsNoErrors(string sortDirection)
    {
        // Arrange
        AddressListQuery query = _emptyQuery with { SortDirection = sortDirection };

        // Act
        ValidationResult result = _validator.Validate(query);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [TestCase("ascending")]
    [TestCase("descending")]
    [TestCase("up")]
    [TestCase("1")]
    [TestCase("asc,desc")]
    public void Validate_UnknownSortDirection_ReturnsSingleErrorWithAllowedValues(string sortDirection)
    {
        // Arrange
        AddressListQuery query = _emptyQuery with { SortDirection = sortDirection };

        // Act
        ValidationResult result = _validator.Validate(query);

        // Assert
        ValidationFailure error = result.Errors.Should().ContainSingle().Subject;
        error.PropertyName.Should().Be(nameof(AddressListQuery.SortDirection));
        error.ErrorMessage.Should().Be(SortDirectionError);
    }

    [Test]
    public void Validate_LongFilters_ReturnsNoErrors()
    {
        // Arrange
        var query = new AddressListQuery(new string('a', 1000), new string('1', 1000));

        // Act
        ValidationResult result = _validator.Validate(query);

        // Assert
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_AllValuesInvalid_ReturnsOneErrorForEachValue()
    {
        // Arrange
        var query = new AddressListQuery(null, null, 0, 101, "email", "up");

        // Act
        ValidationResult result = _validator.Validate(query);

        // Assert
        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            nameof(AddressListQuery.Page),
            nameof(AddressListQuery.PageSize),
            nameof(AddressListQuery.SortBy),
            nameof(AddressListQuery.SortDirection));
    }
}
