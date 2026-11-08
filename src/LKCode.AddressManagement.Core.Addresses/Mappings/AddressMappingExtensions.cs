using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Domain.Addresses;

namespace LKCode.AddressManagement.Core.Addresses.Mappings;

/// <summary>
/// Maps between the address requests and responses and the entity <see cref="Address"/>.
/// </summary>
public static class AddressMappingExtensions
{
    /// <summary>
    /// Maps the <paramref name="address"/> to a response without the normalized fields.
    /// </summary>
    /// <param name="address">The address to map.</param>
    /// <returns>The response with the values of the address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is <see langword="null"/>.</exception>
    public static AddressResponse ToResponse(this Address address)
    {
        ArgumentNullException.ThrowIfNull(address);

        return new AddressResponse(
            address.Id,
            address.FirstName,
            address.LastName,
            address.Street,
            address.HouseNumber,
            address.PostalCode,
            address.City,
            address.Country,
            address.Email,
            address.CreatedAt,
            address.UpdatedAt);
    }
}
