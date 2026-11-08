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
    /// <remarks>The numbers are kept, so that the validation reports invalid ones.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    public static AddressListQuery Sanitize(this AddressListQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query with
        {
            City = SanitizeOptional(query.City),
            PostalCode = SanitizeOptional(query.PostalCode),
            SortBy = SanitizeOptional(query.SortBy),
            SortDirection = SanitizeOptional(query.SortDirection),
        };
    }

    private static string? SanitizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
