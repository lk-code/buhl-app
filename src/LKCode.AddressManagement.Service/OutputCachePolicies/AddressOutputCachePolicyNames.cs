namespace LKCode.AddressManagement.Service.OutputCachePolicies;

/// <summary>
/// The names of the output cache policies of the address endpoints, registered by
/// <see cref="ServiceCollectionExtensions.AddAddressCaching"/>.
/// </summary>
public static class AddressOutputCachePolicyNames
{
    /// <summary>
    /// The policy of a single address: tagged with <see cref="AddressCacheTags.ForAddress"/>.
    /// </summary>
    public const string ById = "AddressById";

    /// <summary>
    /// The policy of the address list: tagged with <see cref="AddressCacheTags.List"/> and varied by all query
    /// parameters.
    /// </summary>
    public const string List = "AddressList";
}
