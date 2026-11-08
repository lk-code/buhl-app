namespace LKCode.AddressManagement.Core.Addresses.Contracts;

/// <summary>
/// The values of an address sent by a client, shared by the requests to create and to update an address.
/// </summary>
public interface IAddressRequest
{
    /// <summary>
    /// Gets the first name.
    /// </summary>
    string FirstName { get; }

    /// <summary>
    /// Gets the last name.
    /// </summary>
    string LastName { get; }

    /// <summary>
    /// Gets the street.
    /// </summary>
    string Street { get; }

    /// <summary>
    /// Gets the house number.
    /// </summary>
    string HouseNumber { get; }

    /// <summary>
    /// Gets the postal code.
    /// </summary>
    string PostalCode { get; }

    /// <summary>
    /// Gets the city.
    /// </summary>
    string City { get; }

    /// <summary>
    /// Gets the country as uppercase ISO 3166-1 alpha-2 code, e.g. <c>DE</c>.
    /// </summary>
    string Country { get; }

    /// <summary>
    /// Gets the optional email address.
    /// </summary>
    string? Email { get; }
}
