using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// The values of addresses used by the integration tests.
/// </summary>
public static class AddressPayloads
{
    public static CreateAddressRequest ValidRequest { get; } = new(
        "Erika",
        "Mustermann",
        "Heidestraße",
        "17",
        "51147",
        "Köln",
        "DE",
        "erika.mustermann@example.com");
}
