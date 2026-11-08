namespace LKCode.AddressManagement.Service.Preconditions;

/// <summary>
/// The outcome of the evaluation of the <c>If-Match</c> header of a request (RFC 9110, section 13.1.1) against the
/// current version of a resource.
/// </summary>
public enum IfMatchOutcome
{
    /// <summary>
    /// The request has no <c>If-Match</c> header.
    /// </summary>
    Missing,

    /// <summary>
    /// The header is empty or no valid <c>If-Match</c> value: neither <c>*</c> nor a list of entity tags.
    /// </summary>
    Invalid,

    /// <summary>
    /// The header is <c>*</c>: the request applies to any current version.
    /// </summary>
    Any,

    /// <summary>
    /// One of the entity tags is the strong entity tag of the current version.
    /// </summary>
    Matched,

    /// <summary>
    /// None of the entity tags is the strong entity tag of the current version; a weak entity tag never matches.
    /// </summary>
    NotMatched,
}
