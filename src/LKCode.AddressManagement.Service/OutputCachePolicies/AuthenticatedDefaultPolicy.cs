using Microsoft.AspNetCore.OutputCaching;

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
    /// Enables the output cache and allows lookup and storage for <c>GET</c> and <c>HEAD</c> requests.
    /// </summary>
    /// <param name="context">The context of the request.</param>
    /// <param name="cancellation">The token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}
