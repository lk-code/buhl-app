using System.Globalization;
using LKCode.AddressManagement.Service.Preconditions;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

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
    /// <remarks>
    /// The header is either <c>*</c> or a comma-separated list of entity tags, possibly split into several header
    /// lines. Entity tags are compared with the strong comparison of RFC 9110, section 8.8.3.2: a weak entity tag
    /// never matches. An empty header, a value that is no entity tag and <c>*</c> together with other values are
    /// invalid.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> is <see langword="null"/>.</exception>
    public static IfMatchOutcome EvaluateIfMatch(this IHeaderDictionary headers, long currentVersion)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (!headers.TryGetValue(HeaderNames.IfMatch, out StringValues values))
        {
            return IfMatchOutcome.Missing;
        }

        if (!EntityTagHeaderValue.TryParseStrictList(values, out IList<EntityTagHeaderValue>? entityTags))
        {
            return IfMatchOutcome.Invalid;
        }

        if (entityTags.Contains(EntityTagHeaderValue.Any))
        {
            return entityTags.Count == 1 ? IfMatchOutcome.Any : IfMatchOutcome.Invalid;
        }

        var currentEntityTag = new EntityTagHeaderValue(ToEntityTag(currentVersion));

        return entityTags.Any(entityTag => entityTag.Compare(currentEntityTag, useStrongComparison: true))
            ? IfMatchOutcome.Matched
            : IfMatchOutcome.NotMatched;
    }

    /// <summary>
    /// Sets the <c>ETag</c> header of a response to the strong entity tag of the <paramref name="version"/>.
    /// </summary>
    /// <param name="headers">The headers of the response.</param>
    /// <param name="version">The version of the resource in the response.</param>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> is <see langword="null"/>.</exception>
    public static void SetETag(this IHeaderDictionary headers, long version)
    {
        ArgumentNullException.ThrowIfNull(headers);

        headers.ETag = ToEntityTag(version);
    }

    private static string ToEntityTag(long version) =>
        string.Create(CultureInfo.InvariantCulture, $"\"{version}\"");
}
