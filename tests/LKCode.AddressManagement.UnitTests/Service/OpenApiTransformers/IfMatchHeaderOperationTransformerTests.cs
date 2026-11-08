using System.Text.RegularExpressions;
using AwesomeAssertions;
using LKCode.AddressManagement.Service.Extensions;
using LKCode.AddressManagement.Service.OpenApiTransformers;
using LKCode.AddressManagement.Service.Preconditions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.UnitTests.Service.OpenApiTransformers;

public class IfMatchHeaderOperationTransformerTests
{
    private const string IfMatchHeader = "If-Match";

    private ServiceProvider _serviceProvider = null!;
    private OpenApiOperationTransformerContext _context = null!;
    private IfMatchHeaderOperationTransformer _transformer = null!;

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
        _transformer = new IfMatchHeaderOperationTransformer();
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
    public async Task TransformAsync_PreconditionRequiredResponse_AddsRequiredIfMatchHeaderParameter()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("200", "412", "428");

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        IOpenApiParameter parameter = operation.Parameters.Should().ContainSingle().Which;
        parameter.Name.Should().Be(IfMatchHeader);
        parameter.In.Should().Be(ParameterLocation.Header);
        parameter.Required.Should().BeTrue();
        parameter.Description.Should().NotBeNullOrWhiteSpace();
        parameter.Schema!.Type.Should().Be(JsonSchemaType.String);
        parameter.Schema.Pattern.Should().Be("""^(\*|(W/)?"[\x21\x23-\x7E]*"(\s*,\s*(W/)?"[\x21\x23-\x7E]*")*)$""");
    }

    [Test]
    public async Task TransformAsync_OperationWithParameters_KeepsParametersAndAddsIfMatchLast()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("428");
        var id = new OpenApiParameter { Name = "id", In = ParameterLocation.Path, Required = true };
        operation.Parameters = [id];

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        operation.Parameters.Should().HaveCount(2);
        operation.Parameters[0].Should().BeSameAs(id);
        operation.Parameters[1].Name.Should().Be(IfMatchHeader);
    }

    [TestCase("200")]
    [TestCase("201")]
    [TestCase("404")]
    [TestCase("412")]
    [TestCase("500")]
    public async Task TransformAsync_WithoutPreconditionRequiredResponse_AddsNoParameter(string statusCode)
    {
        // Arrange
        OpenApiOperation operation = CreateOperation(statusCode);

        // Act
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);

        // Assert
        operation.Parameters.Should().BeNullOrEmpty();
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
        operation.Parameters.Should().BeNullOrEmpty();
    }

    [TestCase("*")]
    [TestCase("\"3\"")]
    [TestCase("\"2\"")]
    [TestCase("W/\"3\"")]
    [TestCase("\"\"")]
    [TestCase("\"abc\"")]
    [TestCase("\"2\", \"3\"")]
    [TestCase("\"3\",\"4\"")]
    [TestCase("W/\"2\", W/\"3\"")]
    [TestCase("W/\"3\", \"3\"")]
    [TestCase("")]
    [TestCase(",")]
    [TestCase("3")]
    [TestCase("\"3")]
    [TestCase("3\"")]
    [TestCase("'3'")]
    [TestCase("\"3\" x")]
    [TestCase("\"3\"\"4\"")]
    [TestCase("W/3")]
    [TestCase("W/*")]
    [TestCase("**")]
    [TestCase("*, \"3\"")]
    [TestCase("\"3\", *")]
    [TestCase("*, *")]
    public async Task TransformAsync_PatternOfParameter_AcceptsExactlyTheValidHeaders(string value)
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("428");
        await _transformer.TransformAsync(operation, _context, CancellationToken.None);
        string pattern = operation.Parameters![0].Schema!.Pattern!;
        bool isValid = new HeaderDictionary { [IfMatchHeader] = value }.EvaluateIfMatch(3) != IfMatchOutcome.Invalid;

        // Act
        bool matches = Regex.IsMatch(value, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));

        // Assert
        matches.Should().Be(isValid);
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
