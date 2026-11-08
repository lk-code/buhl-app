namespace LKCode.AddressManagement.Core.Addresses.Dtos;

/// <summary>
/// An address as returned to clients.
/// </summary>
/// <param name="Id">The unique identifier.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
/// <param name="Street">The street.</param>
/// <param name="HouseNumber">The house number.</param>
/// <param name="PostalCode">The postal code.</param>
/// <param name="City">The city.</param>
/// <param name="Country">The country as uppercase ISO 3166-1 alpha-2 code, e.g. <c>DE</c>.</param>
/// <param name="Email">The email address, or <see langword="null"/> if none is set.</param>
/// <param name="CreatedAt">The point in time the address was created.</param>
/// <param name="UpdatedAt">The point in time the address was last changed.</param>
public sealed record AddressResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Street,
    string HouseNumber,
    string PostalCode,
    string City,
    string Country,
    string? Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
