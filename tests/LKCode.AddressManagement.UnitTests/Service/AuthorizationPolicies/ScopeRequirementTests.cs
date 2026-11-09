using AwesomeAssertions;
using LKCode.AddressManagement.Service.AuthorizationPolicies;

namespace LKCode.AddressManagement.UnitTests.Service.AuthorizationPolicies;

public class ScopeRequirementTests
{
    [TestCase("addresses:read")]
    [TestCase("addresses:write")]
    [TestCase("a")]
    [TestCase("!#[]~")]
    [TestCase("api://address-management/addresses.read")]
    public void Constructor_ValidScope_SetsScope(string scope)
    {
        // Act
        var requirement = new ScopeRequirement(scope);

        // Assert
        requirement.Scope.Should().Be(scope);
    }

    [Test]
    public void Constructor_NullScope_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _ = new ScopeRequirement(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("scope");
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("addresses:read addresses:write")]
    [TestCase(" addresses:read")]
    [TestCase("addresses:read ")]
    [TestCase("addresses:\tread")]
    [TestCase("addresses:read\n")]
    [TestCase("\"addresses:read\"")]
    [TestCase("addresses\\read")]
    [TestCase("adressen:ändern")]
    public void Constructor_InvalidScope_ThrowsArgumentException(string value)
    {
        // Act
        Action act = () => _ = new ScopeRequirement(value);

        // Assert
        act.Should().ThrowExactly<ArgumentException>().WithParameterName("scope");
    }

    [Test]
    public void ToString_Requirement_NamesRequiredScope()
    {
        // Arrange
        var requirement = new ScopeRequirement("addresses:read");

        // Act
        string text = requirement.ToString();

        // Assert
        text.Should().Be("ScopeRequirement:Scope=addresses:read");
    }
}
