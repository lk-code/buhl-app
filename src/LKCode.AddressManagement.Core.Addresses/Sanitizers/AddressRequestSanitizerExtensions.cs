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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}
