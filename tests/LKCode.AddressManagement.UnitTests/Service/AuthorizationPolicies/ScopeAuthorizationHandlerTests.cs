using System.Security.Claims;
using AwesomeAssertions;
using LKCode.AddressManagement.Service.AuthorizationPolicies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace LKCode.AddressManagement.UnitTests.Service.AuthorizationPolicies;

public class ScopeAuthorizationHandlerTests
{
    private const string AuthenticationType = "Bearer";
    private const string ReadScope = "addresses:read";
    private const string WriteScope = "addresses:write";

    private readonly ScopeAuthorizationHandler _handler = new();

    [TestCase("scope", "addresses:read")]
    [TestCase("scp", "addresses:read")]
    [TestCase("scope", "addresses:read addresses:write")]
    [TestCase("scope", "addresses:write addresses:read")]
    [TestCase("scope", "openid addresses:read profile")]
    [TestCase("scope", "  addresses:read  ")]
    [TestCase("scope", "addresses:write   addresses:read")]
    [TestCase("scp", "addresses:write addresses:read")]
    public async Task HandleAsync_ScopeInClaim_Succeeds(string claimType, string value)
    {
        // Arrange
        AuthorizationHandlerContext context = CreateContext(CreateUser((claimType, value)), ReadScope);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Test]
    public async Task HandleAsync_ScopeInOneOfSeveralClaims_Succeeds()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(("scope", WriteScope), ("scope", ReadScope));
        AuthorizationHandlerContext context = CreateContext(user, ReadScope);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Test]
    public async Task HandleAsync_ScopeInScpAndOtherScopeInScope_SucceedsForBoth()
    {
        // Arrange
        ClaimsPrincipal user = CreateUser(("scope", WriteScope), ("scp", ReadScope));
        AuthorizationHandlerContext context = CreateContext(user, ReadScope, WriteScope);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [TestCase("scope", "addresses:write")]
    [TestCase("scope", "")]
    [TestCase("scope", " ")]
    [TestCase("scope", "addresses:readonly")]
    [TestCase("scope", "xaddresses:read")]
    [TestCase("scope", "addresses")]
    [TestCase("scope", "Addresses:Read")]
    [TestCase("scope", "ADDRESSES:READ")]
    [TestCase("scope", "addresses:read,addresses:write")]
    [TestCase("scope", "addresses:read;addresses:write")]
    [TestCase("scope", "addresses:read\taddresses:write")]
    [TestCase("scope", "[\"addresses:read\"]")]
    [TestCase("Scope", "addresses:read")]
    [TestCase("SCP", "addresses:read")]
    [TestCase("scopes", "addresses:read")]
    [TestCase("role", "addresses:read")]
    [TestCase("roles", "addresses:read")]
    [TestCase("aud", "addresses:read")]
    public async Task HandleAsync_ScopeNotInScopeClaim_DoesNotSucceed(string claimType, string value)
    {
        // Arrange
        AuthorizationHandlerContext context = CreateContext(CreateUser((claimType, value)), ReadScope);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.PendingRequirements.Should().ContainSingle();
    }

    [Test]
    public async Task HandleAsync_UserWithoutClaims_DoesNotSucceed()
    {
        // Arrange
        AuthorizationHandlerContext context = CreateContext(CreateUser(), ReadScope);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Test]
    public async Task HandleAsync_AnonymousUser_DoesNotSucceed()
    {
        // Arrange
        AuthorizationHandlerContext context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()), ReadScope);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Test]
    public async Task HandleAsync_ScopeInUnauthenticatedIdentity_DoesNotSucceed()
    {
        // Arrange
        var identity = new ClaimsIdentity([new Claim("scope", ReadScope)], authenticationType: null);
        AuthorizationHandlerContext context = CreateContext(new ClaimsPrincipal(identity), ReadScope);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Test]
    public async Task HandleAsync_ScopeInSecondAuthenticatedIdentity_Succeeds()
    {
        // Arrange
        var user = new ClaimsPrincipal(
        [
            new ClaimsIdentity([new Claim("scope", WriteScope)], AuthenticationType),
            new ClaimsIdentity([new Claim("scope", ReadScope)], AuthenticationType),
        ]);
        AuthorizationHandlerContext context = CreateContext(user, ReadScope);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Test]
    public async Task HandleAsync_TwoScopeRequirements_SucceedsOnlyRequirementOfGrantedScope()
    {
        // Arrange
        AuthorizationHandlerContext context = CreateContext(CreateUser(("scope", ReadScope)), ReadScope, WriteScope);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.PendingRequirements.Should().ContainSingle()
            .Which.Should().BeOfType<ScopeRequirement>()
            .Which.Scope.Should().Be(WriteScope);
    }

    [Test]
    public async Task HandleAsync_OtherRequirementsOnly_LeavesThemPending()
    {
        // Arrange
        var requirement = new DenyAnonymousAuthorizationRequirement();
        var context = new AuthorizationHandlerContext([requirement], CreateUser(("scope", ReadScope)), null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.PendingRequirements.Should().Equal(requirement);
    }

    private static ClaimsPrincipal CreateUser(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(claim => new Claim(claim.Type, claim.Value)), AuthenticationType));

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal user, params string[] scopes) =>
        new([.. scopes.Select(scope => new ScopeRequirement(scope))], user, resource: null);
}
