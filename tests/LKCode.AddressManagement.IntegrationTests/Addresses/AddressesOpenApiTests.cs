using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Domain.Addresses;

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
    public void PostOperation_CreatedResponse_HasRequiredLocationHeaderWithUri()
    {
        // Act
        JsonElement headers = GetPostOperation().GetProperty("responses").GetProperty("201").GetProperty("headers");

        // Assert
        GetNames(headers).Should().Equal("Location");
        JsonElement location = headers.GetProperty("Location");
        location.GetProperty("required").GetBoolean().Should().BeTrue();
        location.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        location.GetProperty("schema").GetProperty("type").GetString().Should().Be("string");
        location.GetProperty("schema").GetProperty("format").GetString().Should().Be("uri");
    }

    [TestCase("400")]
    [TestCase("415")]
    [TestCase("500")]
    public void PostOperation_ErrorResponse_HasNoHeaders(string statusCode)
    {
        // Act
        JsonElement response = GetPostOperation().GetProperty("responses").GetProperty(statusCode);

        // Assert
        response.TryGetProperty("headers", out _).Should().BeFalse();
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

    [Test]
    public void CreateAddressRequestSchema_Required_AreAllValuesExceptEmail()
    {
        // Act
        JsonElement schema = GetSchema("CreateAddressRequest");

        // Assert
        schema.GetProperty("required").EnumerateArray().Select(name => name.GetString()).Should().BeEquivalentTo(
            "firstName",
            "lastName",
            "street",
            "houseNumber",
            "postalCode",
            "city",
            "country");
    }

    [Test]
    public void AddressResponseSchema_Required_AreAllValues()
    {
        // Act
        JsonElement schema = GetSchema("AddressResponse");

        // Assert
        schema.GetProperty("required").EnumerateArray().Select(name => name.GetString()).Should().BeEquivalentTo(
            GetNames(schema.GetProperty("properties")));
        GetNames(schema.GetProperty("properties")).Should().HaveCount(11);
    }

    [TestCaseSource(nameof(RequiredValuesOfSchemas))]
    public void AddressSchema_RequiredValue_IsStringWithLengths(
        string schemaName,
        string name,
        int minLength,
        int maxLength)
    {
        // Act
        JsonElement value = GetSchema(schemaName).GetProperty("properties").GetProperty(name);

        // Assert
        GetTypes(value).Should().Equal("string");
        value.GetProperty("minLength").GetInt32().Should().Be(minLength);
        value.GetProperty("maxLength").GetInt32().Should().Be(maxLength);
    }

    [TestCase("CreateAddressRequest")]
    [TestCase("AddressResponse")]
    public void AddressSchema_Country_HasPatternOfTwoUppercaseLetters(string schemaName)
    {
        // Act
        JsonElement country = GetSchema(schemaName).GetProperty("properties").GetProperty("country");

        // Assert
        country.GetProperty("pattern").GetString().Should().Be("^[A-Z]{2}$");
    }

    [TestCase("CreateAddressRequest")]
    [TestCase("AddressResponse")]
    public void AddressSchema_Email_IsNullableEmailWithMaxLength(string schemaName)
    {
        // Act
        JsonElement email = GetSchema(schemaName).GetProperty("properties").GetProperty("email");

        // Assert
        GetTypes(email).Should().BeEquivalentTo("string", "null");
        email.GetProperty("format").GetString().Should().Be("email");
        email.GetProperty("maxLength").GetInt32().Should().Be(AddressConstraints.EmailMaxLength);
    }

    [TestCase("id", "uuid")]
    [TestCase("createdAt", "date-time")]
    [TestCase("updatedAt", "date-time")]
    public void AddressResponseSchema_Value_IsStringWithFormat(string name, string format)
    {
        // Act
        JsonElement value = GetSchema("AddressResponse").GetProperty("properties").GetProperty(name);

        // Assert
        GetTypes(value).Should().Equal("string");
        value.GetProperty("format").GetString().Should().Be(format);
    }

    private static IEnumerable<TestCaseData> RequiredValuesOfSchemas()
    {
        (string Name, int MinLength, int MaxLength)[] values =
        [
            ("firstName", 1, AddressConstraints.FirstNameMaxLength),
            ("lastName", 1, AddressConstraints.LastNameMaxLength),
            ("street", 1, AddressConstraints.StreetMaxLength),
            ("houseNumber", 1, AddressConstraints.HouseNumberMaxLength),
            ("postalCode", 1, AddressConstraints.PostalCodeMaxLength),
            ("city", 1, AddressConstraints.CityMaxLength),
            ("country", 2, AddressConstraints.CountryMaxLength),
        ];
        string[] schemaNames = ["CreateAddressRequest", "AddressResponse"];

        return schemaNames.SelectMany(schemaName => values.Select(value =>
            new TestCaseData(schemaName, value.Name, value.MinLength, value.MaxLength)
                .SetArgDisplayNames(schemaName, value.Name)));
    }

    private static IEnumerable<string?> GetTypes(JsonElement schema)
    {
        JsonElement type = schema.GetProperty("type");

        return type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Select(item => item.GetString())
            : [type.GetString()];
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
