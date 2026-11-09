using Microsoft.AspNetCore.Authorization;

namespace LKCode.AddressManagement.Service.AuthorizationPolicies;

/// <summary>
/// Requires that the access token of the user grants the <see cref="Scope"/>.
/// </summary>
/// <remarks>
/// The <see cref="ScopeAuthorizationHandler"/> evaluates the requirement.
/// </remarks>
public sealed class ScopeRequirement : IAuthorizationRequirement
{
    private const string InvalidScopeMessage =
        "The scope must consist of printable ASCII characters other than space, '\"' and '\\' (RFC 6749, section "
        + "3.3).";

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeRequirement"/> class.
    /// </summary>
    /// <param name="scope">
    /// The required scope: a scope token of RFC 6749, section 3.3, i.e. at least one printable ASCII character other
    /// than space, <c>"</c> and <c>\</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="scope"/> is no valid scope token.</exception>
    public ScopeRequirement(string scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Length == 0 || !scope.All(IsScopeTokenCharacter))
        {
            throw new ArgumentException(InvalidScopeMessage, nameof(scope));
        }

        Scope = scope;
    }

    /// <summary>
    /// Gets the required scope.
    /// </summary>
    public string Scope { get; }

    /// <summary>
    /// Returns a description of the requirement for the log of failed authorizations.
    /// </summary>
    /// <returns>The name of the requirement and the required scope.</returns>
    public override string ToString() => $"{nameof(ScopeRequirement)}:{nameof(Scope)}={Scope}";

    /// <summary>
    /// Determines whether the <paramref name="character"/> may appear in a scope token: <c>%x21</c>,
    /// <c>%x23-5B</c> or <c>%x5D-7E</c> (RFC 6749, section 3.3).
    /// </summary>
    private static bool IsScopeTokenCharacter(char character) =>
        character is '!' or (>= '#' and <= '[') or (>= ']' and <= '~');
}
