using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.Core.Addresses.Sanitizers;

/// <summary>
/// Cleans the values of address requests before they are validated.
/// </summary>
public static class AddressRequestSanitizerExtensions
{
    /// <summary>
    /// Cleans the values of the <paramref name="request"/>: trims all values, converts the country to uppercase and
    /// treats a blank email address as <see langword="null"/>.
    /// </summary>
    /// <param name="request">The request to clean.</param>
    /// <returns>A new request with the cleaned values.</returns>
    /// <remarks>A missing required value becomes an empty string, so that the validation reports it.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public static CreateAddressRequest Sanitize(this CreateAddressRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request with
        {
            FirstName = SanitizeRequired(request.FirstName),
            LastName = SanitizeRequired(request.LastName),
            Street = SanitizeRequired(request.Street),
            HouseNumber = SanitizeRequired(request.HouseNumber),
            PostalCode = SanitizeRequired(request.PostalCode),
            City = SanitizeRequired(request.City),
            Country = SanitizeCountry(request.Country),
            Email = SanitizeOptional(request.Email),
        };
    }

    /// <summary>
    /// Cleans the values of the <paramref name="request"/>: trims all values, converts the country to uppercase and
    /// treats a blank email address as <see langword="null"/>.
    /// </summary>
    /// <param name="request">The request to clean.</param>
    /// <returns>A new request with the cleaned values.</returns>
    /// <remarks>A missing required value becomes an empty string, so that the validation reports it.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public static UpdateAddressRequest Sanitize(this UpdateAddressRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request with
        {
            FirstName = SanitizeRequired(request.FirstName),
            LastName = SanitizeRequired(request.LastName),
            Street = SanitizeRequired(request.Street),
            HouseNumber = SanitizeRequired(request.HouseNumber),
            PostalCode = SanitizeRequired(request.PostalCode),
            City = SanitizeRequired(request.City),
            Country = SanitizeCountry(request.Country),
            Email = SanitizeOptional(request.Email),
        };
    }

    /// <summary>
    /// Trims a required value. <see langword="null"/>, which a client can send despite the declared type, becomes an
    /// empty string.
    /// </summary>
    private static string SanitizeRequired(string? value) => value?.Trim() ?? string.Empty;

    private static string SanitizeCountry(string? value) => SanitizeRequired(value).ToUpperInvariant();

    private static string? SanitizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
