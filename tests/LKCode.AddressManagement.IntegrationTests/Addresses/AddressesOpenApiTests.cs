using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// Tests the description of the address endpoints in the OpenAPI document.
/// </summary>
public class AddressesOpenApiTests
{
    private const string OpenApiDocumentPath = "/openapi/v1.json";
    private const string JsonMediaType = "application/json";
    private const string ProblemDetailsMediaType = "application/problem+json";
    private const string SchemaReferencePrefix = "#/components/schemas/";

    private JsonElement _document;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await using var factory = new AddressManagementWebApplicationFactory();
        using HttpClient client = factory.CreateClient();
        _document = await client.GetFromJsonAsync<JsonElement>(OpenApiDocumentPath);
    }

    [Test]
    public void PostOperation_OperationId_IsCreateAddress()
    {
        // Act
        JsonElement operation = GetPostOperation();

        // Assert
        operation.GetProperty("operationId").GetString().Should().Be("CreateAddress");
    }

    [Test]
    public void PostOperation_Responses_ContainAllStatusCodes()
    {
        // Act
        JsonElement responses = GetPostOperation().GetProperty("responses");

        // Assert
        GetNames(responses).Should().BeEquivalentTo("201", "400", "415", "500");
    }

    [TestCase("201", JsonMediaType, "AddressResponse")]
    [TestCase("400", ProblemDetailsMediaType, "ValidationProblemDetails")]
    [TestCase("415", ProblemDetailsMediaType, "ProblemDetails")]
    [TestCase("500", ProblemDetailsMediaType, "ProblemDetails")]
    public void PostOperation_Response_HasOnlyMediaTypeWithSchema(string statusCode, string mediaType, string schema)
    {
        // Act
        JsonElement content = GetPostOperation().GetProperty("responses").GetProperty(statusCode).GetProperty("content");

        // Assert
        GetNames(content).Should().Equal(mediaType);
        GetSchemaReference(content.GetProperty(mediaType)).Should().Be(SchemaReferencePrefix + schema);
    }

    [Test]
    public void PostOperation_RequestBody_IsRequiredJsonOfCreateAddressRequest()
    {
        // Act
        JsonElement requestBody = GetPostOperation().GetProperty("requestBody");

        // Assert
        requestBody.GetProperty("required").GetBoolean().Should().BeTrue();
        requestBody.GetProperty("description").GetString().Should().Be("The values of the new address.");
        JsonElement content = requestBody.GetProperty("content");
        GetNames(content).Should().BeEquivalentTo(JsonMediaType, "application/*+json");
        content.EnumerateObject().Should().AllSatisfy(mediaType =>
            GetSchemaReference(mediaType.Value).Should().Be(SchemaReferencePrefix + "CreateAddressRequest"));
    }

    [Test]
    public void GetByIdOperation_OperationId_IsGetAddressById()
    {
        // Act
        JsonElement operation = GetGetByIdOperation();

        // Assert
        operation.GetProperty("operationId").GetString().Should().Be("GetAddressById");
    }

    [Test]
    public void GetByIdOperation_Responses_ContainAllStatusCodes()
    {
        // Act
        JsonElement responses = GetGetByIdOperation().GetProperty("responses");

        // Assert
        GetNames(responses).Should().BeEquivalentTo("200", "404", "500");
    }

    [TestCase("200", JsonMediaType, "AddressResponse")]
    [TestCase("404", ProblemDetailsMediaType, "ProblemDetails")]
    [TestCase("500", ProblemDetailsMediaType, "ProblemDetails")]
    public void GetByIdOperation_Response_HasOnlyMediaTypeWithSchema(
        string statusCode,
        string mediaType,
        string schema)
    {
        // Act
        JsonElement content =
            GetGetByIdOperation().GetProperty("responses").GetProperty(statusCode).GetProperty("content");

        // Assert
        GetNames(content).Should().Equal(mediaType);
        GetSchemaReference(content.GetProperty(mediaType)).Should().Be(SchemaReferencePrefix + schema);
    }

    [Test]
    public void GetByIdOperation_IdParameter_IsRequiredUuidInPath()
    {
        // Act
        JsonElement parameter = GetGetByIdOperation().GetProperty("parameters").EnumerateArray()
            .Should().ContainSingle().Which;

        // Assert
        parameter.GetProperty("name").GetString().Should().Be("id");
        parameter.GetProperty("in").GetString().Should().Be("path");
        parameter.GetProperty("required").GetBoolean().Should().BeTrue();
        parameter.GetProperty("schema").GetProperty("type").GetString().Should().Be("string");
        parameter.GetProperty("schema").GetProperty("format").GetString().Should().Be("uuid");
    }

    [Test]
    public void ProblemDetailsSchema_Properties_AreMembersOfRfc9457()
    {
        // Act
        JsonElement schema = GetSchema("ProblemDetails");

        // Assert
        GetNames(schema.GetProperty("properties")).Should().BeEquivalentTo(
            "type",
            "title",
            "status",
            "detail",
            "instance");
    }

    [Test]
    public void ValidationProblemDetailsSchema_Errors_AreMessagesPerField()
    {
        // Act
        JsonElement schema = GetSchema("ValidationProblemDetails");

        // Assert
        GetNames(schema.GetProperty("properties")).Should().Contain(
            ["type", "title", "status", "detail", "instance", "errors"]);
        JsonElement errors = schema.GetProperty("properties").GetProperty("errors");
        errors.GetProperty("type").GetString().Should().Be("object");
        JsonElement messages = errors.GetProperty("additionalProperties");
        messages.GetProperty("type").GetString().Should().Be("array");
        messages.GetProperty("items").GetProperty("type").GetString().Should().Be("string");
    }

    private static IEnumerable<string> GetNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name);

    private static string? GetSchemaReference(JsonElement mediaType) =>
        mediaType.GetProperty("schema").GetProperty("$ref").GetString();

    private JsonElement GetPostOperation() =>
        _document.GetProperty("paths").GetProperty("/api/addresses").GetProperty("post");

    private JsonElement GetGetByIdOperation() =>
        _document.GetProperty("paths").GetProperty("/api/addresses/{id}").GetProperty("get");

    private JsonElement GetSchema(string name) =>
        _document.GetProperty("components").GetProperty("schemas").GetProperty(name);
}
