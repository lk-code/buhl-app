namespace LKCode.AddressManagement.Core.Addresses.Validators;

/// <summary>
/// The officially assigned ISO 3166-1 alpha-2 country codes.
/// </summary>
public static class CountryCodes
{
    /// <summary>
    /// Checks whether the <paramref name="code"/> is an officially assigned ISO 3166-1 alpha-2 country code.
    /// </summary>
    /// <param name="code">The code to check; it must be uppercase.</param>
    /// <returns>
    /// <see langword="true"/> if the code is assigned; <see langword="false"/> if it is unknown, not uppercase or
    /// <see langword="null"/>.
    /// </returns>
    public static bool IsValid(string? code)
    {
        throw new NotImplementedException();
    }
}
