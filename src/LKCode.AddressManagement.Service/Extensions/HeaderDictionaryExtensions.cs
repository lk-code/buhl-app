using LKCode.AddressManagement.Service.Preconditions;

namespace LKCode.AddressManagement.Service.Extensions;

/// <summary>
/// Reads and writes the HTTP headers of the optimistic concurrency: <c>ETag</c> and <c>If-Match</c>
/// (RFC 9110, sections 8.8.3 and 13.1.1).
/// </summary>
/// <remarks>
/// The entity tag of a resource is its version in quotes, e.g. <c>"3"</c>. It is a strong entity tag.
/// </remarks>
public static class HeaderDictionaryExtensions
{
    /// <summary>
    /// Evaluates the <c>If-Match</c> header of a request against the <paramref name="currentVersion"/>.
    /// </summary>
    /// <param name="headers">The headers of the request.</param>
    /// <param name="currentVersion">The current version of the resource.</param>
    /// <returns>The outcome of the evaluation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> is <see langword="null"/>.</exception>
    public static IfMatchOutcome EvaluateIfMatch(this IHeaderDictionary headers, long currentVersion) =>
        throw new NotImplementedException();

    /// <summary>
    /// Sets the <c>ETag</c> header of a response to the strong entity tag of the <paramref name="version"/>.
    /// </summary>
    /// <param name="headers">The headers of the response.</param>
    /// <param name="version">The version of the resource in the response.</param>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> is <see langword="null"/>.</exception>
    public static void SetETag(this IHeaderDictionary headers, long version) =>
        throw new NotImplementedException();
}
