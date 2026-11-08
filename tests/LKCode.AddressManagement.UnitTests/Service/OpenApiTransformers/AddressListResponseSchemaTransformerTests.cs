using System.Text.Json;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Service.OpenApiTransformers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.UnitTests.Service.OpenApiTransformers;

public class AddressListResponseSchemaTransformerTests
{
    private const string IntegerPattern = @"^-?(?:0|[1-9]\d*)$";

    private ServiceProvider _serviceProvider = null!;
    private AddressListResponseSchemaTransformer _transformer = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = new ServiceCollection().BuildServiceProvider();
        _transformer = new AddressListResponseSchemaTransformer();
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
            _transformer.TransformAsync(schema, CreateContext(typeof(AddressListResponse)), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("schema");
    }

    [Test]
    public async Task TransformAsync_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        OpenApiSchema schema = CreateListResponseSchema();

        // Act
        Func<Task> act = () => _transformer.TransformAsync(schema, null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [TestCase("page", "1", null)]
    [TestCase("pageSize", "1", "100")]
    [TestCase("totalCount", "0", null)]
    [TestCase("totalPages", "0", null)]
    public async Task TransformAsync_Number_DescribesInt32WithRange(string name, string minimum, string? maximum)
    {
        // Arrange
        OpenApiSchema schema = CreateListResponseSchema();

        // Act
        await _transformer.TransformAsync(schema, CreateContext(typeof(AddressListResponse)), CancellationToken.None);

        // Assert
        IOpenApiSchema number = schema.Properties![name];
        number.Type.Should().Be(JsonSchemaType.Integer);
        number.Format.Should().Be("int32");
        number.Minimum.Should().Be(minimum);
        number.Maximum.Should().Be(maximum);
        number.Pattern.Should().BeNull();
    }

    [TestCase("page")]
    [TestCase("totalCount")]
    public async Task TransformAsync_Number_KeepsDescription(string name)
    {
        // Arrange
        OpenApiSchema schema = CreateListResponseSchema();

        // Act
        await _transformer.TransformAsync(schema, CreateContext(typeof(AddressListResponse)), CancellationToken.None);

        // Assert
        schema.Properties![name].Description.Should().Be($"The {name}.");
    }

    [Test]
    public async Task TransformAsync_Items_KeepsSchema()
    {
        // Arrange
        OpenApiSchema schema = CreateListResponseSchema();
        IOpenApiSchema items = schema.Properties!["items"];

        // Act
        await _transformer.TransformAsync(schema, CreateContext(typeof(AddressListResponse)), CancellationToken.None);

        // Assert
        schema.Properties["items"].Should().BeSameAs(items);
        items.Type.Should().Be(JsonSchemaType.Array);
    }

    [Test]
    public async Task TransformAsync_ListResponseWithoutRequired_RequiresAllMembers()
    {
        // Arrange
        OpenApiSchema schema = CreateListResponseSchema();
        schema.Required = null;

        // Act
        await _transformer.TransformAsync(schema, CreateContext(typeof(AddressListResponse)), CancellationToken.None);

        // Assert
        schema.Required.Should().BeEquivalentTo("items", "page", "pageSize", "totalCount", "totalPages");
    }

    [Test]
    public async Task TransformAsync_ListResponseWithoutProperties_OnlyRequiresAllMembers()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object };

        // Act
        await _transformer.TransformAsync(schema, CreateContext(typeof(AddressListResponse)), CancellationToken.None);

        // Assert
        schema.Properties.Should().BeNull();
        schema.Required.Should().HaveCount(5);
    }

    [TestCase(typeof(AddressResponse))]
    [TestCase(typeof(AddressListQuery))]
    [TestCase(typeof(ProblemDetails))]
    [TestCase(typeof(int))]
    public async Task TransformAsync_OtherType_KeepsSchemaUnchanged(Type type)
    {
        // Arrange
        OpenApiSchema schema = CreateListResponseSchema();
        string before = await SerializeAsync(schema);

        // Act
        await _transformer.TransformAsync(schema, CreateContext(type), CancellationToken.None);

        // Assert
        (await SerializeAsync(schema)).Should().Be(before);
    }

    /// <summary>
    /// Creates the schema of <see cref="AddressListResponse"/> like the OpenAPI generator does: the numbers as integer
    /// or string because the JSON options allow reading numbers from strings.
    /// </summary>
    private static OpenApiSchema CreateListResponseSchema()
    {
        var properties = new Dictionary<string, IOpenApiSchema>
        {
            ["items"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = new OpenApiSchema { Type = JsonSchemaType.Object },
            },
        };
        foreach (string name in new[] { "page", "pageSize", "totalCount", "totalPages" })
        {
            properties[name] = new OpenApiSchema
            {
                Type = JsonSchemaType.Integer | JsonSchemaType.String,
                Format = "int32",
                Pattern = IntegerPattern,
                Description = $"The {name}.",
            };
        }

        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = properties,
            Required = new HashSet<string>(properties.Keys),
        };
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
