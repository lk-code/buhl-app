namespace LKCode.AddressManagement.Core.Addresses.Queries;

/// <summary>
/// The fields an address list can be sorted by.
/// </summary>
/// <remarks>
/// The names used in the API are defined in <see cref="AddressSortFields"/>. Addresses with the same value are
/// additionally sorted by their id, so that the order is stable.
/// </remarks>
public enum AddressSortField
{
    /// <summary>
    /// The last name.
    /// </summary>
    LastName,

    /// <summary>
    /// The first name.
    /// </summary>
    FirstName,

    /// <summary>
    /// The city as entered.
    /// </summary>
    City,

    /// <summary>
    /// The postal code as entered.
    /// </summary>
    PostalCode,

    /// <summary>
    /// The country code.
    /// </summary>
    Country,

    /// <summary>
    /// The point in time the address was created.
    /// </summary>
    CreatedAt,

    /// <summary>
    /// The point in time the address was last changed.
    /// </summary>
    UpdatedAt,
}
