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
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the required scope.
    /// </summary>
    public string Scope { get; }

    /// <summary>
    /// Returns a description of the requirement for the log of failed authorizations.
    /// </summary>
    /// <returns>The name of the requirement and the required scope.</returns>
    public override string ToString() => throw new NotImplementedException();
}
