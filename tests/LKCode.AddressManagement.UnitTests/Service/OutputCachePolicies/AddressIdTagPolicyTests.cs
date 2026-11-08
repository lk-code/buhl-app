using AwesomeAssertions;
using LKCode.AddressManagement.Service.OutputCachePolicies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;

namespace LKCode.AddressManagement.UnitTests.Service.OutputCachePolicies;

public class AddressIdTagPolicyTests
{
    private const string Id = "0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00";
    private const string ExpectedTag = $"address-{Id}";

    private readonly AddressIdTagPolicy _policy = new();

    [TestCase(Id)]
    [TestCase("0198F2A4-6E0B-7C3D-9A1E-5B4C3D2E1F00")]
    [TestCase("0198f2a46e0b7c3d9a1e5b4c3d2e1f00")]
    [TestCase("{0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00}")]
    [TestCase("(0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00)")]
    public async Task CacheRequestAsync_ValidIdInAnyFormat_AddsTagOfAddress(string id)
    {
        // Arrange
        OutputCacheContext context = CreateContext(id);

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.Tags.Should().Equal(ExpectedTag);
    }

    [Test]
    public async Task CacheRequestAsync_ValidId_KeepsLookupAndStorageAllowed()
    {
        // Arrange
        OutputCacheContext context = CreateContext(Id);

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheLookup.Should().BeTrue();
        context.AllowCacheStorage.Should().BeTrue();
    }

    [Test]
    public async Task CacheRequestAsync_ExistingTags_KeepsThemAndAddsTagOfAddress()
    {
        // Arrange
        OutputCacheContext context = CreateContext(Id);
        context.Tags.Add("other");

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.Tags.Should().BeEquivalentTo("other", ExpectedTag);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("not-a-guid")]
    [TestCase("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f0")]
    [TestCase("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1fzz")]
    public async Task CacheRequestAsync_InvalidId_DisablesLookupAndStorageWithoutTag(string? id)
    {
        // Arrange
        OutputCacheContext context = CreateContext(id);

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheLookup.Should().BeFalse();
        context.AllowCacheStorage.Should().BeFalse();
        context.Tags.Should().BeEmpty();
    }

    [Test]
    public async Task CacheRequestAsync_NoIdRouteValue_DisablesLookupAndStorageWithoutTag()
    {
        // Arrange
        OutputCacheContext context = CreateContext();

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheLookup.Should().BeFalse();
        context.AllowCacheStorage.Should().BeFalse();
        context.Tags.Should().BeEmpty();
    }

    [Test]
    public async Task CacheRequestAsync_IdRouteValueIsNoString_DisablesLookupAndStorageWithoutTag()
    {
        // Arrange
        OutputCacheContext context = CreateContext();
        context.HttpContext.Request.RouteValues["id"] = 42;

        // Act
        await _policy.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheLookup.Should().BeFalse();
        context.AllowCacheStorage.Should().BeFalse();
        context.Tags.Should().BeEmpty();
    }

    [Test]
    public async Task ServeFromCacheAsync_AnyContext_ChangesNothing()
    {
        // Arrange
        OutputCacheContext context = CreateContext(Id);

        // Act
        await _policy.ServeFromCacheAsync(context, CancellationToken.None);

        // Assert
        AssertUnchanged(context);
    }

    [Test]
    public async Task ServeResponseAsync_AnyContext_ChangesNothing()
    {
        // Arrange
        OutputCacheContext context = CreateContext(Id);

        // Act
        await _policy.ServeResponseAsync(context, CancellationToken.None);

        // Assert
        AssertUnchanged(context);
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

    private static OutputCacheContext CreateContext(string? id)
    {
        OutputCacheContext context = CreateContext();
        context.HttpContext.Request.RouteValues["id"] = id;

        return context;
    }

    private static void AssertUnchanged(OutputCacheContext context)
    {
        context.AllowCacheLookup.Should().BeTrue();
        context.AllowCacheStorage.Should().BeTrue();
        context.Tags.Should().BeEmpty();
    }
}
