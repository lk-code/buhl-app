namespace LKCode.AddressManagement.Service.AuthorizationPolicies;

/// <summary>
/// The names of the authorization policies of the address endpoints, registered by
/// <see cref="ServiceCollectionExtensions.AddAddressAuthorization"/>.
/// </summary>
public static class AddressAuthorizationPolicyNames
{
    /// <summary>
    /// The policy of the endpoints that read addresses: an authenticated user with the scope
    /// <see cref="AddressScopes.Read"/>.
    /// </summary>
    public const string Read = "AddressesRead";

    /// <summary>
    /// The policy of the endpoints that change addresses: an authenticated user with the scope
    /// <see cref="AddressScopes.Write"/>.
    /// </summary>
    public const string Write = "AddressesWrite";
}
