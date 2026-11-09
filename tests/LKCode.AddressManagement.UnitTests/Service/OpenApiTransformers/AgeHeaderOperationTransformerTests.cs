using System.Globalization;
using AwesomeAssertions;
using LKCode.AddressManagement.Service.OpenApiTransformers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.UnitTests.Service.OpenApiTransformers;

public class AgeHeaderOperationTransformerTests
{
    private const string AgeHeader = "Age";

    private ServiceProvider _serviceProvider = null!;
    private AgeHeaderOperationTransformer _transformer = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = new ServiceCollection().BuildServiceProvider();
        _transformer = new AgeHeaderOperationTransformer();
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
        Func<Task> act = () => _transformer.TransformAsync(operation, CreateCachedContext(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("operation");
    }

    [Test]
    public async Task TransformAsync_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("200");

        // Act
        Func<Task> act = () => _transformer.TransformAsync(operation, null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Test]
    public async Task TransformAsync_CachedEndpoint_AddsOptionalAgeHeaderAsNonNegativeInteger()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("200");

        // Act
        await _transformer.TransformAsync(operation, CreateCachedContext(), CancellationToken.None);

        // Assert
        IOpenApiHeader header = operation.Responses!["200"].Headers!.Should().ContainKey(AgeHeader).WhoseValue;
        header.Required.Should().BeFalse();
        header.Description.Should().NotBeNullOrWhiteSpace();
        header.Schema!.Type.Should().Be(JsonSchemaType.Integer);
        header.Schema.Format.Should().Be("int64");
        header.Schema.Minimum.Should().Be("0");
        header.Schema.Maximum.Should().BeNull();
    }

    [Test]
    public async Task TransformAsync_CachedEndpointWithPolicyName_AddsAgeHeader()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("200");

        // Act
        await _transformer.TransformAsync(
            operation,
            CreateContext(new OutputCacheAttribute { PolicyName = "AddressById" }),
            CancellationToken.None);

        // Assert
        operation.Responses!["200"].Headers.Should().ContainKey(AgeHeader);
    }

    [Test]
    public async Task TransformAsync_ResponseWithHeaders_KeepsExistingHeaders()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("200");
        var eTag = new OpenApiHeader { Description = "The entity tag." };
        ((OpenApiResponse)operation.Responses!["200"]).Headers = new Dictionary<string, IOpenApiHeader>
        {
            ["ETag"] = eTag,
        };

        // Act
        await _transformer.TransformAsync(operation, CreateCachedContext(), CancellationToken.None);

        // Assert
        operation.Responses["200"].Headers.Should().HaveCount(2)
            .And.ContainKey(AgeHeader)
            .And.Contain(new KeyValuePair<string, IOpenApiHeader>("ETag", eTag));
    }

    [TestCase(201)]
    [TestCase(204)]
    [TestCase(304)]
    [TestCase(400)]
    [TestCase(404)]
    [TestCase(500)]
    public async Task TransformAsync_CachedEndpointWithResponseOtherThanOk_AddsNoHeader(int statusCode)
    {
        // Arrange
        string key = statusCode.ToString(CultureInfo.InvariantCulture);
        OpenApiOperation operation = CreateOperation(key);

        // Act
        await _transformer.TransformAsync(operation, CreateCachedContext(), CancellationToken.None);

        // Assert
        operation.Responses![key].Headers.Should().BeNullOrEmpty();
    }

    [Test]
    public async Task TransformAsync_CachedEndpointWithSeveralResponses_AddsHeaderOnlyToOk()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("200", "400", "404", "500");

        // Act
        await _transformer.TransformAsync(operation, CreateCachedContext(), CancellationToken.None);

        // Assert
        operation.Responses!.Where(response => response.Value.Headers is { Count: > 0 })
            .Select(response => response.Key)
            .Should().Equal("200");
    }

    [Test]
    public async Task TransformAsync_EndpointWithoutOutputCache_AddsNoHeader()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("200");

        // Act
        await _transformer.TransformAsync(operation, CreateContext(), CancellationToken.None);

        // Assert
        operation.Responses!["200"].Headers.Should().BeNullOrEmpty();
    }

    [Test]
    public async Task TransformAsync_EndpointWithOtherMetadataOnly_AddsNoHeader()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("200");

        // Act
        await _transformer.TransformAsync(
            operation,
            CreateContext(new HttpGetAttribute(), new ProducesResponseTypeAttribute(200)),
            CancellationToken.None);

        // Assert
        operation.Responses!["200"].Headers.Should().BeNullOrEmpty();
    }

    [Test]
    public async Task TransformAsync_CachedEndpointWithoutResponses_KeepsOperationUnchanged()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await _transformer.TransformAsync(operation, CreateCachedContext(), CancellationToken.None);

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

    private OpenApiOperationTransformerContext CreateCachedContext() => CreateContext(new OutputCacheAttribute());

    private OpenApiOperationTransformerContext CreateContext(params object[] endpointMetadata) => new()
    {
        DocumentName = "v1",
        Description = new ApiDescription
        {
            ActionDescriptor = new ActionDescriptor { EndpointMetadata = [.. endpointMetadata] },
        },
        ApplicationServices = _serviceProvider,
    };
}
