using System.Text.Json.Nodes;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Domain.Addresses;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// The values of addresses used by the integration tests, as request and as JSON body.
/// </summary>
public static class AddressPayloads
{
    private const string EmailDomain = "@example.com";

    public static CreateAddressRequest ValidRequest { get; } = new(
        "Erika",
        "Mustermann",
        "Heidestraße",
        "17",
        "51147",
        "Köln",
        "DE",
        "erika.mustermann@example.com");

    /// <summary>
    /// Gets all fields of the JSON body with their values at and above the maximum length.
    /// </summary>
    public static IReadOnlyList<AddressPayloadField> Fields { get; } =
    [
        Text("firstName", AddressConstraints.FirstNameMaxLength),
        Text("lastName", AddressConstraints.LastNameMaxLength),
        Text("street", AddressConstraints.StreetMaxLength),
        Text("houseNumber", AddressConstraints.HouseNumberMaxLength),
        Text("postalCode", AddressConstraints.PostalCodeMaxLength),
        Text("city", AddressConstraints.CityMaxLength),
        new("country", true, "DE", "DEU"),
        new("email", false, Email(AddressConstraints.EmailMaxLength), Email(AddressConstraints.EmailMaxLength + 1)),
    ];

    /// <summary>
    /// Gets the required fields of the JSON body.
    /// </summary>
    public static IReadOnlyList<AddressPayloadField> RequiredFields { get; } =
        [.. Fields.Where(field => field.IsRequired)];

    /// <summary>
    /// Creates a new JSON body of <see cref="ValidRequest"/>, which a test may change.
    /// </summary>
    public static JsonObject Valid() => new()
    {
        ["firstName"] = ValidRequest.FirstName,
        ["lastName"] = ValidRequest.LastName,
        ["street"] = ValidRequest.Street,
        ["houseNumber"] = ValidRequest.HouseNumber,
        ["postalCode"] = ValidRequest.PostalCode,
        ["city"] = ValidRequest.City,
        ["country"] = ValidRequest.Country,
        ["email"] = ValidRequest.Email,
    };

    /// <summary>
    /// Creates a new JSON body of <see cref="ValidRequest"/> with one value replaced.
    /// </summary>
    public static JsonObject ValidWith(string name, string? value)
    {
        JsonObject payload = Valid();
        payload[name] = value;

        return payload;
    }

    /// <summary>
    /// Creates a new JSON body of <see cref="ValidRequest"/> without the given values.
    /// </summary>
    public static JsonObject ValidWithout(params IEnumerable<string> names)
    {
        JsonObject payload = Valid();
        foreach (string name in names)
        {
            payload.Remove(name);
        }

        return payload;
    }

    private static AddressPayloadField Text(string name, int maxLength) =>
        new(name, true, new string('a', maxLength), new string('a', maxLength + 1));

    private static string Email(int length) => new string('a', length - EmailDomain.Length) + EmailDomain;
}
