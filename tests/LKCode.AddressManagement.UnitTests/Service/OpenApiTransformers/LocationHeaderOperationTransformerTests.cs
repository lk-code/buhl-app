using AwesomeAssertions;
using LKCode.AddressManagement.Service.OpenApiTransformers;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.UnitTests.Service.OpenApiTransformers;

public class LocationHeaderOperationTransformerTests
{
    private const string LocationHeader = "Location";

    private ServiceProvider _serviceProvider = null!;
    private OpenApiOperationTransformerContext _context = null!;
    private LocationHeaderOperationTransformer _transformer = null!;

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
        _transformer = new LocationHeaderOperationTransformer();
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
    public async Task TransformAsync_CreatedResponse_AddsRequiredLocationHeaderWithUri()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("201");

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        IOpenApiHeader header = operation.Responses!["201"].Headers!.Should().ContainKey(LocationHeader).WhoseValue;
        header.Required.Should().BeTrue();
        header.Description.Should().NotBeNullOrWhiteSpace();
        header.Schema!.Type.Should().Be(JsonSchemaType.String);
        header.Schema.Format.Should().Be("uri");
    }

    [Test]
    public async Task TransformAsync_CreatedResponseWithHeaders_KeepsExistingHeaders()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("201");
        var etag = new OpenApiHeader { Description = "The version of the resource." };
        ((OpenApiResponse)operation.Responses!["201"]).Headers = new Dictionary<string, IOpenApiHeader>
        {
            ["ETag"] = etag,
        };

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        operation.Responses["201"].Headers.Should().HaveCount(2)
            .And.ContainKey(LocationHeader)
            .And.Contain(new KeyValuePair<string, IOpenApiHeader>("ETag", etag));
    }

    [TestCase("200")]
    [TestCase("204")]
    [TestCase("400")]
    [TestCase("404")]
    [TestCase("500")]
    public async Task TransformAsync_OtherResponse_AddsNoHeader(string statusCode)
    {
        // Arrange
        OpenApiOperation operation = CreateOperation(statusCode);

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        operation.Responses![statusCode].Headers.Should().BeNullOrEmpty();
    }

    [Test]
    public async Task TransformAsync_SeveralResponses_AddsHeaderOnlyToCreatedResponse()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("201", "400", "500");

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        operation.Responses!.Where(response => response.Value.Headers is { Count: > 0 })
            .Select(response => response.Key)
            .Should().Equal("201");
    }

    [Test]
    public async Task TransformAsync_WithoutResponses_KeepsOperationUnchanged()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        operation.Responses.Should().BeNullOrEmpty();
    }

    private static OpenApiOperation CreateOperation(params IEnumerable<string> statusCodes)
    {
        var responses = new OpenApiResponses();
        foreach (string statusCode in statusCodes)
        {
            responses[statusCode] = new OpenApiResponse { Description = statusCode };
        }

        return new OpenApiOperation { Responses = responses };
    }
}
