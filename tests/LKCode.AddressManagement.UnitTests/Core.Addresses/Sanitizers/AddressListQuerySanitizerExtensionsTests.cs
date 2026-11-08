using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Sanitizers;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses.Sanitizers;

public class AddressListQuerySanitizerExtensionsTests
{
    [Test]
    public void Sanitize_CleanValues_KeepsAllValues()
    {
        // Arrange
        var query = new AddressListQuery("Köln", "51147", 2, 50, "city", "desc");

        // Act
        AddressListQuery result = query.Sanitize();

        // Assert
        result.Should().Be(query);
    }

    [Test]
    public void Sanitize_SurroundingWhitespace_TrimsAllTexts()
    {
        // Arrange
        var query = new AddressListQuery(" Köln\t", "\n51147 ", 2, 50, "  city ", "\tdesc\r\n");

        // Act
        AddressListQuery result = query.Sanitize();

        // Assert
        result.Should().Be(new AddressListQuery("Köln", "51147", 2, 50, "city", "desc"));
    }

    [Test]
    public void Sanitize_InnerWhitespace_KeepsInnerWhitespace()
    {
        // Arrange
        var query = new AddressListQuery("Bad  Homburg", "SW1A 1AA", null, null, "last name", "de sc");

        // Act
        AddressListQuery result = query.Sanitize();

        // Assert
        result.Should().Be(query);
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t\n")]
    public void Sanitize_BlankTexts_ReturnsNullForAllTexts(string blank)
    {
        // Arrange
        var query = new AddressListQuery(blank, blank, 3, 10, blank, blank);

        // Act
        AddressListQuery result = query.Sanitize();

        // Assert
        result.Should().Be(new AddressListQuery(null, null, 3, 10, null, null));
    }

    [Test]
    public void Sanitize_MissingValues_KeepsMissingValues()
    {
        // Arrange
        var query = new AddressListQuery(null, null);

        // Act
        AddressListQuery result = query.Sanitize();

        // Assert
        result.Should().Be(query);
    }

    [TestCase(0, 0)]
    [TestCase(-1, 101)]
    [TestCase(int.MinValue, int.MaxValue)]
    public void Sanitize_InvalidNumbers_KeepsNumbersForValidation(int page, int pageSize)
    {
        // Arrange
        var query = new AddressListQuery(null, null, page, pageSize);

        // Act
        AddressListQuery result = query.Sanitize();

        // Assert
        result.Page.Should().Be(page);
        result.PageSize.Should().Be(pageSize);
    }

    [Test]
    public void Sanitize_NameInOtherCase_KeepsCase()
    {
        // Arrange
        var query = new AddressListQuery(null, null, SortBy: "LastName", SortDirection: "DESC");

        // Act
        AddressListQuery result = query.Sanitize();

        // Assert
        result.Should().Be(query);
    }

    [Test]
    public void Sanitize_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        AddressListQuery query = null!;

        // Act
        Action act = () => query.Sanitize();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("query");
    }
}
