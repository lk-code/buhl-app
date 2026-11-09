using AwesomeAssertions;
using LKCode.AddressManagement.Service.OpenApiTransformers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.UnitTests.Service.OpenApiTransformers;

public class BearerSecuritySchemeDocumentTransformerTests
{
    private const string Bearer = "Bearer";

    private readonly BearerSecuritySchemeDocumentTransformer _transformer = new();
    private ServiceProvider _serviceProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = new ServiceCollection().BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task TransformAsync_Document_AddsHttpBearerSchemeForJwt()
    {
        // Arrange
        var document = new OpenApiDocument();

        // Act
        await _transformer.TransformAsync(document, CreateContext(), CancellationToken.None);

        // Assert
        document.Components!.SecuritySchemes!.Keys.Should().Equal(Bearer);
        IOpenApiSecurityScheme scheme = document.Components.SecuritySchemes[Bearer];
        scheme.Type.Should().Be(SecuritySchemeType.Http);
        scheme.Scheme.Should().Be("bearer");
        scheme.BearerFormat.Should().Be("JWT");
        scheme.Description.Should().Contain("addresses:read").And.Contain("addresses:write");
    }

    [Test]
    public async Task TransformAsync_Document_RegistersSchemeForReferences()
    {
        // Arrange
        var document = new OpenApiDocument();

        // Act
        await _transformer.TransformAsync(document, CreateContext(), CancellationToken.None);

        // Assert
        var reference = new OpenApiSecuritySchemeReference(Bearer, document);
        reference.Target.Should().NotBeNull();
        reference.Target.Scheme.Should().Be("bearer");
    }

    [Test]
    public async Task TransformAsync_DocumentWithComponents_KeepsThem()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object };
        var document = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema> { ["AddressResponse"] = schema },
            },
        };

        // Act
        await _transformer.TransformAsync(document, CreateContext(), CancellationToken.None);

        // Assert
        document.Components.Schemas.Should().ContainSingle().Which.Value.Should().BeSameAs(schema);
        document.Components.SecuritySchemes.Should().ContainKey(Bearer);
    }

    [Test]
    public async Task TransformAsync_Document_AddsNoGlobalSecurityRequirement()
    {
        // Arrange
        var document = new OpenApiDocument();

        // Act
        await _transformer.TransformAsync(document, CreateContext(), CancellationToken.None);

        // Assert
        document.Security.Should().BeNullOrEmpty();
    }

    [Test]
    public async Task TransformAsync_NullDocument_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = () => _transformer.TransformAsync(null!, CreateContext(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("document");
    }

    [Test]
    public async Task TransformAsync_NullContext_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = () => _transformer.TransformAsync(new OpenApiDocument(), null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    private OpenApiDocumentTransformerContext CreateContext() => new()
    {
        DocumentName = "v1",
        DescriptionGroups = [],
        ApplicationServices = _serviceProvider,
    };
}
