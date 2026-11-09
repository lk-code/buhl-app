using AwesomeAssertions;
using LKCode.AddressManagement.Service;
using LKCode.AddressManagement.Service.AuthorizationPolicies;
using LKCode.AddressManagement.Service.OpenApiTransformers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace LKCode.AddressManagement.UnitTests.Service.OpenApiTransformers;

public class SecurityRequirementOperationTransformerTests
{
    private const string Bearer = "Bearer";
    private const string WwwAuthenticate = "WWW-Authenticate";

    private ServiceProvider _serviceProvider = null!;
    private SecurityRequirementOperationTransformer _transformer = null!;
    private OpenApiDocument _document = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = new ServiceCollection().AddLogging().AddAddressAuthorization().BuildServiceProvider();
        _transformer = new SecurityRequirementOperationTransformer(
            _serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>());
        _document = new OpenApiDocument();
        _document.AddComponent<IOpenApiSecurityScheme>(
            Bearer,
            new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer" });
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [TestCase(AddressAuthorizationPolicyNames.Read, AddressScopes.Read)]
    [TestCase(AddressAuthorizationPolicyNames.Write, AddressScopes.Write)]
    public async Task TransformAsync_AddressPolicy_RequiresBearerWithScopeOfPolicy(string policyName, string scope)
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await _transformer.TransformAsync(
            operation,
            CreateContext(new AuthorizeAttribute(policyName)),
            CancellationToken.None);

        // Assert
        GetScopesOfBearer(operation).Should().Equal(scope);
    }

    [Test]
    public async Task TransformAsync_ReadAndWritePolicies_RequiresBearerWithBothScopes()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await _transformer.TransformAsync(
            operation,
            CreateContext(
                new AuthorizeAttribute(AddressAuthorizationPolicyNames.Read),
                new AuthorizeAttribute(AddressAuthorizationPolicyNames.Write)),
            CancellationToken.None);

