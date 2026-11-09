using System.Security.Claims;
using AwesomeAssertions;
using LKCode.AddressManagement.Service.OutputCachePolicies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

namespace LKCode.AddressManagement.UnitTests.Service.OutputCachePolicies;

public class AuthenticatedDefaultPolicyTests
{
    private readonly AuthenticatedDefaultPolicy _policy = new();

    [TestCase("GET")]
    [TestCase("HEAD")]
    [TestCase("get")]
    [TestCase("head")]
    public async Task CacheRequestAsync_GetOrHead_EnablesCachingWithLookupStorageAndLocking(string method)
    {
        // Arrange
        OutputCacheContext context = CreateContext(method);

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.EnableOutputCaching.Should().BeTrue();
        context.AllowCacheLookup.Should().BeTrue();
        context.AllowCacheStorage.Should().BeTrue();
        context.AllowLocking.Should().BeTrue();
    }

    [TestCase("GET")]
    [TestCase("HEAD")]
    public async Task CacheRequestAsync_AuthenticatedRequestWithAuthorizationHeader_AllowsLookupAndStorage(
        string method)
    {
        // Arrange
        OutputCacheContext context = CreateContext(method);
        context.HttpContext.Request.Headers.Authorization = "Bearer token";
        context.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim("scope", "addresses:read")], "Bearer"));

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheLookup.Should().BeTrue();
        context.AllowCacheStorage.Should().BeTrue();
    }

    [TestCase("POST")]
    [TestCase("PUT")]
    [TestCase("PATCH")]
    [TestCase("DELETE")]
    [TestCase("OPTIONS")]
    [TestCase("TRACE")]
    public async Task CacheRequestAsync_OtherMethod_DisallowsLookupAndStorage(string method)
    {
        // Arrange
        OutputCacheContext context = CreateContext(method);
        context.AllowCacheLookup = true;
        context.AllowCacheStorage = true;

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.EnableOutputCaching.Should().BeTrue();
        context.AllowCacheLookup.Should().BeFalse();
        context.AllowCacheStorage.Should().BeFalse();
    }

    [Test]
    public async Task CacheRequestAsync_Get_VariesByAllQueryParameters()
    {
        // Arrange
        OutputCacheContext context = CreateContext(HttpMethods.Get);

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.CacheVaryByRules.QueryKeys.ToString().Should().Be("*");
    }

    [Test]
    public async Task CacheRequestAsync_Get_AddsNoTags()
    {
        // Arrange
        OutputCacheContext context = CreateContext(HttpMethods.Get);

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.Tags.Should().BeEmpty();
    }

    [Test]
    public async Task ServeFromCacheAsync_Request_ChangesNothing()
    {
        // Arrange
        OutputCacheContext context = CreateContext(HttpMethods.Get);
        context.AllowCacheLookup = true;
        context.AllowCacheStorage = true;

        // Act
        await _policy.ServeFromCacheAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheLookup.Should().BeTrue();
        context.AllowCacheStorage.Should().BeTrue();
    }

    [Test]
    public async Task ServeResponseAsync_OkWithoutCookie_KeepsStorageAllowed()
    {
        // Arrange
        OutputCacheContext context = CreateResponseContext(StatusCodes.Status200OK);

        // Act
        await _policy.ServeResponseAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheStorage.Should().BeTrue();
    }

    [TestCase(StatusCodes.Status201Created)]
    [TestCase(StatusCodes.Status204NoContent)]
    [TestCase(StatusCodes.Status304NotModified)]
    [TestCase(StatusCodes.Status400BadRequest)]
    [TestCase(StatusCodes.Status401Unauthorized)]
    [TestCase(StatusCodes.Status403Forbidden)]
    [TestCase(StatusCodes.Status404NotFound)]
    [TestCase(StatusCodes.Status500InternalServerError)]
    public async Task ServeResponseAsync_OtherStatusCode_DisallowsStorage(int statusCode)
    {
        // Arrange
        OutputCacheContext context = CreateResponseContext(statusCode);

        // Act
        await _policy.ServeResponseAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheStorage.Should().BeFalse();
    }

    [Test]
    public async Task ServeResponseAsync_OkWithCookie_DisallowsStorage()
    {
        // Arrange
        OutputCacheContext context = CreateResponseContext(StatusCodes.Status200OK);
        context.HttpContext.Response.Headers.SetCookie = "session=1";

        // Act
        await _policy.ServeResponseAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheStorage.Should().BeFalse();
    }

    [Test]
    public async Task ServeResponseAsync_OkWithEmptySetCookieHeader_KeepsStorageAllowed()
    {
        // Arrange
        OutputCacheContext context = CreateResponseContext(StatusCodes.Status200OK);
        context.HttpContext.Response.Headers[HeaderNames.SetCookie] = string.Empty;

        // Act
        await _policy.ServeResponseAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheStorage.Should().BeTrue();
    }

    [Test]
    public async Task CacheRequestAsync_NullContext_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _policy.CacheRequestAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Test]
    public async Task ServeFromCacheAsync_NullContext_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _policy.ServeFromCacheAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Test]
    public async Task ServeResponseAsync_NullContext_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _policy.ServeResponseAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    private static OutputCacheContext CreateContext(string method)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;

        return new OutputCacheContext { HttpContext = httpContext };
    }

    /// <summary>
    /// Creates the context of a <c>GET</c> request with a response, in which storage is allowed.
    /// </summary>
    private static OutputCacheContext CreateResponseContext(int statusCode)
    {
        OutputCacheContext context = CreateContext(HttpMethods.Get);
        context.HttpContext.Response.StatusCode = statusCode;
        context.AllowCacheStorage = true;

        return context;
    }
}
