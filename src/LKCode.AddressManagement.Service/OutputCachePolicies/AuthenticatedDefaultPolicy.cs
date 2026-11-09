using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

namespace LKCode.AddressManagement.Service.OutputCachePolicies;

/// <summary>
/// The base of the output cache policies of the address endpoints: the default policy of ASP.NET Core, but for
/// authenticated requests, too.
/// </summary>
/// <remarks>
/// Like the default policy, it caches only <c>GET</c> and <c>HEAD</c> requests, varies by all query parameters,
/// allows locking and stores only responses with <c>200 OK</c> and without cookies. Unlike the default policy, it
/// also caches requests with an <c>Authorization</c> header or an authenticated user: the default policy never caches
/// them, so every request to the protected endpoints would bypass the cache. This is safe, because the authentication
/// and the authorization run before the output cache, so that a cached response only answers a request that may get
/// it, and because the responses of the address endpoints do not depend on the user. A policy built on it must be
/// registered without the default policy.
/// </remarks>
public sealed class AuthenticatedDefaultPolicy : IOutputCachePolicy
{
    /// <summary>
    /// The value of <see cref="CacheVaryByRules.QueryKeys"/> that varies by all query parameters.
    /// </summary>
    private const string AllQueryKeys = "*";

    /// <summary>
    /// Enables the output cache and allows lookup and storage for <c>GET</c> and <c>HEAD</c> requests.
    /// </summary>
    /// <param name="context">The context of the request.</param>
    /// <param name="cancellation">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(context);

        string method = context.HttpContext.Request.Method;
        bool isCacheable = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = isCacheable;
        context.AllowCacheStorage = isCacheable;
        context.AllowLocking = true;
        context.CacheVaryByRules.QueryKeys = AllQueryKeys;

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Does nothing; a response is served from the cache as it was stored.
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
    /// Disallows the storage of a response that has another status code than <c>200 OK</c> or sets cookies.
    /// </summary>
    /// <param name="context">The context of the request with the response.</param>
    /// <param name="cancellation">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(context);

        HttpResponse response = context.HttpContext.Response;
        if (response.StatusCode != StatusCodes.Status200OK || !StringValues.IsNullOrEmpty(response.Headers.SetCookie))
        {
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }
}
