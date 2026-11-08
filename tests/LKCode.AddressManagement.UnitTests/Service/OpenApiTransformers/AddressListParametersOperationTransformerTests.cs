using AwesomeAssertions;
using LKCode.AddressManagement.Service.OpenApiTransformers;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.UnitTests.Service.OpenApiTransformers;

public class AddressListParametersOperationTransformerTests
{
    private const string IntegerPattern = @"^-?(?:0|[1-9]\d*)$";

    private ServiceProvider _serviceProvider = null!;
    private OpenApiOperationTransformerContext _context = null!;
    private AddressListParametersOperationTransformer _transformer = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = new ServiceCollection().BuildServiceProvider();
        _context = new OpenApiOperationTransformerContext
        {
            DocumentName = "v1",
            Description = new ApiDescription(),
            ApplicationServices = _serviceProvider,
        };
        _transformer = new AddressListParametersOperationTransformer();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task TransformAsync_NullOperation_ThrowsArgumentNullException()
    {
        // Arrange
        OpenApiOperation operation = null!;

        // Act
        Func<Task> act = () => _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("operation");
    }

    [Test]
    public async Task TransformAsync_PageParameter_DescribesInt32OfAtLeastOneWithDefaultOne()
    {
        // Arrange
        OpenApiOperation operation = CreateListOperation();

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        IOpenApiSchema schema = GetSchema(operation, "page");
        schema.Type.Should().Be(JsonSchemaType.Integer);
        schema.Format.Should().Be("int32");
        schema.Minimum.Should().Be("1");
        schema.Maximum.Should().Be("2147483647");
        schema.Pattern.Should().BeNull();
        schema.Default!.GetValue<int>().Should().Be(1);
    }

    [Test]
    public async Task TransformAsync_PageSizeParameter_DescribesInt32FromOneToHundredWithDefaultTwenty()
    {
        // Arrange
        OpenApiOperation operation = CreateListOperation();

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        IOpenApiSchema schema = GetSchema(operation, "pageSize");
        schema.Type.Should().Be(JsonSchemaType.Integer);
        schema.Format.Should().Be("int32");
        schema.Minimum.Should().Be("1");
        schema.Maximum.Should().Be("100");
        schema.Pattern.Should().BeNull();
        schema.Default!.GetValue<int>().Should().Be(20);
    }

    [Test]
    public async Task TransformAsync_SortByParameter_DescribesSortFieldsWithDefaultLastName()
    {
        // Arrange
        OpenApiOperation operation = CreateListOperation();

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        IOpenApiSchema schema = GetSchema(operation, "sortBy");
        schema.Type.Should().Be(JsonSchemaType.String);
        schema.Enum!.Select(value => value.GetValue<string>()).Should().Equal(
            "lastName",
            "firstName",
            "city",
            "postalCode",
            "country",
            "createdAt",
            "updatedAt");
        schema.Default!.GetValue<string>().Should().Be("lastName");
    }

    [Test]
    public async Task TransformAsync_SortDirectionParameter_DescribesAscAndDescWithDefaultAsc()
    {
        // Arrange
        OpenApiOperation operation = CreateListOperation();

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        IOpenApiSchema schema = GetSchema(operation, "sortDirection");
        schema.Type.Should().Be(JsonSchemaType.String);
        schema.Enum!.Select(value => value.GetValue<string>()).Should().Equal("asc", "desc");
        schema.Default!.GetValue<string>().Should().Be("asc");
    }

    [Test]
    public async Task TransformAsync_ListParameters_KeepsNamesOrderLocationAndDescriptions()
    {
        // Arrange
        OpenApiOperation operation = CreateListOperation();

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        operation.Parameters!.Select(parameter => parameter.Name)
            .Should().Equal("city", "postalCode", "page", "pageSize", "sortBy", "sortDirection");
        operation.Parameters.Should().AllSatisfy(parameter =>
        {
            parameter.In.Should().Be(ParameterLocation.Query);
            parameter.Description.Should().Be($"The {parameter.Name}.");
        });
    }

    [TestCase("city")]
    [TestCase("postalCode")]
    public async Task TransformAsync_FilterParameter_KeepsSchema(string name)
    {
        // Arrange
        OpenApiOperation operation = CreateListOperation();
        IOpenApiSchema before = GetSchema(operation, name);

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        GetSchema(operation, name).Should().BeSameAs(before);
    }

    [TestCase(ParameterLocation.Header)]
    [TestCase(ParameterLocation.Path)]
    [TestCase(ParameterLocation.Cookie)]
    public async Task TransformAsync_ParameterWithSameNameOutsideQuery_KeepsSchema(ParameterLocation location)
    {
        // Arrange
        OpenApiSchema schema = CreateIntegerSchema();
        var operation = new OpenApiOperation
        {
            Parameters = [new OpenApiParameter { Name = "page", In = location, Schema = schema }],
        };

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        operation.Parameters.Should().ContainSingle().Which.Schema.Should().BeSameAs(schema);
    }

    [Test]
    public async Task TransformAsync_OperationWithoutParameters_AddsNoParameters()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        operation.Parameters.Should().BeNullOrEmpty();
    }

    [Test]
    public async Task TransformAsync_TwoOperations_DoNotShareSchemas()
    {
        // Arrange
        OpenApiOperation first = CreateListOperation();
        OpenApiOperation second = CreateListOperation();

        // Act
        await _transformer.TransformAsync(first, _context, CancellationToken.None);
        await _transformer.TransformAsync(second, _context, CancellationToken.None);

        // Assert
        GetSchema(first, "page").Should().NotBeSameAs(GetSchema(second, "page"));
    }

    /// <summary>
    /// Creates the operation of the list like the OpenAPI generator does: the filters as strings, the numbers as
    /// integer or string because the JSON options allow reading numbers from strings.
    /// </summary>
    private static OpenApiOperation CreateListOperation() => new()
    {
        Parameters =
        [
            CreateParameter("city", new OpenApiSchema { Type = JsonSchemaType.String }),
            CreateParameter("postalCode", new OpenApiSchema { Type = JsonSchemaType.String }),
            CreateParameter("page", CreateIntegerSchema()),
            CreateParameter("pageSize", CreateIntegerSchema()),
            CreateParameter("sortBy", new OpenApiSchema { Type = JsonSchemaType.String }),
            CreateParameter("sortDirection", new OpenApiSchema { Type = JsonSchemaType.String }),
        ],
    };

    private static OpenApiParameter CreateParameter(string name, OpenApiSchema schema) => new()
    {
        Name = name,
        In = ParameterLocation.Query,
        Description = $"The {name}.",
        Schema = schema,
    };

    private static OpenApiSchema CreateIntegerSchema() => new()
    {
        Type = JsonSchemaType.Integer | JsonSchemaType.String,
        Format = "int32",
        Pattern = IntegerPattern,
    };

    private static IOpenApiSchema GetSchema(OpenApiOperation operation, string name) =>
        operation.Parameters!.Single(parameter => parameter.Name == name).Schema!;
}
