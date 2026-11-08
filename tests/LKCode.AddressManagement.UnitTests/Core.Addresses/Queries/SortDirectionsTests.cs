using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Queries;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses.Queries;

public class SortDirectionsTests
{
    [Test]
    public void Names_AllDirections_AreAscAndDescInOrderOfEnum()
    {
        // Act
        IReadOnlyList<string> names = SortDirections.Names;

        // Assert
        names.Should().Equal("asc", "desc");
    }

    [Test]
    public void Names_AllDirections_AreNamesOfAllEnumValues()
    {
        // Act
        IEnumerable<string> names = Enum.GetValues<SortDirection>().Select(SortDirections.GetName);

        // Assert
        names.Should().Equal(SortDirections.Names);
    }

    [TestCase(SortDirection.Ascending, "asc")]
    [TestCase(SortDirection.Descending, "desc")]
    public void GetName_Direction_ReturnsName(SortDirection direction, string expectedName)
    {
        // Act
        string name = SortDirections.GetName(direction);

        // Assert
        name.Should().Be(expectedName);
    }

    [TestCase(-1)]
    [TestCase(2)]
    public void GetName_UndefinedDirection_ThrowsArgumentOutOfRangeException(int value)
    {
        // Arrange
        var direction = (SortDirection)value;

        // Act
        Action act = () => SortDirections.GetName(direction);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("direction");
    }

    [TestCase("asc", SortDirection.Ascending)]
    [TestCase("desc", SortDirection.Descending)]
    [TestCase("ASC", SortDirection.Ascending)]
    [TestCase("Asc", SortDirection.Ascending)]
    [TestCase("DESC", SortDirection.Descending)]
    [TestCase("dEsC", SortDirection.Descending)]
    public void TryParse_NameInAnyCase_ReturnsDirection(string name, SortDirection expectedDirection)
    {
        // Act
        bool result = SortDirections.TryParse(name, out SortDirection direction);

        // Assert
        result.Should().BeTrue();
        direction.Should().Be(expectedDirection);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase(" asc")]
    [TestCase("desc ")]
    [TestCase("ascending")]
    [TestCase("descending")]
    [TestCase("Ascending")]
    [TestCase("up")]
    [TestCase("down")]
    [TestCase("+")]
    [TestCase("-")]
    [TestCase("0")]
    [TestCase("1")]
    [TestCase("asc,desc")]
    public void TryParse_UnknownOrMalformedName_ReturnsFalse(string? name)
    {
        // Act
        bool result = SortDirections.TryParse(name, out SortDirection direction);

        // Assert
        result.Should().BeFalse();
        direction.Should().Be(default(SortDirection));
    }
}
