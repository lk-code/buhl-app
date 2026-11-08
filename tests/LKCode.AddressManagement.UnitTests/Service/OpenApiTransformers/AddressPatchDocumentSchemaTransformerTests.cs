using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Service.OpenApiTransformers;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.UnitTests.Service.OpenApiTransformers;

public class AddressPatchDocumentSchemaTransformerTests
{
    private const int AddReplaceTestIndex = 0;
    private const int MoveCopyIndex = 1;
    private const int RemoveIndex = 2;

    private static readonly string[] _fieldPointers =
    [
        "/firstName",
        "/lastName",
        "/street",
        "/houseNumber",
        "/postalCode",
        "/city",
        "/country",
        "/email",
    ];

    private ServiceProvider _serviceProvider = null!;
    private AddressPatchDocumentSchemaTransformer _transformer = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = new ServiceCollection().BuildServiceProvider();
        _transformer = new AddressPatchDocumentSchemaTransformer();
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
        Func<Task> act = () => _transformer.TransformAsync(schema, CreateContext(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("schema");
    }

    [Test]
    public async Task TransformAsync_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        OpenApiSchemaTransformerContext context = null!;

        // Act
        Func<Task> act = () => _transformer.TransformAsync(CreateSchema(), context, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [TestCase(AddReplaceTestIndex)]
    [TestCase(MoveCopyIndex)]
    [TestCase(RemoveIndex)]
    public async Task TransformAsync_PatchDocumentOfAddress_PathIsEnumOfFieldPointers(int operationIndex)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema();

        // Act
        await _transformer.TransformAsync(schema, CreateContext(), CancellationToken.None);

        // Assert
        IOpenApiSchema path = GetOperation(schema, operationIndex).Properties!["path"];
        path.Type.Should().Be(JsonSchemaType.String);
        GetEnum(path).Should().Equal(_fieldPointers);
        path.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task TransformAsync_PatchDocumentOfAddress_FromIsEnumOfFieldPointers()
    {
        // Arrange
        OpenApiSchema schema = CreateSchema();

        // Act
        await _transformer.TransformAsync(schema, CreateContext(), CancellationToken.None);

        // Assert
        IOpenApiSchema from = GetOperation(schema, MoveCopyIndex).Properties!["from"];
        from.Type.Should().Be(JsonSchemaType.String);
        GetEnum(from).Should().Equal(_fieldPointers);
        from.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task TransformAsync_PatchDocumentOfAddress_ValueIsNullableString()
    {
        // Arrange
        OpenApiSchema schema = CreateSchema();

        // Act
        await _transformer.TransformAsync(schema, CreateContext(), CancellationToken.None);

        // Assert
        IOpenApiSchema value = GetOperation(schema, AddReplaceTestIndex).Properties!["value"];
        value.Type.Should().Be(JsonSchemaType.String | JsonSchemaType.Null);
        value.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task TransformAsync_PatchDocumentOfAddress_DescribesDocumentAndOperations()
    {
        // Arrange
        OpenApiSchema schema = CreateSchema();

        // Act
        await _transformer.TransformAsync(schema, CreateContext(), CancellationToken.None);

        // Assert
        schema.Description.Should().NotBeNullOrWhiteSpace();
        schema.Items!.Description.Should().NotBeNullOrWhiteSpace();
        schema.Items.OneOf.Should().AllSatisfy(operation =>
            operation.Properties!["op"].Description.Should().NotBeNullOrWhiteSpace());
    }

    [Test]
    public async Task TransformAsync_PatchDocumentOfAddress_KeepsOperationsAndRequiredMembers()
    {
        // Arrange
        OpenApiSchema schema = CreateSchema();

        // Act
        await _transformer.TransformAsync(schema, CreateContext(), CancellationToken.None);

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Array);
        schema.Items!.OneOf.Should().HaveCount(3);
        GetEnum(GetOperation(schema, AddReplaceTestIndex).Properties!["op"]).Should().Equal("add", "replace", "test");
        GetEnum(GetOperation(schema, MoveCopyIndex).Properties!["op"]).Should().Equal("move", "copy");
        GetEnum(GetOperation(schema, RemoveIndex).Properties!["op"]).Should().Equal("remove");
        GetOperation(schema, AddReplaceTestIndex).Required.Should().BeEquivalentTo("op", "path", "value");
        GetOperation(schema, MoveCopyIndex).Required.Should().BeEquivalentTo("op", "path", "from");
        GetOperation(schema, RemoveIndex).Required.Should().BeEquivalentTo("op", "path");
        schema.Items.OneOf.Should().AllSatisfy(operation =>
            operation.AdditionalPropertiesAllowed.Should().BeFalse());
    }

    [Test]
    public async Task TransformAsync_OptionsWithoutNamingPolicy_UsesNamesOfOptions()
    {
        // Arrange
        OpenApiSchema schema = CreateSchema();

        // Act
        await _transformer.TransformAsync(schema, CreateContext(JsonSerializerOptions.Default), CancellationToken.None);

        // Assert
        GetEnum(GetOperation(schema, RemoveIndex).Properties!["path"]).Should().StartWith("/FirstName");
    }

    [Test]
    public async Task TransformAsync_SchemaWithoutOperations_KeepsSchemaUnchanged()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Array };
        string before = await SerializeAsync(schema);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(), CancellationToken.None);

        // Assert
        (await SerializeAsync(schema)).Should().Be(before);
    }

