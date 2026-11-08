namespace LKCode.AddressManagement.Core.Addresses.Dtos;

/// <summary>
/// The values of a new address.
/// </summary>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
/// <param name="Street">The street.</param>
/// <param name="HouseNumber">The house number.</param>
/// <param name="PostalCode">The postal code.</param>
/// <param name="City">The city.</param>
/// <param name="Country">The country as uppercase ISO 3166-1 alpha-2 code, e.g. <c>DE</c>.</param>
/// <param name="Email">The optional email address.</param>
public sealed record CreateAddressRequest(
    string FirstName,
    string LastName,
    string Street,
    string HouseNumber,
    string PostalCode,
    string City,
    string Country,
    string? Email);
