using Microsoft.AspNetCore.Authorization;

namespace LKCode.AddressManagement.Service.AuthorizationPolicies;

/// <summary>
/// Evaluates the <see cref="ScopeRequirement"/>: the user must have the required scope in a scope claim of an
/// authenticated identity.
/// </summary>
/// <remarks>
/// Scope claims are the claims of the type <c>scope</c> (RFC 8693, RFC 9068; e.g. <c>dotnet user-jwts</c> and
/// Keycloak) and <c>scp</c> (e.g. Entra ID). A claim may contain one scope or several scopes separated by spaces
/// (RFC 6749, section 3.3); a token may also contain several scope claims, e.g. as a JSON array. Scopes are compared
/// exactly, including their case. The claim types are expected as they are in the token, i.e. without the mapping of
/// inbound claims.
/// </remarks>
public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    /// <summary>
    /// Marks the <paramref name="requirement"/> as successful if the user has its scope.
    /// </summary>
    /// <param name="context">The context of the authorization with the user.</param>
    /// <param name="requirement">The requirement to evaluate.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="requirement"/> is <see langword="null"/>.
    /// </exception>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeRequirement requirement)
    {
        throw new NotImplementedException();
    }
}
