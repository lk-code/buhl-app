namespace LKCode.AddressManagement.Service.AuthorizationPolicies;

/// <summary>
/// The scopes that an access token must grant to use the address endpoints.
/// </summary>
public static class AddressScopes
{
    /// <summary>
    /// The scope to read addresses: get a single address and list addresses.
    /// </summary>
    public const string Read = "addresses:read";

    /// <summary>
    /// The scope to change addresses: create, replace, patch and delete them.
    /// </summary>
    public const string Write = "addresses:write";
}
