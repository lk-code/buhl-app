using AwesomeAssertions;
using LKCode.AddressManagement.Service.Extensions;
using LKCode.AddressManagement.Service.Preconditions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace LKCode.AddressManagement.UnitTests.Service.Extensions;

public class HeaderDictionaryExtensionsTests
{
    private const long CurrentVersion = 3;

    [Test]
    public void EvaluateIfMatch_NoIfMatchHeader_ReturnsMissing()
    {
        // Arrange
        var headers = new HeaderDictionary { ["ETag"] = "\"3\"" };

        // Act
        IfMatchOutcome outcome = headers.EvaluateIfMatch(CurrentVersion);

        // Assert
        outcome.Should().Be(IfMatchOutcome.Missing);
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase(",")]
    [TestCase("3")]
    [TestCase("\"3")]
    [TestCase("3\"")]
    [TestCase("'3'")]
    [TestCase("\"3\" x")]
    [TestCase("\"3\"\"4\"")]
    [TestCase("W/3")]
    [TestCase("W/*")]
    [TestCase("**")]
    [TestCase("*, \"3\"")]
    [TestCase("\"3\", *")]
    [TestCase("*, *")]
    public void EvaluateIfMatch_SyntacticallyInvalidValue_ReturnsInvalid(string value)
    {
        // Arrange
        var headers = new HeaderDictionary { ["If-Match"] = value };

        // Act
        IfMatchOutcome outcome = headers.EvaluateIfMatch(CurrentVersion);

        // Assert
        outcome.Should().Be(IfMatchOutcome.Invalid);
    }

    [Test]
    public void EvaluateIfMatch_AsteriskInOneOfSeveralHeaderValues_ReturnsInvalid()
    {
        // Arrange
        var headers = new HeaderDictionary { ["If-Match"] = new StringValues(["\"3\"", "*"]) };

        // Act
        IfMatchOutcome outcome = headers.EvaluateIfMatch(CurrentVersion);

        // Assert
        outcome.Should().Be(IfMatchOutcome.Invalid);
    }

    [Test]
    public void EvaluateIfMatch_Asterisk_ReturnsAny()
    {
        // Arrange
        var headers = new HeaderDictionary { ["If-Match"] = "*" };

        // Act
        IfMatchOutcome outcome = headers.EvaluateIfMatch(CurrentVersion);

        // Assert
        outcome.Should().Be(IfMatchOutcome.Any);
    }

    [TestCase("\"3\"")]
    [TestCase(" \"3\" ")]
    [TestCase("\"2\", \"3\"")]
    [TestCase("\"3\",\"4\"")]
    [TestCase("W/\"3\", \"3\"")]
    [TestCase("\"1\", \"2\", \"3\", \"4\"")]
    public void EvaluateIfMatch_StrongEntityTagOfCurrentVersion_ReturnsMatched(string value)
    {
        // Arrange
        var headers = new HeaderDictionary { ["If-Match"] = value };

        // Act
        IfMatchOutcome outcome = headers.EvaluateIfMatch(CurrentVersion);

        // Assert
        outcome.Should().Be(IfMatchOutcome.Matched);
    }

    [Test]
    public void EvaluateIfMatch_CurrentVersionInOneOfSeveralHeaderValues_ReturnsMatched()
    {
        // Arrange
        var headers = new HeaderDictionary { ["If-Match"] = new StringValues(["\"2\"", "\"3\""]) };

        // Act
        IfMatchOutcome outcome = headers.EvaluateIfMatch(CurrentVersion);

        // Assert
        outcome.Should().Be(IfMatchOutcome.Matched);
    }

    [TestCase("\"2\"")]
    [TestCase("\"4\"")]
    [TestCase("\"03\"")]
    [TestCase("\"3 \"")]
    [TestCase("\"\"")]
    [TestCase("\"abc\"")]
    [TestCase("W/\"3\"")]
    [TestCase("W/\"2\", W/\"3\"")]
    [TestCase("\"1\", \"2\"")]
    public void EvaluateIfMatch_NoStrongEntityTagOfCurrentVersion_ReturnsNotMatched(string value)
    {
        // Arrange
        var headers = new HeaderDictionary { ["If-Match"] = value };

        // Act
        IfMatchOutcome outcome = headers.EvaluateIfMatch(CurrentVersion);

        // Assert
        outcome.Should().Be(IfMatchOutcome.NotMatched);
    }

    [TestCase(1L, "\"1\"")]
    [TestCase(long.MaxValue, "\"9223372036854775807\"")]
    public void EvaluateIfMatch_EntityTagOfVersion_ReturnsMatched(long version, string value)
    {
        // Arrange
        var headers = new HeaderDictionary { ["If-Match"] = value };

        // Act
        IfMatchOutcome outcome = headers.EvaluateIfMatch(version);

        // Assert
        outcome.Should().Be(IfMatchOutcome.Matched);
    }

    [Test]
    public void EvaluateIfMatch_NullHeaders_ThrowsArgumentNullException()
    {
        // Arrange
        IHeaderDictionary headers = null!;

        // Act
        Action act = () => headers.EvaluateIfMatch(CurrentVersion);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("headers");
    }

    [TestCase(1L, "\"1\"")]
    [TestCase(3L, "\"3\"")]
    [TestCase(long.MaxValue, "\"9223372036854775807\"")]
    public void SetETag_Version_SetsStrongEntityTagWithVersion(long version, string expected)
    {
        // Arrange
        IHeaderDictionary headers = new HeaderDictionary();

        // Act
        headers.SetETag(version);

        // Assert
        headers.ETag.ToString().Should().Be(expected);
    }

    [Test]
    public void SetETag_ExistingETag_ReplacesETag()
    {
        // Arrange
        IHeaderDictionary headers = new HeaderDictionary { ["ETag"] = "\"2\"" };

        // Act
        headers.SetETag(3);

        // Assert
        headers.ETag.Should().Equal("\"3\"");
    }

    [Test]
    public void SetETag_Version_IsMatchedByIfMatchWithSameValue()
    {
        // Arrange
        IHeaderDictionary responseHeaders = new HeaderDictionary();
        responseHeaders.SetETag(CurrentVersion);
        var requestHeaders = new HeaderDictionary { ["If-Match"] = responseHeaders.ETag };

        // Act
        IfMatchOutcome outcome = requestHeaders.EvaluateIfMatch(CurrentVersion);

        // Assert
        outcome.Should().Be(IfMatchOutcome.Matched);
    }

    [Test]
    public void SetETag_NullHeaders_ThrowsArgumentNullException()
    {
        // Arrange
        IHeaderDictionary headers = null!;

        // Act
        Action act = () => headers.SetETag(CurrentVersion);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("headers");
    }
}