        // Assert
        GetScopesOfBearer(operation).Should().Equal(AddressScopes.Read, AddressScopes.Write);
    }

    [Test]
    public async Task TransformAsync_AuthorizeWithoutPolicy_RequiresBearerWithoutScopes()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await _transformer.TransformAsync(operation, CreateContext(new AuthorizeAttribute()), CancellationToken.None);

        // Assert
        GetScopesOfBearer(operation).Should().BeEmpty();
    }

    [Test]
    public async Task TransformAsync_NoAuthorizationMetadata_RequiresBearerOfFallbackPolicyWithoutScopes()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await _transformer.TransformAsync(operation, CreateContext(), CancellationToken.None);

        // Assert
        GetScopesOfBearer(operation).Should().BeEmpty();
    }

    [Test]
    public async Task TransformAsync_NoAuthorizationMetadataAndNoFallbackPolicy_AddsNoSecurity()
    {
        // Arrange
        await using ServiceProvider provider = new ServiceCollection().AddLogging().AddAuthorization()
            .BuildServiceProvider();
        var transformer = new SecurityRequirementOperationTransformer(
            provider.GetRequiredService<IAuthorizationPolicyProvider>());
        var operation = new OpenApiOperation();

        // Act
        await transformer.TransformAsync(operation, CreateContext(), CancellationToken.None);

        // Assert
        operation.Security.Should().BeNullOrEmpty();
    }

    [Test]
    public async Task TransformAsync_AllowAnonymous_AddsNoSecurity()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await _transformer.TransformAsync(
            operation,
            CreateContext(new AuthorizeAttribute(AddressAuthorizationPolicyNames.Read), new AllowAnonymousAttribute()),
            CancellationToken.None);

        // Assert
        operation.Security.Should().BeNullOrEmpty();
    }

    [Test]
    public async Task TransformAsync_ProtectedEndpoint_RefersToBearerSchemeOfDocument()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await _transformer.TransformAsync(operation, CreateContext(), CancellationToken.None);

        // Assert
        OpenApiSecuritySchemeReference reference =
            operation.Security.Should().ContainSingle().Which.Keys.Should().ContainSingle().Which;
        reference.Reference.Id.Should().Be(Bearer);
        reference.Target.Should().NotBeNull();
        reference.Target.Scheme.Should().Be("bearer");
    }

    [Test]
    public async Task TransformAsync_ProtectedEndpointWithUnauthorizedResponse_AddsRequiredWwwAuthenticateHeader()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("200", "401", "403", "500");

        // Act
        await _transformer.TransformAsync(
            operation,
            CreateContext(new AuthorizeAttribute(AddressAuthorizationPolicyNames.Read)),
            CancellationToken.None);

        // Assert
        IDictionary<string, IOpenApiHeader> headers = operation.Responses!["401"].Headers!;
        headers.Keys.Should().Equal(WwwAuthenticate);
        IOpenApiHeader header = headers[WwwAuthenticate];
        header.Required.Should().BeTrue();
        header.Description.Should().Contain("Bearer");
        header.Schema!.Type.Should().Be(JsonSchemaType.String);
        operation.Responses.Where(response => response.Value.Headers is { Count: > 0 })
            .Select(response => response.Key)
            .Should().Equal("401");
    }

    [Test]
    public async Task TransformAsync_UnauthorizedResponseWithHeaders_KeepsExistingHeaders()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("401");
        var other = new OpenApiHeader { Description = "Another header." };
        ((OpenApiResponse)operation.Responses!["401"]).Headers = new Dictionary<string, IOpenApiHeader>
        {
            ["X-Other"] = other,
        };

        // Act
        await _transformer.TransformAsync(operation, CreateContext(), CancellationToken.None);

        // Assert
        operation.Responses["401"].Headers.Should().HaveCount(2)
            .And.ContainKey(WwwAuthenticate)
            .And.Contain(new KeyValuePair<string, IOpenApiHeader>("X-Other", other));
    }

    [Test]
    public async Task TransformAsync_AllowAnonymousWithUnauthorizedResponse_AddsNoHeader()
    {
        // Arrange
        OpenApiOperation operation = CreateOperation("200", "401");

        // Act
        await _transformer.TransformAsync(
            operation,
            CreateContext(new AllowAnonymousAttribute()),
            CancellationToken.None);

        // Assert
        operation.Responses!["401"].Headers.Should().BeNullOrEmpty();
    }

    [Test]
    public async Task TransformAsync_ProtectedEndpointWithoutResponses_AddsOnlySecurity()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        await _transformer.TransformAsync(operation, CreateContext(), CancellationToken.None);

        // Assert
        operation.Responses.Should().BeNullOrEmpty();
        operation.Security.Should().ContainSingle();
    }

    [Test]
    public async Task TransformAsync_UnknownPolicy_ThrowsInvalidOperationException()
    {
        // Arrange
        var operation = new OpenApiOperation();

        // Act
        Func<Task> act = () => _transformer.TransformAsync(
            operation,
            CreateContext(new AuthorizeAttribute("Unknown")),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task TransformAsync_NullOperation_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = () => _transformer.TransformAsync(null!, CreateContext(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("operation");
    }

    [Test]
    public async Task TransformAsync_NullContext_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = () => _transformer.TransformAsync(new OpenApiOperation(), null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Test]
    public void Constructor_NullPolicyProvider_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _ = new SecurityRequirementOperationTransformer(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("policyProvider");
    }

    private static List<string> GetScopesOfBearer(OpenApiOperation operation)
    {
        OpenApiSecurityRequirement requirement = operation.Security.Should().ContainSingle().Which;
        KeyValuePair<OpenApiSecuritySchemeReference, List<string>> scheme = requirement.Should().ContainSingle().Which;
        scheme.Key.Reference.Id.Should().Be(Bearer);

        return scheme.Value;
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

    private OpenApiOperationTransformerContext CreateContext(params object[] endpointMetadata) => new()
    {
        DocumentName = "v1",
        Description = new ApiDescription
        {
            ActionDescriptor = new ActionDescriptor { EndpointMetadata = [.. endpointMetadata] },
        },
        ApplicationServices = _serviceProvider,
        Document = _document,
    };
}
