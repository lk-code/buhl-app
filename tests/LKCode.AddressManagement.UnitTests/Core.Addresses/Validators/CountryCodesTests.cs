using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Validators;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses.Validators;

public class CountryCodesTests
{
    [TestCase("DE")]
    [TestCase("AT")]
    [TestCase("CH")]
    [TestCase("FR")]
    [TestCase("GB")]
    [TestCase("US")]
    [TestCase("AD")]
    [TestCase("ZW")]
    [TestCase("AX")]
    [TestCase("AQ")]
    [TestCase("BV")]
    [TestCase("CW")]
    [TestCase("SS")]
    public void IsValid_AssignedCode_ReturnsTrue(string code)
    {
        // Act
        bool result = CountryCodes.IsValid(code);

        // Assert
        result.Should().BeTrue();
    }

    [TestCase("GER")]
    [TestCase("D")]
    [TestCase("de")]
    [TestCase("De")]
    [TestCase("de1")]
    [TestCase("D1")]
    [TestCase("XX")]
    [TestCase("ZZ")]
    [TestCase("AA")]
    [TestCase("UK")]
    [TestCase("EU")]
    [TestCase("XK")]
    [TestCase("DE ")]
    [TestCase(" DE")]
    [TestCase("ÄÖ")]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase(null)]
    public void IsValid_UnassignedOrMalformedCode_ReturnsFalse(string? code)
    {
        // Act
        bool result = CountryCodes.IsValid(code);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void IsValid_AllTwoLetterCombinations_Accepts249Codes()
    {
        // Arrange
        IEnumerable<string> combinations =
            from first in Enumerable.Range('A', 26)
            from second in Enumerable.Range('A', 26)
            select $"{(char)first}{(char)second}";

        // Act
        int count = combinations.Count(CountryCodes.IsValid);

        // Assert
        count.Should().Be(249);
    }
}
