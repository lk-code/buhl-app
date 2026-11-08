using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Domain.Addresses;
using LKCode.AddressManagement.Domain.Contracts;

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

    /// <summary>
    /// Creates a new address from the <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The sanitized and validated request.</param>
    /// <param name="textNormalizer">The normalizer for the city and the postal code.</param>
    /// <param name="timeProvider">The provider of the current time.</param>
    /// <returns>The new address.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/>, <paramref name="textNormalizer"/> or <paramref name="timeProvider"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A value of the request is invalid; see <see cref="Address.Create"/>.
    /// </exception>
    public static Address ToAddress(
        this CreateAddressRequest request,
        ITextNormalizer textNormalizer,
        TimeProvider timeProvider)
    {
        throw new NotImplementedException();
    }
}
