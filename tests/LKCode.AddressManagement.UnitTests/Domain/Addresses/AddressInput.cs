using LKCode.AddressManagement.Domain.Addresses;

namespace LKCode.AddressManagement.UnitTests.Domain.Addresses;

/// <summary>
/// The input values of <see cref="Address.Create"/>, used to vary single values in tests.
/// </summary>
public sealed record AddressInput(
    string FirstName,
    string LastName,
    string Street,
    string HouseNumber,
    string PostalCode,
    string City,
    string Country,
    string? Email)
{
    public static AddressInput Valid { get; } = new(
        "Erika",
        "Mustermann",
        "Heidestraße",
        "17",
        "51147",
        "Köln",
        "DE",
        "erika.mustermann@example.com");

    public static AddressInput From(Address address) => new(
        address.FirstName,
        address.LastName,
        address.Street,
        address.HouseNumber,
        address.PostalCode,
        address.City,
        address.Country,
        address.Email);
}
