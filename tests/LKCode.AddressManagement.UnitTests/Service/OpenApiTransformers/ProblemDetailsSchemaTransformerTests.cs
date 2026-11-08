using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Service.OpenApiTransformers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.UnitTests.Service.OpenApiTransformers;

public class ProblemDetailsSchemaTransformerTests
{
    private static readonly Type[] _problemDetailsTypes =
    [
        typeof(ProblemDetails),
        typeof(HttpValidationProblemDetails),
        typeof(ValidationProblemDetails),
    ];

    private static readonly Type[] _validationProblemDetailsTypes =
    [
        typeof(HttpValidationProblemDetails),
        typeof(ValidationProblemDetails),
    ];

    private ServiceProvider _serviceProvider = null!;
    private ProblemDetailsSchemaTransformer _transformer = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = new ServiceCollection().BuildServiceProvider();
        _transformer = new ProblemDetailsSchemaTransformer();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task TransformAsync_NullSchema_ThrowsArgumentNullException()
    {
        // Arrange
        OpenApiSchema schema = null!;

        // Act
        Func<Task> act = () =>
            _transformer.TransformAsync(schema, CreateContext(typeof(ProblemDetails)), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("schema");
    }

    [Test]
    public async Task TransformAsync_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        OpenApiSchemaTransformerContext context = null!;

        // Act
        Func<Task> act = () =>
            _transformer.TransformAsync(CreateSchema(typeof(ProblemDetails)), context, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Test]
    public async Task TransformAsync_ProblemDetails_RequiresMembersOfEveryResponse()
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(typeof(ProblemDetails));

        // Act
        await _transformer.TransformAsync(schema, CreateContext(typeof(ProblemDetails)), CancellationToken.None);

        // Assert
        schema.Required.Should().BeEquivalentTo("type", "title", "status", "traceId");
    }

    [TestCaseSource(nameof(_validationProblemDetailsTypes))]
    public async Task TransformAsync_ValidationProblemDetails_RequiresMembersOfEveryResponseAndErrors(Type type)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(type);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(type), CancellationToken.None);

        // Assert
        schema.Required.Should().BeEquivalentTo("type", "title", "status", "traceId", "errors");
    }

    [TestCaseSource(nameof(_problemDetailsTypes))]
    public async Task TransformAsync_ProblemDetailsType_DescribesStatusAsHttpStatusCode(Type type)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(type);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(type), CancellationToken.None);

        // Assert
        IOpenApiSchema status = schema.Properties!["status"];
        status.Type.Should().Be(JsonSchemaType.Integer);
        status.Format.Should().Be("int32");
        status.Minimum.Should().Be("100");
        status.Maximum.Should().Be("599");
        status.Pattern.Should().BeNull();
    }

    [TestCaseSource(nameof(StringMembersOfProblemDetailsTypes))]
    public async Task TransformAsync_ProblemDetailsType_DescribesStringMember(
        Type type,
        string name,
        JsonSchemaType expectedType,
        string? expectedFormat)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(type);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(type), CancellationToken.None);

        // Assert
        IOpenApiSchema member = schema.Properties![name];
        member.Type.Should().Be(expectedType);
        member.Format.Should().Be(expectedFormat);
        member.Pattern.Should().BeNull();
    }

    [TestCaseSource(nameof(_problemDetailsTypes))]
    public async Task TransformAsync_ProblemDetailsType_DescribesEveryMember(Type type)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(type);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(type), CancellationToken.None);

        // Assert
        schema.Properties.Should().NotBeEmpty().And.AllSatisfy(member =>
            member.Value.Description.Should().NotBeNullOrWhiteSpace());
    }

    [TestCaseSource(nameof(_validationProblemDetailsTypes))]
    public async Task TransformAsync_ValidationProblemDetails_KeepsErrorsAsMessagesPerField(Type type)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(type);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(type), CancellationToken.None);

        // Assert
        IOpenApiSchema errors = schema.Properties!["errors"];
        errors.Type.Should().Be(JsonSchemaType.Object);
        errors.AdditionalProperties!.Type.Should().Be(JsonSchemaType.Array);
        errors.AdditionalProperties.Items!.Type.Should().Be(JsonSchemaType.String);
    }

    [Test]
    public async Task TransformAsync_SchemaWithoutProperties_AddsAllMembers()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object };

        // Act
        await _transformer.TransformAsync(schema, CreateContext(typeof(ProblemDetails)), CancellationToken.None);

        // Assert
        schema.Properties!.Keys.Should().BeEquivalentTo("type", "title", "status", "detail", "instance", "traceId");
        schema.Required.Should().BeEquivalentTo("type", "title", "status", "traceId");
    }

    [TestCase(typeof(AddressResponse))]
    [TestCase(typeof(CreateAddressRequest))]
    [TestCase(typeof(string))]
    [TestCase(typeof(int))]
    public async Task TransformAsync_OtherType_KeepsSchemaUnchanged(Type type)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema(type);
        string before = await SerializeAsync(schema);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(type), CancellationToken.None);

        // Assert
        (await SerializeAsync(schema)).Should().Be(before);
    }

    private static IEnumerable<TestCaseData> StringMembersOfProblemDetailsTypes()
    {
        (string Name, JsonSchemaType Type, string? Format)[] members =
        [
            ("type", JsonSchemaType.String, "uri-reference"),
            ("title", JsonSchemaType.String, null),
            ("detail", JsonSchemaType.String | JsonSchemaType.Null, null),
            ("instance", JsonSchemaType.String | JsonSchemaType.Null, "uri-reference"),
            ("traceId", JsonSchemaType.String, null),
        ];

        return _problemDetailsTypes.SelectMany(type => members.Select(member =>
            new TestCaseData(type, member.Name, member.Type, member.Format)
                .SetArgDisplayNames(type.Name, member.Name)));
    }

    /// <summary>
    /// Creates a schema like the OpenAPI generator does with the JSON options of the web: nullable strings, the
    /// status as integer or string with a pattern and the errors as arrays of messages by field; no member is
    /// required and there is no <c>traceId</c>.
    /// </summary>
    private static OpenApiSchema CreateSchema(Type type)
    {
        JsonTypeInfo typeInfo = JsonSerializerOptions.Web.GetTypeInfo(type);
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return new OpenApiSchema { Type = JsonSchemaType.String };
        }

        var properties = new Dictionary<string, IOpenApiSchema>();
        foreach (JsonPropertyInfo property in typeInfo.Properties.Where(property => !property.IsExtensionData))
        {
            properties[property.Name] = CreatePropertySchema(property.PropertyType);
        }

        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = properties,
        };
    }

    private static OpenApiSchema CreatePropertySchema(Type type)
    {
        if (type == typeof(int?))
        {
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Integer | JsonSchemaType.String | JsonSchemaType.Null,
                Format = "int32",
                Pattern = "^-?(?:0|[1-9]\\d*)$",
            };
        }

        if (type == typeof(IDictionary<string, string[]>))
        {
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                AdditionalProperties = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String },
                },
            };
        }

        return new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null };
    }

    private static async Task<string> SerializeAsync(OpenApiSchema schema)
    {
        await using var writer = new StringWriter();
        schema.SerializeAsV31(new OpenApiJsonWriter(writer));

        return writer.ToString();
    }

    private OpenApiSchemaTransformerContext CreateContext(Type type) => new()
    {
        DocumentName = "v1",
        JsonTypeInfo = JsonSerializerOptions.Web.GetTypeInfo(type),
        JsonPropertyInfo = null,
        ParameterDescription = null,
        ApplicationServices = _serviceProvider,
    };
}
