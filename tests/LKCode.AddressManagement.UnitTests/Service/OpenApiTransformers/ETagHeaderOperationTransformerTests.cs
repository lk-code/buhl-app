using System.Globalization;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Service.OpenApiTransformers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.UnitTests.Service.OpenApiTransformers;

public class ETagHeaderOperationTransformerTests
{
    private const string ETagHeader = "ETag";

    private ServiceProvider _serviceProvider = null!;
    private ETagHeaderOperationTransformer _transformer = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = new ServiceCollection().BuildServiceProvider();
        _transformer = new ETagHeaderOperationTransformer();
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
        Func<Task> act = () => _transformer.TransformAsync(operation, CreateContext(), CancellationToken.None);

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

    [TestCase(200)]
    [TestCase(201)]
    public async Task TransformAsync_ResponseWithAddress_AddsRequiredETagHeaderWithPattern(int statusCode)
    {
        // Arrange
        string key = statusCode.ToString(CultureInfo.InvariantCulture);
        OpenApiOperation operation = CreateOperation(key);

        // Act
        await _transformer.TransformAsync(
            operation,
            CreateContext((statusCode, typeof(AddressResponse))),
            CancellationToken.None);

        // Assert
        IOpenApiHeader header = operation.Responses![key].Headers!.Should().ContainKey(ETagHeader).WhoseValue;
        header.Required.Should().BeTrue();
        header.Description.Should().NotBeNullOrWhiteSpace();
        header.Schema!.Type.Should().Be(JsonSchemaType.String);
        header.Schema.Pattern.Should().Be("^\"[1-9][0-9]*\"$");
    }

    [Test]
    public async Task TransformAsync_ResponseWithHeaders_KeepsExistingHeaders()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("201");
        var location = new OpenApiHeader { Description = "The URL of the resource." };
        ((OpenApiResponse)operation.Responses!["201"]).Headers = new Dictionary<string, IOpenApiHeader>
        {
            ["Location"] = location,
        };

        // Act
        await _transformer.TransformAsync(
            operation,
            CreateContext((201, typeof(AddressResponse))),
            CancellationToken.None);

        // Assert
        operation.Responses["201"].Headers.Should().HaveCount(2)
            .And.ContainKey(ETagHeader)
            .And.Contain(new KeyValuePair<string, IOpenApiHeader>("Location", location));
    }

    [TestCase(200, typeof(IReadOnlyList<AddressResponse>))]
    [TestCase(204, typeof(void))]
    [TestCase(400, typeof(ValidationProblemDetails))]
    [TestCase(404, typeof(ProblemDetails))]
    [TestCase(412, typeof(ProblemDetails))]
    public async Task TransformAsync_ResponseWithoutSingleAddress_AddsNoHeader(int statusCode, Type type)
    {
        // Arrange
        string key = statusCode.ToString(CultureInfo.InvariantCulture);
        OpenApiOperation operation = CreateOperation(key);

        // Act
        await _transformer.TransformAsync(operation, CreateContext((statusCode, type)), CancellationToken.None);

        // Assert
        operation.Responses![key].Headers.Should().BeNullOrEmpty();
    }

    [Test]
    public async Task TransformAsync_SeveralResponses_AddsHeaderOnlyToResponseWithAddress()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("200", "404", "412", "500");

        // Act
        await _transformer.TransformAsync(
            operation,
            CreateContext(
                (200, typeof(AddressResponse)),
                (404, typeof(ProblemDetails)),
                (412, typeof(ProblemDetails)),
                (500, typeof(ProblemDetails))),
            CancellationToken.None);

        // Assert
        operation.Responses!.Where(response => response.Value.Headers is { Count: > 0 })
            .Select(response => response.Key)
            .Should().Equal("200");
    }

    [Test]
    public async Task TransformAsync_ResponseTypeWithoutResponseInOperation_KeepsOperationUnchanged()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("404");

        // Act
        await _transformer.TransformAsync(
            operation,
            CreateContext((200, typeof(AddressResponse))),
            CancellationToken.None);

        // Assert
        operation.Responses.Should().ContainSingle().Which.Value.Headers.Should().BeNullOrEmpty();
    }

    [Test]
    public async Task TransformAsync_WithoutResponses_KeepsOperationUnchanged()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await _transformer.TransformAsync(
            operation,
            CreateContext((200, typeof(AddressResponse))),
            CancellationToken.None);

        // Assert
        operation.Responses.Should().BeNullOrEmpty();
    }

    [TestCase("\"1\"", true)]
    [TestCase("\"42\"", true)]
    [TestCase("\"9223372036854775807\"", true)]
    [TestCase("\"0\"", false)]
    [TestCase("\"01\"", false)]
    [TestCase("\"\"", false)]
    [TestCase("\"-1\"", false)]
    [TestCase("1", false)]
    [TestCase("W/\"1\"", false)]
    [TestCase("\"1\", \"2\"", false)]
    public async Task TransformAsync_PatternOfHeader_MatchesOnlyEntityTagsOfVersions(string value, bool expected)
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("200");
        await _transformer.TransformAsync(
            operation,
            CreateContext((200, typeof(AddressResponse))),
            CancellationToken.None);
        string pattern = operation.Responses!["200"].Headers![ETagHeader].Schema!.Pattern!;

        // Act
        bool matches = Regex.IsMatch(value, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));

        // Assert
        matches.Should().Be(expected);
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

    private OpenApiOperationTransformerContext CreateContext(params (int StatusCode, Type Type)[] responseTypes)
    {
        var description = new ApiDescription();
        foreach ((int statusCode, Type type) in responseTypes)
        {
            description.SupportedResponseTypes.Add(new ApiResponseType { StatusCode = statusCode, Type = type });
        }

        return new OpenApiOperationTransformerContext
        {
            DocumentName = "v1",
            Description = description,
            ApplicationServices = _serviceProvider,
        };
    }
}
