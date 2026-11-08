using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.Core.Addresses.Sanitizers;

/// <summary>
/// Cleans the values of address list queries before they are validated.
/// </summary>
public static class AddressListQuerySanitizerExtensions
{
    /// <summary>
    /// Cleans the values of the <paramref name="query"/>: trims all texts and treats blank texts as
    /// <see langword="null"/>, i.e. as missing.
    /// </summary>
    /// <param name="query">The query to clean.</param>
    /// <returns>A new query with the cleaned values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    public static AddressListQuery Sanitize(this AddressListQuery query) => throw new NotImplementedException();
}
