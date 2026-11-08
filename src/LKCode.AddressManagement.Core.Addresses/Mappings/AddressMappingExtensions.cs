using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Queries;
using LKCode.AddressManagement.Core.Addresses.Results;
using LKCode.AddressManagement.Domain.Addresses;
using LKCode.AddressManagement.Domain.Contracts;

namespace LKCode.AddressManagement.Core.Addresses.Mappings;

/// <summary>
/// Maps between the address requests and responses and the entity <see cref="Address"/>, and from a page of addresses
/// to the list response.
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
            address.UpdatedAt,
            address.Version);
    }

    /// <summary>
    /// Maps the <paramref name="addressPage"/> to a response with the addresses on the page, the page and the number
    /// of all addresses and pages.
    /// </summary>
    /// <param name="addressPage">The page to map.</param>
    /// <param name="page">The number of the page, starting at 1.</param>
    /// <param name="pageSize">The maximum number of addresses on a page, at least 1.</param>
    /// <returns>The response with the addresses in their order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="addressPage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> or <paramref name="pageSize"/> is less than 1.
    /// </exception>
    public static AddressListResponse ToListResponse(this AddressPage addressPage, int page, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(addressPage);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, AddressListConstraints.MinPage);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, AddressListConstraints.MinPageSize);

        // Rounded up without adding to the count, which could overflow.
        int totalCount = addressPage.TotalCount;
        int totalPages = (totalCount / pageSize) + (totalCount % pageSize == 0 ? 0 : 1);

        return new AddressListResponse(
            [.. addressPage.Addresses.Select(address => address.ToResponse())],
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    /// <summary>
    /// Maps the <paramref name="response"/> to a request that replaces all values of the address with its current
    /// values, e.g. as the starting point of a partial change. The version is no value of a request and is not
    /// mapped.
    /// </summary>
    /// <param name="response">The address to map.</param>
    /// <returns>The request with the values of the address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is <see langword="null"/>.</exception>
    public static UpdateAddressRequest ToUpdateRequest(this AddressResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new UpdateAddressRequest(
            response.FirstName,
            response.LastName,
            response.Street,
            response.HouseNumber,
            response.PostalCode,
            response.City,
            response.Country,
            response.Email);
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
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(textNormalizer);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return Address.Create(
            request.FirstName,
            request.LastName,
            request.Street,
            request.HouseNumber,
            request.PostalCode,
            request.City,
            request.Country,
            request.Email,
            textNormalizer,
            timeProvider);
    }

    /// <summary>
    /// Replaces all values of the <paramref name="address"/> with the values of the <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The sanitized and validated request.</param>
    /// <param name="address">The address to change.</param>
    /// <param name="textNormalizer">The normalizer for the city and the postal code.</param>
    /// <param name="timeProvider">The provider of the current time.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/>, <paramref name="address"/>, <paramref name="textNormalizer"/> or
    /// <paramref name="timeProvider"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A value of the request is invalid; see <see cref="Address.Update"/>. The address stays unchanged.
    /// </exception>
    public static void ApplyTo(
        this UpdateAddressRequest request,
        Address address,
        ITextNormalizer textNormalizer,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(textNormalizer);
        ArgumentNullException.ThrowIfNull(timeProvider);

        address.Update(
            request.FirstName,
            request.LastName,
            request.Street,
            request.HouseNumber,
            request.PostalCode,
            request.City,
            request.Country,
            request.Email,
            textNormalizer,
            timeProvider);
    }
}
