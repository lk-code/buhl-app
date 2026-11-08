using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Queries;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses.Queries;

public class AddressSortFieldsTests
{
    [Test]
    public void Names_AllFields_AreJsonNamesInOrderOfEnum()
    {
        // Act
        IReadOnlyList<string> names = AddressSortFields.Names;

        // Assert
        names.Should().Equal("lastName", "firstName", "city", "postalCode", "country", "createdAt", "updatedAt");
    }

    [Test]
    public void Names_AllFields_AreNamesOfAllEnumValues()
    {
        // Act
        IEnumerable<string> names = Enum.GetValues<AddressSortField>().Select(AddressSortFields.GetName);

        // Assert
        names.Should().Equal(AddressSortFields.Names);
    }

    [TestCase(AddressSortField.LastName, "lastName")]
    [TestCase(AddressSortField.FirstName, "firstName")]
    [TestCase(AddressSortField.City, "city")]
    [TestCase(AddressSortField.PostalCode, "postalCode")]
    [TestCase(AddressSortField.Country, "country")]
    [TestCase(AddressSortField.CreatedAt, "createdAt")]
    [TestCase(AddressSortField.UpdatedAt, "updatedAt")]
    public void GetName_Field_ReturnsJsonName(AddressSortField field, string expectedName)
    {
        // Act
        string name = AddressSortFields.GetName(field);

        // Assert
        name.Should().Be(expectedName);
    }

    [TestCase(-1)]
    [TestCase(7)]
    [TestCase(99)]
    public void GetName_UndefinedField_ThrowsArgumentOutOfRangeException(int value)
    {
        // Arrange
        var field = (AddressSortField)value;

        // Act
        Action act = () => AddressSortFields.GetName(field);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("field");
    }

    [TestCase("lastName", AddressSortField.LastName)]
    [TestCase("firstName", AddressSortField.FirstName)]
    [TestCase("city", AddressSortField.City)]
    [TestCase("postalCode", AddressSortField.PostalCode)]
    [TestCase("country", AddressSortField.Country)]
    [TestCase("createdAt", AddressSortField.CreatedAt)]
    [TestCase("updatedAt", AddressSortField.UpdatedAt)]
    public void TryParse_Name_ReturnsField(string name, AddressSortField expectedField)
    {
        // Act
        bool result = AddressSortFields.TryParse(name, out AddressSortField field);

        // Assert
        result.Should().BeTrue();
        field.Should().Be(expectedField);
    }

    [TestCase("LastName", AddressSortField.LastName)]
    [TestCase("LASTNAME", AddressSortField.LastName)]
    [TestCase("lastname", AddressSortField.LastName)]
    [TestCase("FIRSTNAME", AddressSortField.FirstName)]
    [TestCase("City", AddressSortField.City)]
    [TestCase("POSTALCODE", AddressSortField.PostalCode)]
    [TestCase("Country", AddressSortField.Country)]
    [TestCase("createdat", AddressSortField.CreatedAt)]
    [TestCase("UpdatedAt", AddressSortField.UpdatedAt)]
    public void TryParse_NameInOtherCase_ReturnsField(string name, AddressSortField expectedField)
    {
        // Act
        bool result = AddressSortFields.TryParse(name, out AddressSortField field);

        // Assert
        result.Should().BeTrue();
        field.Should().Be(expectedField);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase(" lastName")]
    [TestCase("lastName ")]
    [TestCase("last name")]
    [TestCase("last_name")]
    [TestCase("lastNames")]
    [TestCase("-lastName")]
    [TestCase("lastName,city")]
    [TestCase("lastName;city")]
    [TestCase("id")]
    [TestCase("street")]
    [TestCase("houseNumber")]
    [TestCase("email")]
    [TestCase("version")]
    [TestCase("normalizedCity")]
    [TestCase("isDeleted")]
    [TestCase("deletedAt")]
    [TestCase("0")]
    [TestCase("1")]
    [TestCase("6")]
    public void TryParse_UnknownOrMalformedName_ReturnsFalse(string? name)
    {
        // Act
        bool result = AddressSortFields.TryParse(name, out AddressSortField field);

        // Assert
        result.Should().BeFalse();
        field.Should().Be(default(AddressSortField));
    }
}
