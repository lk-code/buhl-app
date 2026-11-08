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
        JsonElement parameters = GetGetByIdOperation().GetProperty("parameters");

        // Assert
        AssertIsIdParameter(parameters);
    }

    [Test]
    public void GetListOperation_OperationId_IsListAddresses()
    {
        // Act
        JsonElement operation = GetGetListOperation();

        // Assert
        operation.GetProperty("operationId").GetString().Should().Be("ListAddresses");
    }

    [Test]
    public void GetListOperation_Responses_ContainAllStatusCodes()
    {
        // Act
        JsonElement responses = GetGetListOperation().GetProperty("responses");

        // Assert
        GetNames(responses).Should().BeEquivalentTo("200", "500");
    }

    [Test]
    public void GetListOperation_OkResponse_HasOnlyJsonWithArrayOfAddressResponse()
    {
        // Act
        JsonElement response = GetGetListOperation().GetProperty("responses").GetProperty("200");

        // Assert
        response.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        response.TryGetProperty("headers", out _).Should().BeFalse();
        JsonElement content = response.GetProperty("content");
        GetNames(content).Should().Equal(JsonMediaType);
        JsonElement schema = content.GetProperty(JsonMediaType).GetProperty("schema");
        GetTypes(schema).Should().Equal("array");
        schema.GetProperty("items").GetProperty("$ref").GetString()
            .Should().Be(SchemaReferencePrefix + "AddressResponse");
    }

    [Test]
    public void GetListOperation_ErrorResponse_HasOnlyProblemDetails()
    {
        // Act
        JsonElement content = GetGetListOperation().GetProperty("responses").GetProperty("500").GetProperty("content");

        // Assert
        GetNames(content).Should().Equal(ProblemDetailsMediaType);
        GetSchemaReference(content.GetProperty(ProblemDetailsMediaType))
            .Should().Be(SchemaReferencePrefix + "ProblemDetails");
    }

    [Test]
    public void GetListOperation_Parameters_AreCityAndPostalCode()
    {
        // Act
        JsonElement parameters = GetGetListOperation().GetProperty("parameters");

        // Assert
        parameters.EnumerateArray().Select(parameter => parameter.GetProperty("name").GetString())
            .Should().Equal("city", "postalCode");
    }

    [TestCase("city")]
    [TestCase("postalCode")]
    public void GetListOperation_FilterParameter_IsOptionalStringInQuery(string name)
    {
        // Act
        JsonElement parameter = GetGetListOperation().GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == name);

        // Assert
        parameter.GetProperty("in").GetString().Should().Be("query");
        (parameter.TryGetProperty("required", out JsonElement required) && required.GetBoolean()).Should().BeFalse();
        parameter.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        JsonElement schema = parameter.GetProperty("schema");
        GetTypes(schema).Should().Equal("string");
        schema.TryGetProperty("maxLength", out _).Should().BeFalse();
    }

    [Test]
    public void GetListOperation_RequestBody_DoesNotExist()
    {
        // Act
        JsonElement operation = GetGetListOperation();

        // Assert
        operation.TryGetProperty("requestBody", out _).Should().BeFalse();
    }

    [Test]
    public void PutOperation_OperationId_IsUpdateAddress()
    {
        // Act
        JsonElement operation = GetPutOperation();

        // Assert
        operation.GetProperty("operationId").GetString().Should().Be("UpdateAddress");
    }

    [Test]
    public void PutOperation_Responses_ContainAllStatusCodes()
    {
        // Act
        JsonElement responses = GetPutOperation().GetProperty("responses");

        // Assert
        GetNames(responses).Should().BeEquivalentTo("200", "400", "404", "415", "500");
    }

    [TestCase("200", JsonMediaType, "AddressResponse")]
    [TestCase("400", ProblemDetailsMediaType, "ValidationProblemDetails")]
    [TestCase("404", ProblemDetailsMediaType, "ProblemDetails")]
    [TestCase("415", ProblemDetailsMediaType, "ProblemDetails")]
    [TestCase("500", ProblemDetailsMediaType, "ProblemDetails")]
    public void PutOperation_Response_HasOnlyMediaTypeWithSchema(string statusCode, string mediaType, string schema)
    {
        // Act
        JsonElement response = GetPutOperation().GetProperty("responses").GetProperty(statusCode);

        // Assert
        response.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        JsonElement content = response.GetProperty("content");
        GetNames(content).Should().Equal(mediaType);
        GetSchemaReference(content.GetProperty(mediaType)).Should().Be(SchemaReferencePrefix + schema);
    }

    [TestCase("200")]
    [TestCase("400")]
    [TestCase("404")]
    [TestCase("415")]
    [TestCase("500")]
    public void PutOperation_Response_HasNoHeaders(string statusCode)
    {
        // Act
        JsonElement response = GetPutOperation().GetProperty("responses").GetProperty(statusCode);

        // Assert
        response.TryGetProperty("headers", out _).Should().BeFalse();
    }

    [Test]
    public void PutOperation_RequestBody_IsRequiredJsonOfUpdateAddressRequest()
    {
        // Act
        JsonElement requestBody = GetPutOperation().GetProperty("requestBody");

        // Assert
        requestBody.GetProperty("required").GetBoolean().Should().BeTrue();
        requestBody.GetProperty("description").GetString().Should().Be("The new values of the address.");
        JsonElement content = requestBody.GetProperty("content");
        GetNames(content).Should().BeEquivalentTo(JsonMediaType, "application/*+json");
        content.EnumerateObject().Should().AllSatisfy(mediaType =>
            GetSchemaReference(mediaType.Value).Should().Be(SchemaReferencePrefix + "UpdateAddressRequest"));
    }

    [Test]
    public void PutOperation_IdParameter_IsRequiredUuidInPath()
    {
        // Act
        JsonElement parameters = GetPutOperation().GetProperty("parameters");

        // Assert
        AssertIsIdParameter(parameters);
    }

    [Test]
    public void PatchOperation_OperationId_IsPatchAddress()
    {
        // Act
        JsonElement operation = GetPatchOperation();

        // Assert
        operation.GetProperty("operationId").GetString().Should().Be("PatchAddress");
    }

    [Test]
    public void PatchOperation_Responses_ContainAllStatusCodes()
    {
        // Act
        JsonElement responses = GetPatchOperation().GetProperty("responses");

        // Assert
        GetNames(responses).Should().BeEquivalentTo("200", "400", "404", "409", "415", "500");
    }

    [TestCase("200", JsonMediaType, "AddressResponse")]
    [TestCase("400", ProblemDetailsMediaType, "ValidationProblemDetails")]
    [TestCase("404", ProblemDetailsMediaType, "ProblemDetails")]
    [TestCase("409", ProblemDetailsMediaType, "ProblemDetails")]
    [TestCase("415", ProblemDetailsMediaType, "ProblemDetails")]
    [TestCase("500", ProblemDetailsMediaType, "ProblemDetails")]
    public void PatchOperation_Response_HasOnlyMediaTypeWithSchema(string statusCode, string mediaType, string schema)
    {
        // Act
        JsonElement response = GetPatchOperation().GetProperty("responses").GetProperty(statusCode);

        // Assert
        response.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        JsonElement content = response.GetProperty("content");
        GetNames(content).Should().Equal(mediaType);
        GetSchemaReference(content.GetProperty(mediaType)).Should().Be(SchemaReferencePrefix + schema);
    }

    [TestCase("200")]
    [TestCase("400")]
    [TestCase("404")]
    [TestCase("409")]
    [TestCase("415")]
    [TestCase("500")]
    public void PatchOperation_Response_HasNoHeaders(string statusCode)
    {
        // Act
        JsonElement response = GetPatchOperation().GetProperty("responses").GetProperty(statusCode);

        // Assert
        response.TryGetProperty("headers", out _).Should().BeFalse();
    }

    [Test]
    public void PatchOperation_RequestBody_IsRequiredJsonPatchDocument()
    {
        // Act
        JsonElement requestBody = GetPatchOperation().GetProperty("requestBody");

        // Assert
        requestBody.GetProperty("required").GetBoolean().Should().BeTrue();
        requestBody.GetProperty("description").GetString()
            .Should().Be("The operations to apply to the values of the address.");
        JsonElement content = requestBody.GetProperty("content");
        GetNames(content).Should().Equal("application/json-patch+json");
        GetSchemaReference(content.GetProperty("application/json-patch+json"))
            .Should().Be(SchemaReferencePrefix + "JsonPatchDocument");
    }

    [Test]
    public void PatchOperation_IdParameter_IsRequiredUuidInPath()
    {
        // Act
        JsonElement parameters = GetPatchOperation().GetProperty("parameters");

        // Assert
        AssertIsIdParameter(parameters);
    }

    [Test]
    public void JsonPatchDocumentSchema_Document_IsDescribedArrayOfOperations()
    {
        // Act
        JsonElement schema = GetSchema("JsonPatchDocument");

        // Assert
        GetTypes(schema).Should().Equal("array");
        schema.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        JsonElement items = schema.GetProperty("items");
        items.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        items.GetProperty("oneOf").GetArrayLength().Should().Be(3);
    }

    [TestCase(0, new[] { "add", "replace", "test" }, new[] { "op", "path", "value" })]
    [TestCase(1, new[] { "move", "copy" }, new[] { "op", "path", "from" })]
    [TestCase(2, new[] { "remove" }, new[] { "op", "path" })]
    public void JsonPatchDocumentSchema_Operation_HasOperationsAndOnlyRequiredMembers(
        int index,
        string[] operations,
        string[] members)
    {
        // Act
        JsonElement operation = GetPatchOperationSchema(index);

        // Assert
        GetTypes(operation).Should().Equal("object");
        operation.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        GetNames(operation.GetProperty("properties")).Should().BeEquivalentTo(members);
        operation.GetProperty("required").EnumerateArray().Select(member => member.GetString())
            .Should().BeEquivalentTo(members);
        JsonElement op = operation.GetProperty("properties").GetProperty("op");
        GetTypes(op).Should().Equal("string");
        op.GetProperty("enum").EnumerateArray().Select(value => value.GetString()).Should().Equal(operations);
    }

    [TestCase(0, "path")]
    [TestCase(1, "path")]
    [TestCase(1, "from")]
    [TestCase(2, "path")]
    public void JsonPatchDocumentSchema_PathAndFrom_AreEnumOfAllFieldsOfAnAddress(int index, string member)
    {
        // Act
        JsonElement pointer = GetPatchOperationSchema(index).GetProperty("properties").GetProperty(member);

        // Assert
        GetTypes(pointer).Should().Equal("string");
        pointer.GetProperty("enum").EnumerateArray().Select(value => value.GetString()).Should().Equal(
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
    public void JsonPatchDocumentSchema_Value_IsNullableString()
    {
        // Act
        JsonElement value = GetPatchOperationSchema(0).GetProperty("properties").GetProperty("value");

        // Assert
        GetTypes(value).Should().BeEquivalentTo("string", "null");
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void JsonPatchDocumentSchema_Members_HaveDescription(int index)
    {
        // Act
        JsonElement properties = GetPatchOperationSchema(index).GetProperty("properties");

        // Assert
        properties.EnumerateObject().Should().AllSatisfy(member =>
            member.Value.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace());
    }

    [Test]
    public void DeleteOperation_OperationId_IsDeleteAddress()
    {
        // Act
        JsonElement operation = GetDeleteOperation();

        // Assert
        operation.GetProperty("operationId").GetString().Should().Be("DeleteAddress");
    }

    [Test]
    public void DeleteOperation_Responses_ContainAllStatusCodes()
    {
        // Act
        JsonElement responses = GetDeleteOperation().GetProperty("responses");

        // Assert
        GetNames(responses).Should().BeEquivalentTo("204", "404", "500");
    }

    [Test]
    public void DeleteOperation_NoContentResponse_HasNoContentAndNoHeaders()
    {
        // Act
        JsonElement response = GetDeleteOperation().GetProperty("responses").GetProperty("204");

        // Assert
        response.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        response.TryGetProperty("content", out _).Should().BeFalse();
        response.TryGetProperty("headers", out _).Should().BeFalse();
    }

    [TestCase("404")]
    [TestCase("500")]
    public void DeleteOperation_ErrorResponse_HasOnlyProblemDetailsWithoutHeaders(string statusCode)
    {
        // Act
        JsonElement response = GetDeleteOperation().GetProperty("responses").GetProperty(statusCode);

        // Assert
        response.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        response.TryGetProperty("headers", out _).Should().BeFalse();
        JsonElement content = response.GetProperty("content");
        GetNames(content).Should().Equal(ProblemDetailsMediaType);
        GetSchemaReference(content.GetProperty(ProblemDetailsMediaType))
            .Should().Be(SchemaReferencePrefix + "ProblemDetails");
    }

    [Test]
    public void DeleteOperation_IdParameter_IsRequiredUuidInPath()
    {
        // Act
        JsonElement parameters = GetDeleteOperation().GetProperty("parameters");

        // Assert
        AssertIsIdParameter(parameters);
    }

    [Test]
    public void DeleteOperation_RequestBody_DoesNotExist()
    {
        // Act
        JsonElement operation = GetDeleteOperation();

        // Assert
        operation.TryGetProperty("requestBody", out _).Should().BeFalse();
    }

    [TestCase("CreateAddressRequest")]
    [TestCase("UpdateAddressRequest")]
    public void AddressRequestSchema_Required_AreAllValuesExceptEmail(string schemaName)
    {
        // Act
        JsonElement schema = GetSchema(schemaName);

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

    [TestCase("CreateAddressRequest")]
    [TestCase("UpdateAddressRequest")]
    public void AddressRequestSchema_Properties_AreExactlyTheValuesOfAnAddress(string schemaName)
    {
        // Act
        JsonElement schema = GetSchema(schemaName);

        // Assert
        GetNames(schema.GetProperty("properties")).Should().BeEquivalentTo(
            "firstName",
            "lastName",
            "street",
            "houseNumber",
            "postalCode",
            "city",
            "country",
            "email");
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
    [TestCase("UpdateAddressRequest")]
    [TestCase("AddressResponse")]
    public void AddressSchema_Country_HasPatternOfTwoUppercaseLetters(string schemaName)
    {
        // Act
        JsonElement country = GetSchema(schemaName).GetProperty("properties").GetProperty("country");

        // Assert
        country.GetProperty("pattern").GetString().Should().Be("^[A-Z]{2}$");
    }

    [TestCase("CreateAddressRequest")]
    [TestCase("UpdateAddressRequest")]
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
        string[] schemaNames = ["CreateAddressRequest", "UpdateAddressRequest", "AddressResponse"];

        return schemaNames.SelectMany(schemaName => values.Select(value =>
            new TestCaseData(schemaName, value.Name, value.MinLength, value.MaxLength)
                .SetArgDisplayNames(schemaName, value.Name)));
    }

    private static void AssertIsIdParameter(JsonElement parameters)
    {
        JsonElement parameter = parameters.EnumerateArray().Should().ContainSingle().Which;
        parameter.GetProperty("name").GetString().Should().Be("id");
        parameter.GetProperty("in").GetString().Should().Be("path");
        parameter.GetProperty("required").GetBoolean().Should().BeTrue();
        parameter.GetProperty("schema").GetProperty("type").GetString().Should().Be("string");
        parameter.GetProperty("schema").GetProperty("format").GetString().Should().Be("uuid");
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

    private JsonElement GetGetListOperation() =>
        _document.GetProperty("paths").GetProperty("/api/addresses").GetProperty("get");

    private JsonElement GetGetByIdOperation() =>
        _document.GetProperty("paths").GetProperty("/api/addresses/{id}").GetProperty("get");

    private JsonElement GetPutOperation() =>
        _document.GetProperty("paths").GetProperty("/api/addresses/{id}").GetProperty("put");

    private JsonElement GetPatchOperation() =>
        _document.GetProperty("paths").GetProperty("/api/addresses/{id}").GetProperty("patch");

    private JsonElement GetPatchOperationSchema(int index) =>
        GetSchema("JsonPatchDocument").GetProperty("items").GetProperty("oneOf")[index];

    private JsonElement GetDeleteOperation() =>
        _document.GetProperty("paths").GetProperty("/api/addresses/{id}").GetProperty("delete");

    private JsonElement GetSchema(string name) =>
        _document.GetProperty("components").GetProperty("schemas").GetProperty(name);
}
