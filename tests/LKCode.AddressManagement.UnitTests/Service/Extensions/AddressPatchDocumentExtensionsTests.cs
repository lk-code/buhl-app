using System.Text.Json;
using AwesomeAssertions;
using FluentValidation.Results;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Service.Extensions;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson.Operations;

namespace LKCode.AddressManagement.UnitTests.Service.Extensions;

public class AddressPatchDocumentExtensionsTests
{
    private const string OperationError =
        "The operation must be 'add', 'remove', 'replace', 'move', 'copy' or 'test'.";

    private const string PathError = "The path must point to a field of the address, e.g. '/firstName'.";
    private const string FromError =
        "The location to move or copy from must point to a field of the address, e.g. '/firstName'.";
    private const string ValueError = "The value must be a string or null.";

    private static readonly UpdateAddressRequest _request = new(
        "Erika",
        "Mustermann",
        "Heidestraße",
        "17",
        "51147",
        "Köln",
        "DE",
        "erika.mustermann@example.com");

    [TestCase("""{"op": "add", "path": "/email", "value": "erika@example.com"}""")]
    [TestCase("""{"op": "add", "path": "/firstName", "value": "Max"}""")]
    [TestCase("""{"op": "remove", "path": "/email"}""")]
    [TestCase("""{"op": "remove", "path": "/firstName"}""")]
    [TestCase("""{"op": "replace", "path": "/lastName", "value": "Musterfrau"}""")]
    [TestCase("""{"op": "replace", "path": "/street", "value": "   "}""")]
    [TestCase("""{"op": "replace", "path": "/houseNumber", "value": ""}""")]
    [TestCase("""{"op": "replace", "path": "/postalCode", "value": null}""")]
    [TestCase("""{"op": "replace", "path": "/city"}""")]
    [TestCase("""{"op": "move", "from": "/email", "path": "/lastName"}""")]
    [TestCase("""{"op": "copy", "from": "/lastName", "path": "/firstName"}""")]
    [TestCase("""{"op": "test", "path": "/country", "value": "DE"}""")]
    [TestCase("""{"op": "test", "path": "/email", "value": null}""")]
    public void ValidateOperations_ValidOperation_IsValid(string operation)
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument = Parse(operation);

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void ValidateOperations_EveryField_IsValid()
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument = Parse(
            """{"op": "replace", "path": "/firstName", "value": "Max"}""",
            """{"op": "replace", "path": "/lastName", "value": "Musterfrau"}""",
            """{"op": "replace", "path": "/street", "value": "Hauptstraße"}""",
            """{"op": "replace", "path": "/houseNumber", "value": "4a"}""",
            """{"op": "replace", "path": "/postalCode", "value": "10115"}""",
            """{"op": "replace", "path": "/city", "value": "Berlin"}""",
            """{"op": "replace", "path": "/country", "value": "DE"}""",
            """{"op": "replace", "path": "/email", "value": "max@example.com"}""");

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void ValidateOperations_NoOperations_IsValid()
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument = Parse();

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [TestCase("""{"op": "foo", "path": "/firstName", "value": "Max"}""")]
    [TestCase("""{"op": "REPLACE", "path": "/firstName", "value": "Max"}""")]
    [TestCase("""{"op": "Test", "path": "/firstName", "value": "Max"}""")]
    [TestCase("""{"op": " add", "path": "/firstName", "value": "Max"}""")]
    [TestCase("""{"op": "", "path": "/firstName", "value": "Max"}""")]
    [TestCase("""{"op": null, "path": "/firstName", "value": "Max"}""")]
    [TestCase("""{"path": "/firstName", "value": "Max"}""")]
    public void ValidateOperations_InvalidOperation_ReturnsOperationError(string operation)
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument = Parse(operation);

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        AssertSingleError(result, "$[0].op", OperationError);
    }

    [TestCase("add")]
    [TestCase("remove")]
    [TestCase("replace")]
    [TestCase("move")]
    [TestCase("copy")]
    [TestCase("test")]
    public void ValidateOperations_MissingPath_ReturnsPathError(string op)
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            Parse($$"""{"op": "{{op}}", "from": "/lastName", "value": "Max"}""");

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        AssertSingleError(result, "$[0].path", PathError);
    }

    [TestCase("add", "")]
    [TestCase("add", "/")]
    [TestCase("add", "/unknown")]
    [TestCase("remove", "firstName")]
    [TestCase("remove", "/FirstName")]
    [TestCase("replace", "/id")]
    [TestCase("replace", "/createdAt")]
    [TestCase("replace", "/updatedAt")]
    [TestCase("replace", "/firstName/0")]
    [TestCase("replace", "/firstName/")]
    [TestCase("replace", "//firstName")]
    [TestCase("replace", "/first~1name")]
    [TestCase("replace", "/first~2name")]
    [TestCase("replace", "/email ")]
    [TestCase("replace", "/-")]
    [TestCase("move", "/normalizedCity")]
    [TestCase("copy", "/EMAIL")]
    [TestCase("test", "/unknown")]
    public void ValidateOperations_PathOfNoField_ReturnsPathError(string op, string path)
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            Parse($$"""{"op": "{{op}}", "path": "{{path}}", "from": "/lastName", "value": "Max"}""");

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        AssertSingleError(result, "$[0].path", PathError);
    }

    [TestCase("move")]
    [TestCase("copy")]
    public void ValidateOperations_MissingFrom_ReturnsFromError(string op)
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument = Parse($$"""{"op": "{{op}}", "path": "/firstName"}""");

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        AssertSingleError(result, "$[0].from", FromError);
    }

    [TestCase("move", "")]
    [TestCase("move", "/unknown")]
    [TestCase("move", "/LastName")]
    [TestCase("copy", "lastName")]
    [TestCase("copy", "/id")]
    public void ValidateOperations_FromOfNoField_ReturnsFromError(string op, string from)
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            Parse($$"""{"op": "{{op}}", "from": "{{from}}", "path": "/firstName"}""");

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        AssertSingleError(result, "$[0].from", FromError);
    }

    [TestCase("add")]
    [TestCase("remove")]
    [TestCase("replace")]
    [TestCase("test")]
    public void ValidateOperations_FromOfOtherOperation_IsIgnored(string op)
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            Parse($$"""{"op": "{{op}}", "from": "/unknown", "path": "/email", "value": "erika@example.com"}""");

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [TestCase("add", "42")]
    [TestCase("add", "true")]
    [TestCase("replace", "false")]
    [TestCase("replace", "1.5")]
    [TestCase("replace", "{}")]
    [TestCase("replace", """{"value": "Max"}""")]
    [TestCase("test", "[]")]
    [TestCase("test", """["Max"]""")]
    [TestCase("test", "0")]
    public void ValidateOperations_ValueOfNoString_ReturnsValueError(string op, string value)
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            Parse($$"""{"op": "{{op}}", "path": "/firstName", "value": {{value}}}""");

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        AssertSingleError(result, "$[0].value", ValueError);
    }

    [TestCase("remove")]
    [TestCase("move")]
    [TestCase("copy")]
    public void ValidateOperations_ValueOfOtherOperation_IsIgnored(string op)
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            Parse($$"""{"op": "{{op}}", "from": "/email", "path": "/lastName", "value": 42}""");

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void ValidateOperations_OperationsCreatedInCode_AcceptsStringAndNullValues()
    {
        // Arrange
        var patchDocument = new JsonPatchDocument<UpdateAddressRequest>(
            [
                new Operation<UpdateAddressRequest>("replace", "/firstName", from: null, value: "Max"),
                new Operation<UpdateAddressRequest>("replace", "/email", from: null, value: null),
            ],
            JsonSerializerOptions.Web);

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void ValidateOperations_OperationCreatedInCodeWithNumber_ReturnsValueError()
    {
        // Arrange
        var patchDocument = new JsonPatchDocument<UpdateAddressRequest>(
            [new Operation<UpdateAddressRequest>("replace", "/firstName", from: null, value: 42)],
            JsonSerializerOptions.Web);

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        AssertSingleError(result, "$[0].value", ValueError);
    }

    [Test]
    public void ValidateOperations_InvalidOperation_DoesNotCheckFromAndValue()
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            Parse("""{"op": "foo", "from": "/unknown", "path": "/firstName", "value": 42}""");

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        AssertSingleError(result, "$[0].op", OperationError);
    }

    [Test]
    public void ValidateOperations_SeveralInvalidOperations_ReturnsAllErrorsByIndex()
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument = Parse(
            """{"op": "replace", "path": "/firstName", "value": "Max"}""",
            """{"op": "foo", "path": "/unknown"}""",
            """{"op": "copy", "path": "/lastName"}""",
            """{"op": "test", "path": "/city", "value": 42}""");

        // Act
        ValidationResult result = patchDocument.ValidateOperations();

        // Assert
        result.Errors.Select(error => (error.PropertyName, error.ErrorMessage)).Should().Equal(
            ("$[1].op", OperationError),
            ("$[1].path", PathError),
            ("$[2].from", FromError),
            ("$[3].value", ValueError));
    }

    [Test]
    public void ValidateOperations_NullPatchDocument_ThrowsArgumentNullException()
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument = null!;

        // Act
        Action act = () => patchDocument.ValidateOperations();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("patchDocument");
    }

    [Test]
    public void TryApplyTo_Replace_ChangesValue()
    {
        // Arrange
        UpdateAddressRequest request = _request with { };
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            Parse("""{"op": "replace", "path": "/firstName", "value": "Max"}""");

        // Act
        bool applied = patchDocument.TryApplyTo(request, out int failedTestIndex);

        // Assert
        applied.Should().BeTrue();
        failedTestIndex.Should().Be(-1);
        request.Should().Be(_request with { FirstName = "Max" });
    }

    [Test]
    public void TryApplyTo_Add_ChangesValue()
    {
        // Arrange
        UpdateAddressRequest request = _request with { Email = null };
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            Parse("""{"op": "add", "path": "/email", "value": "erika@example.com"}""");

        // Act
        bool applied = patchDocument.TryApplyTo(request, out _);

        // Assert
        applied.Should().BeTrue();
        request.Should().Be(_request with { Email = "erika@example.com" });
    }

    [TestCase("/email")]
    [TestCase("/city")]
    public void TryApplyTo_Remove_SetsValueToNull(string path)
    {
        // Arrange
        UpdateAddressRequest request = _request with { };
        JsonPatchDocument<UpdateAddressRequest> patchDocument = Parse($$"""{"op": "remove", "path": "{{path}}"}""");

        // Act
        bool applied = patchDocument.TryApplyTo(request, out _);

        // Assert
        applied.Should().BeTrue();
        request.Should().Be(path == "/email" ? _request with { Email = null } : _request with { City = null! });
    }

    [Test]
    public void TryApplyTo_Copy_CopiesValue()
    {
        // Arrange
        UpdateAddressRequest request = _request with { };
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            Parse("""{"op": "copy", "from": "/lastName", "path": "/firstName"}""");

        // Act
        bool applied = patchDocument.TryApplyTo(request, out _);

        // Assert
        applied.Should().BeTrue();
        request.Should().Be(_request with { FirstName = "Mustermann" });
    }

    [Test]
    public void TryApplyTo_Move_MovesValue()
    {
        // Arrange
        UpdateAddressRequest request = _request with { };
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            Parse("""{"op": "move", "from": "/email", "path": "/lastName"}""");

        // Act
        bool applied = patchDocument.TryApplyTo(request, out _);

        // Assert
        applied.Should().BeTrue();
        request.Should().Be(_request with { LastName = "erika.mustermann@example.com", Email = null });
    }

    [Test]
    public void TryApplyTo_SuccessfulTests_AppliesAllOperations()
    {
        // Arrange
        UpdateAddressRequest request = _request with { };
        JsonPatchDocument<UpdateAddressRequest> patchDocument = Parse(
            """{"op": "test", "path": "/firstName", "value": "Erika"}""",
            """{"op": "replace", "path": "/firstName", "value": "Max"}""",
            """{"op": "test", "path": "/firstName", "value": "Max"}""",
            """{"op": "replace", "path": "/email", "value": null}""",
            """{"op": "test", "path": "/email", "value": null}""");

        // Act
        bool applied = patchDocument.TryApplyTo(request, out int failedTestIndex);

        // Assert
        applied.Should().BeTrue();
        failedTestIndex.Should().Be(-1);
        request.Should().Be(_request with { FirstName = "Max", Email = null });
    }

    [Test]
    public void TryApplyTo_NoOperations_KeepsValues()
    {
        // Arrange
        UpdateAddressRequest request = _request with { };

        // Act
        bool applied = Parse().TryApplyTo(request, out int failedTestIndex);

        // Assert
        applied.Should().BeTrue();
        failedTestIndex.Should().Be(-1);
        request.Should().Be(_request);
    }

    [TestCase("""{"op": "test", "path": "/firstName", "value": "Max"}""")]
    [TestCase("""{"op": "test", "path": "/firstName", "value": "erika"}""")]
    [TestCase("""{"op": "test", "path": "/firstName", "value": "Erika "}""")]
    [TestCase("""{"op": "test", "path": "/firstName", "value": null}""")]
    [TestCase("""{"op": "test", "path": "/email", "value": null}""")]
    [TestCase("""{"op": "test", "path": "/email"}""")]
    public void TryApplyTo_FailedTest_ReturnsFalseWithIndex(string test)
    {
        // Arrange
        UpdateAddressRequest request = _request with { };
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            Parse("""{"op": "replace", "path": "/lastName", "value": "Musterfrau"}""", test);

        // Act
        bool applied = patchDocument.TryApplyTo(request, out int failedTestIndex);

        // Assert
        applied.Should().BeFalse();
        failedTestIndex.Should().Be(1);
    }

    [Test]
    public void TryApplyTo_SeveralFailedTests_ReturnsIndexOfFirst()
    {
        // Arrange
        UpdateAddressRequest request = _request with { };
        JsonPatchDocument<UpdateAddressRequest> patchDocument = Parse(
            """{"op": "test", "path": "/firstName", "value": "Erika"}""",
            """{"op": "test", "path": "/lastName", "value": "Musterfrau"}""",
            """{"op": "test", "path": "/city", "value": "Berlin"}""");

        // Act
        bool applied = patchDocument.TryApplyTo(request, out int failedTestIndex);

        // Assert
        applied.Should().BeFalse();
        failedTestIndex.Should().Be(1);
    }

    [Test]
    public void TryApplyTo_FailedOperationOtherThanTest_ThrowsInvalidOperationException()
    {
        // Arrange
        UpdateAddressRequest request = _request with { };
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            Parse("""{"op": "replace", "path": "/unknown", "value": "Max"}""");

        // Act
        Action act = () => patchDocument.TryApplyTo(request, out _);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*operation 0*replace*");
    }

    [Test]
    public void TryApplyTo_NullPatchDocument_ThrowsArgumentNullException()
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument = null!;

        // Act
        Action act = () => patchDocument.TryApplyTo(_request with { }, out _);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("patchDocument");
    }

    [Test]
    public void TryApplyTo_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument = Parse();

        // Act
        Action act = () => patchDocument.TryApplyTo(null!, out _);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("request");
    }

    private static JsonPatchDocument<UpdateAddressRequest> Parse(params string[] operations) =>
        JsonSerializer.Deserialize<JsonPatchDocument<UpdateAddressRequest>>(
            $"[{string.Join(", ", operations)}]",
            JsonSerializerOptions.Web)!;

    private static void AssertSingleError(ValidationResult result, string propertyName, string errorMessage)
    {
        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => (error.PropertyName, error.ErrorMessage))
            .Should().Equal((propertyName, errorMessage));
    }
}
