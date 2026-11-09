using AwesomeAssertions;
using LKCode.AddressManagement.Service.OutputCachePolicies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

namespace LKCode.AddressManagement.UnitTests.Service.OutputCachePolicies;

public class ConditionalRequestBypassPolicyTests
{
    private readonly ConditionalRequestBypassPolicy _policy = new();

    [TestCase("If-None-Match", "\"3\"")]
    [TestCase("If-None-Match", "W/\"3\"")]
    [TestCase("If-None-Match", "*")]
    [TestCase("If-None-Match", "\"1\", \"2\"")]
    [TestCase("If-None-Match", "invalid")]
    [TestCase("If-Modified-Since", "Sat, 14 Mar 2026 09:26:53 GMT")]
    [TestCase("If-Modified-Since", "invalid")]
    [TestCase("if-none-match", "*")]
    [TestCase("IF-MODIFIED-SINCE", "Sat, 14 Mar 2026 09:26:53 GMT")]
    public async Task CacheRequestAsync_ConditionalRequest_DisablesLookupOnly(string headerName, string value)
    {
        // Arrange
        OutputCacheContext context = CreateContext();
        context.HttpContext.Request.Headers[headerName] = value;

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheLookup.Should().BeFalse();
        context.AllowCacheStorage.Should().BeTrue();
    }

    [Test]
    public async Task CacheRequestAsync_BothConditionalHeaders_DisablesLookupOnly()
    {
        // Arrange
        OutputCacheContext context = CreateContext();
        context.HttpContext.Request.Headers.IfNoneMatch = "\"3\"";
        context.HttpContext.Request.Headers.IfModifiedSince = "Sat, 14 Mar 2026 09:26:53 GMT";

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheLookup.Should().BeFalse();
        context.AllowCacheStorage.Should().BeTrue();
    }

    [TestCase(null, null)]
    [TestCase("If-Match", "\"3\"")]
    [TestCase("If-Unmodified-Since", "Sat, 14 Mar 2026 09:26:53 GMT")]
    [TestCase("ETag", "\"3\"")]
    [TestCase("Accept", "application/json")]
    public async Task CacheRequestAsync_NoConditionalRequest_KeepsLookupAndStorageAllowed(
        string? headerName,
        string? value)
    {
        // Arrange
        OutputCacheContext context = CreateContext();
        if (headerName is not null)
        {
            context.HttpContext.Request.Headers[headerName] = value;
        }

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheLookup.Should().BeTrue();
        context.AllowCacheStorage.Should().BeTrue();
    }

    [TestCase("If-None-Match")]
    [TestCase("If-Modified-Since")]
    public async Task CacheRequestAsync_EmptyConditionalHeader_KeepsLookupAndStorageAllowed(string headerName)
    {
        // Arrange
        OutputCacheContext context = CreateContext();
        context.HttpContext.Request.Headers[headerName] = string.Empty;

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheLookup.Should().BeTrue();
        context.AllowCacheStorage.Should().BeTrue();
    }

    [Test]
    public async Task CacheRequestAsync_ConditionalRequest_AddsNoTags()
    {
        // Arrange
        OutputCacheContext context = CreateContext();
        context.HttpContext.Request.Headers[HeaderNames.IfNoneMatch] = "*";

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.Tags.Should().BeEmpty();
    }

    [Test]
    public async Task ServeFromCacheAsync_ConditionalRequest_ChangesNothing()
    {
        // Arrange
        OutputCacheContext context = CreateContext();
        context.HttpContext.Request.Headers[HeaderNames.IfNoneMatch] = "*";

        // Act
        await _policy.ServeFromCacheAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheLookup.Should().BeTrue();
        context.AllowCacheStorage.Should().BeTrue();
    }

    [Test]
    public async Task ServeResponseAsync_ConditionalRequest_ChangesNothing()
    {
        // Arrange
        OutputCacheContext context = CreateContext();
        context.HttpContext.Request.Headers[HeaderNames.IfNoneMatch] = "*";

        // Act
        await _policy.ServeResponseAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheLookup.Should().BeTrue();
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

    /// <summary>
    /// Creates a context in which lookup and storage are allowed, as after the default policy.
    /// </summary>
    private static OutputCacheContext CreateContext()
    {
        return new OutputCacheContext
        {
            HttpContext = new DefaultHttpContext(),
            AllowCacheLookup = true,
            AllowCacheStorage = true,
        };
    }
}
