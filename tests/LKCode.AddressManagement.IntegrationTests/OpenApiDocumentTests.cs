using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace LKCode.AddressManagement.IntegrationTests;

/// <summary>
/// Tests the rules that apply to the whole OpenAPI document: it is valid, describes exactly the endpoints of the API
/// and describes all of their responses, headers and schemas consistently.
/// </summary>
public class OpenApiDocumentTests
{
    private const string OpenApiDocumentPath = "/openapi/v1.json";
    private const string JsonMediaType = "application/json";
    private const string ProblemDetailsMediaType = "application/problem+json";
    private const string SchemaReferencePrefix = "#/components/schemas/";

    private static readonly (string Path, string Method)[] _operations =
    [
        ("/api/addresses", "get"),
        ("/api/addresses", "post"),
        ("/api/addresses/{id}", "get"),
        ("/api/addresses/{id}", "put"),
        ("/api/addresses/{id}", "delete"),
    ];

    private string _json = string.Empty;
    private JsonElement _document;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await using var factory = new AddressManagementWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        _json = await client.GetStringAsync(OpenApiDocumentPath);
        _document = JsonSerializer.Deserialize<JsonElement>(_json);
    }

    [Test]
    public async Task Document_ReadByOpenApiReader_IsValidOpenApi31WithoutWarnings()
    {
        // Arrange
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(_json));

        // Act
        ReadResult result = await OpenApiDocument.LoadAsync(stream, "json");

        // Assert
        OpenApiDiagnostic? diagnostic = result.Diagnostic;
        diagnostic.Should().NotBeNull();
        diagnostic.Errors.Select(error => error.ToString()).Should().BeEmpty();
        diagnostic.Warnings.Select(warning => warning.ToString()).Should().BeEmpty();
        diagnostic.SpecificationVersion.Should().Be(OpenApiSpecVersion.OpenApi3_1);
    }

    [Test]
    public void Paths_Operations_AreExactlyTheAddressEndpoints()
    {
        // Act
        IEnumerable<string> operations = _document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => GetNames(path.Value).Select(method => $"{method} {path.Name}"));

        // Assert
        operations.Should().BeEquivalentTo(_operations.Select(operation => $"{operation.Method} {operation.Path}"));
    }

    [Test]
    public void Operations_OperationIds_AreUnique()
    {
        // Act
        string?[] operationIds =
        [
            .. _operations.Select(operation =>
                GetOperation(operation.Path, operation.Method).GetProperty("operationId").GetString()),
        ];

        // Assert
        operationIds.Should().OnlyHaveUniqueItems()
            .And.AllSatisfy(operationId => operationId.Should().NotBeNullOrWhiteSpace());
    }

    [TestCaseSource(nameof(Operations))]
    public void Operation_Documentation_HasSummaryDescriptionAndAddressesTag(string path, string method)
    {
        // Act
        JsonElement operation = GetOperation(path, method);

        // Assert
        operation.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
        operation.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        operation.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).Should().Equal("Addresses");
    }

    [TestCaseSource(nameof(Operations))]
    public void Operation_Responses_ContainInternalServerError(string path, string method)
    {
        // Act
        JsonElement responses = GetOperation(path, method).GetProperty("responses");

        // Assert
        GetNames(responses).Should().Contain("500");
    }

    [TestCaseSource(nameof(Operations))]
    public void Operation_Responses_HaveDescription(string path, string method)
    {
        // Act
        JsonElement responses = GetOperation(path, method).GetProperty("responses");

        // Assert
        responses.EnumerateObject().Should().AllSatisfy(response =>
            response.Value.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace());
    }

    [TestCaseSource(nameof(Operations))]
    public void Operation_ErrorResponses_HaveOnlyProblemDetails(string path, string method)
    {
        // Act
        JsonProperty[] errorResponses =
        [
            .. GetOperation(path, method).GetProperty("responses").EnumerateObject()
                .Where(response => IsErrorStatusCode(response.Name)),
        ];

        // Assert
        errorResponses.Should().NotBeEmpty().And.AllSatisfy(response =>
        {
            string schema = response.Name == "400" ? "ValidationProblemDetails" : "ProblemDetails";
            JsonElement content = response.Value.GetProperty("content");
            GetNames(content).Should().Equal(ProblemDetailsMediaType);
            GetSchemaReference(content.GetProperty(ProblemDetailsMediaType))
                .Should().Be(SchemaReferencePrefix + schema);
        });
    }

    [TestCaseSource(nameof(Operations))]
    public void Operation_SuccessResponsesWithContent_HaveOnlyJson(string path, string method)
    {
        // Act
        JsonElement[] contents =
        [
            .. GetOperation(path, method).GetProperty("responses").EnumerateObject()
                .Where(response => !IsErrorStatusCode(response.Name))
                .Select(response => response.Value.TryGetProperty("content", out JsonElement content)
                    ? content
                    : default)
                .Where(content => content.ValueKind != JsonValueKind.Undefined),
        ];

        // Assert
        contents.Should().AllSatisfy(content => GetNames(content).Should().Equal(JsonMediaType));
    }

    [Test]
    public void Responses_CreatedOrWithHeaders_AreOnlyCreatedAddressWithLocationHeader()
    {
        // Act
        IEnumerable<string> responses = _operations.SelectMany(operation =>
            GetOperation(operation.Path, operation.Method).GetProperty("responses").EnumerateObject()
                .Where(response => response.Name == "201" || response.Value.TryGetProperty("headers", out _))
                .Select(response => $"{operation.Method} {operation.Path} {response.Name}: "
                    + string.Join(", ", GetHeaderNames(response.Value))));

        // Assert
        responses.Should().Equal("post /api/addresses 201: Location");
    }

    [Test]
    public void Components_Schemas_AreExactlyTheSchemasOfTheApi()
    {
        // Act
        IEnumerable<string> schemas = GetNames(_document.GetProperty("components").GetProperty("schemas"));

        // Assert
        schemas.Should().BeEquivalentTo(
            "AddressResponse",
            "CreateAddressRequest",
            "UpdateAddressRequest",
            "ProblemDetails",
            "ValidationProblemDetails");
    }

    [Test]
    public void Document_SchemaReferences_ReferToEveryComponentSchemaAndNothingElse()
    {
        // Act
        IEnumerable<string?> references = GetReferences(_document).Distinct();

        // Assert
        references.Should().BeEquivalentTo(
            GetNames(_document.GetProperty("components").GetProperty("schemas"))
                .Select(name => SchemaReferencePrefix + name));
    }

    private static IEnumerable<TestCaseData> Operations() =>
        _operations.Select(operation => new TestCaseData(operation.Path, operation.Method)
            .SetArgDisplayNames(operation.Method, operation.Path));

    private static bool IsErrorStatusCode(string statusCode) =>
        statusCode.StartsWith('4') || statusCode.StartsWith('5');

    private static IEnumerable<string> GetHeaderNames(JsonElement response) =>
        response.TryGetProperty("headers", out JsonElement headers) ? GetNames(headers) : [];

    private static IEnumerable<string?> GetReferences(JsonElement element)
    {
        IEnumerable<string?> references =
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty("$ref", out JsonElement reference)
                ? [reference.GetString()]
                : [];
        IEnumerable<JsonElement> children = element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .Where(property => property.Name != "$ref")
                .Select(property => property.Value),
            JsonValueKind.Array => element.EnumerateArray(),
            _ => [],
        };

        return references.Concat(children.SelectMany(GetReferences));
    }

    private static IEnumerable<string> GetNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name);

    private static string? GetSchemaReference(JsonElement mediaType) =>
        mediaType.GetProperty("schema").GetProperty("$ref").GetString();

    private JsonElement GetOperation(string path, string method) =>
        _document.GetProperty("paths").GetProperty(path).GetProperty(method);
}
