namespace LKCode.AddressManagement.Service.OutputCachePolicies;

/// <summary>
/// The tags of the cached address responses. A change of an address removes all responses with the affected tags
/// from the output cache.
/// </summary>
public static class AddressCacheTags
{
    /// <summary>
    /// The tag of every cached page of the address list.
    /// </summary>
    public const string List = "addresses";

    /// <summary>
    /// Gets the tag of the cached response of the address with the <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The id of the address.</param>
    /// <returns>
    /// <c>address-</c> followed by the id in lowercase with hyphens, e.g.
    /// <c>address-0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00</c>, whatever format the request used.
    /// </returns>
    public static string ForAddress(Guid id) => throw new NotImplementedException();
}