    [TestCase(typeof(UpdateAddressRequest))]
    [TestCase(typeof(JsonPatchDocument<CreateAddressRequest>))]
    [TestCase(typeof(string))]
    public async Task TransformAsync_OtherType_KeepsSchemaUnchanged(Type type)
    {
        // Arrange
        OpenApiSchema schema = CreateSchema();
        string before = await SerializeAsync(schema);
        OpenApiSchemaTransformerContext context = CreateContext(JsonSerializerOptions.Web, type);

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        (await SerializeAsync(schema)).Should().Be(before);
    }

    /// <summary>
    /// Creates the schema that the OpenAPI generator of ASP.NET Core creates for every JSON Patch document.
    /// </summary>
    private static OpenApiSchema CreateSchema()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                OneOf =
                [
                    CreateOperation(["add", "replace", "test"], "value"),
                    CreateOperation(["move", "copy"], "from"),
                    CreateOperation(["remove"], member: null),
                ],
            },
        };

        static OpenApiSchema CreateOperation(string[] operations, string? member)
        {
            var properties = new Dictionary<string, IOpenApiSchema>
            {
                ["op"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [.. operations.Select(operation => (JsonNode)JsonValue.Create(operation))],
                },
                ["path"] = new OpenApiSchema { Type = JsonSchemaType.String },
            };
            var required = new HashSet<string> { "op", "path" };
            if (member == "value")
            {
                properties[member] = new OpenApiSchema();
                required.Add(member);
            }
            else if (member == "from")
            {
                properties[member] = new OpenApiSchema { Type = JsonSchemaType.String };
                required.Add(member);
            }

            return new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Properties = properties,
                Required = required,
                AdditionalPropertiesAllowed = false,
            };
        }
    }

    private static IOpenApiSchema GetOperation(OpenApiSchema schema, int index) => schema.Items!.OneOf![index];

    private static IEnumerable<string?> GetEnum(IOpenApiSchema schema) =>
        schema.Enum!.Select(value => value.GetValue<string>());

    private static async Task<string> SerializeAsync(OpenApiSchema schema)
    {
        await using var writer = new StringWriter();
        schema.SerializeAsV31(new OpenApiJsonWriter(writer));

        return writer.ToString();
    }

    private OpenApiSchemaTransformerContext CreateContext() => CreateContext(JsonSerializerOptions.Web);

    private OpenApiSchemaTransformerContext CreateContext(
        JsonSerializerOptions options,
        Type? type = null) => new()
    {
        DocumentName = "v1",
        JsonTypeInfo = options.GetTypeInfo(type ?? typeof(JsonPatchDocument<UpdateAddressRequest>)),
        JsonPropertyInfo = null,
        ParameterDescription = null,
        ApplicationServices = _serviceProvider,
    };
}
