namespace LKCode.AddressManagement.Domain.Addresses;

/// <summary>
/// The maximum lengths of the values of an <see cref="Address"/>. These constants are the only source for the
/// validation, the database mapping and the OpenAPI definition.
/// </summary>
public static class AddressConstraints
{
    /// <summary>
    /// The maximum length of <see cref="Address.FirstName"/>.
    /// </summary>
    public const int FirstNameMaxLength = 100;

    /// <summary>
    /// The maximum length of <see cref="Address.LastName"/>.
    /// </summary>
    public const int LastNameMaxLength = 100;

    /// <summary>
    /// The maximum length of <see cref="Address.Street"/>.
    /// </summary>
    public const int StreetMaxLength = 200;

    /// <summary>
    /// The maximum length of <see cref="Address.HouseNumber"/>.
    /// </summary>
    public const int HouseNumberMaxLength = 20;

    /// <summary>
    /// The maximum length of <see cref="Address.PostalCode"/>.
    /// </summary>
    public const int PostalCodeMaxLength = 20;

    /// <summary>
    /// The maximum length of <see cref="Address.City"/>.
    /// </summary>
    public const int CityMaxLength = 100;

    /// <summary>
    /// The maximum length of <see cref="Address.Country"/>, an ISO 3166-1 alpha-2 code.
    /// </summary>
    public const int CountryMaxLength = 2;

    /// <summary>
    /// The maximum length of <see cref="Address.Email"/> (RFC 5321 path limit minus the angle brackets).
    /// </summary>
    public const int EmailMaxLength = 254;
}
