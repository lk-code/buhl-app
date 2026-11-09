using Microsoft.AspNetCore.OutputCaching;

namespace LKCode.AddressManagement.Service.OutputCachePolicies;

/// <summary>
/// Tags the cached response of a single address with <see cref="AddressCacheTags.ForAddress"/>, so that a change of
/// the address removes it from the output cache.
/// </summary>
/// <remarks>
/// The id comes from the route value <c>id</c>. Without a valid id, the policy disables the cache for the request:
/// a response without the tag could not be removed by a change.
/// </remarks>
public sealed class AddressIdTagPolicy : IOutputCachePolicy
{
    private const string IdRouteValueName = "id";

    /// <summary>
    /// Adds the tag of the address to the <paramref name="context"/>, or disables the cache without a valid id.
    /// </summary>
    /// <param name="context">The context of the request.</param>
    /// <param name="cancellation">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.HttpContext.Request.RouteValues.TryGetValue(IdRouteValueName, out object? value)
            && value is string text
            && Guid.TryParse(text, out Guid id))
        {
            context.Tags.Add(AddressCacheTags.ForAddress(id));
        }
        else
        {
            context.AllowCacheLookup = false;
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Does nothing; the tag only matters when a response is stored.
    /// </summary>
    /// <param name="context">The context of the request.</param>
    /// <param name="cancellation">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Does nothing; the tag only matters when a response is stored.
    /// </summary>
    /// <param name="context">The context of the request.</param>
    /// <param name="cancellation">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ValueTask.CompletedTask;
    }
}
