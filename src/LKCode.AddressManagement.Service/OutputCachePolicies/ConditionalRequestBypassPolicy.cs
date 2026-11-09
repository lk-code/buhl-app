using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

namespace LKCode.AddressManagement.Service.OutputCachePolicies;

/// <summary>
/// Answers conditional requests (<c>If-None-Match</c>, <c>If-Modified-Since</c>) never from the output cache.
/// </summary>
/// <remarks>
/// The output cache would answer them with <c>304 Not Modified</c> if a matching response is cached, while the
/// endpoints themselves do not evaluate these headers and answer with <c>200 OK</c>. Without the policy, the answer
/// would depend on the state of the cache. The response of a conditional request is still stored.
/// </remarks>
public sealed class ConditionalRequestBypassPolicy : IOutputCachePolicy
{
    /// <summary>
    /// Disables the lookup in the cache if the request contains <c>If-None-Match</c> or <c>If-Modified-Since</c>.
    /// </summary>
    /// <param name="context">The context of the request.</param>
    /// <param name="cancellation">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The same check as in the output cache middleware, which evaluates these headers only if they are not empty.
        IHeaderDictionary headers = context.HttpContext.Request.Headers;
        if (!StringValues.IsNullOrEmpty(headers.IfNoneMatch) || !StringValues.IsNullOrEmpty(headers.IfModifiedSince))
        {
            context.AllowCacheLookup = false;
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Does nothing; the policy only decides about the lookup.
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
    /// Does nothing; the policy only decides about the lookup.
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
