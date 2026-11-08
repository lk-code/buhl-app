using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Service.Extensions;

namespace LKCode.AddressManagement.UnitTests.Service.Extensions;

public class JsonSerializerOptionsExtensionsTests
{
    [Test]
    public void GetPropertyJsonPointers_WebOptions_ReturnsCamelCasePointersInOrder()
    {
        // Arrange
        JsonSerializerOptions options = JsonSerializerOptions.Web;

        // Act
        IReadOnlyList<string> pointers = options.GetPropertyJsonPointers(typeof(UpdateAddressRequest));

        // Assert
        pointers.Should().Equal(
            "/firstName",
            "/lastName",
            "/street",
            "/houseNumber",
            "/postalCode",
            "/city",
            "/country",
            "/email");
    }

    [Test]
    public void GetPropertyJsonPointers_DefaultOptions_ReturnsPointersWithPropertyNames()
    {
        // Arrange
        JsonSerializerOptions options = JsonSerializerOptions.Default;

        // Act
        IReadOnlyList<string> pointers = options.GetPropertyJsonPointers(typeof(UpdateAddressRequest));

        // Assert
        pointers.Should().HaveCount(8).And.StartWith("/FirstName").And.EndWith("/Email");
    }

    [Test]
    public void GetPropertyJsonPointers_NamesWithTildeAndSlash_ReturnsEscapedPointers()
    {
        // Arrange
        JsonSerializerOptions options = JsonSerializerOptions.Web;

        // Act
        IReadOnlyList<string> pointers = options.GetPropertyJsonPointers(typeof(SpecialNames));

        // Assert
        pointers.Should().Equal("/a~1b", "/c~0d", "/~01~1");
    }

    [Test]
    public void GetPropertyJsonPointers_TypeWithoutProperties_ReturnsEmptyList()
    {
        // Arrange
        JsonSerializerOptions options = JsonSerializerOptions.Web;

        // Act
        IReadOnlyList<string> pointers = options.GetPropertyJsonPointers(typeof(NoProperties));

        // Assert
        pointers.Should().BeEmpty();
    }

    [Test]
    public void GetPropertyJsonPointers_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        JsonSerializerOptions options = null!;

        // Act
        Action act = () => options.GetPropertyJsonPointers(typeof(UpdateAddressRequest));

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Test]
    public void GetPropertyJsonPointers_NullType_ThrowsArgumentNullException()
    {
        // Arrange
        JsonSerializerOptions options = JsonSerializerOptions.Web;

        // Act
        Action act = () => options.GetPropertyJsonPointers(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("type");
    }

    private sealed record SpecialNames(
        [property: JsonPropertyName("a/b")] string Slash,
        [property: JsonPropertyName("c~d")] string Tilde,
        [property: JsonPropertyName("~1/")] string Both);

    private sealed class NoProperties;
}
