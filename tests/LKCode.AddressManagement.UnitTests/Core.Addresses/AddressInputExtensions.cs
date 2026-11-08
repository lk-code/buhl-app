using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.UnitTests.Domain.Addresses;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses;

/// <summary>
/// Converts the test values of <see cref="AddressInput"/> to address requests and back.
/// </summary>
public static class AddressInputExtensions
{
    public static CreateAddressRequest ToCreateRequest(this AddressInput input) => new(
        input.FirstName,
        input.LastName,
        input.Street,
        input.HouseNumber,
        input.PostalCode,
        input.City,
        input.Country,
        input.Email);

    public static UpdateAddressRequest ToUpdateRequest(this AddressInput input) => new(
        input.FirstName,
        input.LastName,
        input.Street,
        input.HouseNumber,
        input.PostalCode,
        input.City,
        input.Country,
        input.Email);

    public static AddressInput ToInput(this CreateAddressRequest request) => new(
        request.FirstName,
        request.LastName,
        request.Street,
        request.HouseNumber,
        request.PostalCode,
        request.City,
        request.Country,
        request.Email);

    public static AddressInput ToInput(this UpdateAddressRequest request) => new(
        request.FirstName,
        request.LastName,
        request.Street,
        request.HouseNumber,
        request.PostalCode,
        request.City,
        request.Country,
        request.Email);
}
