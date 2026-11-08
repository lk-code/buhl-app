namespace LKCode.AddressManagement.UnitTests.Domain.Addresses;

/// <summary>
/// Describes one input value of an address with its parameter name and its values at and above the maximum length.
/// </summary>
public sealed record AddressField(
    string ParameterName,
    bool IsRequired,
    string ValueAtMaxLength,
    string ValueExceedingMaxLength,
    Func<AddressInput, string?, AddressInput> With)
{
    private const string EmailDomain = "@example.com";

    public static IReadOnlyList<AddressField> All { get; } =
    [
        new("firstName", true, Text(100), Text(101), (input, value) => input with { FirstName = value! }),
        new("lastName", true, Text(100), Text(101), (input, value) => input with { LastName = value! }),
        new("street", true, Text(200), Text(201), (input, value) => input with { Street = value! }),
        new("houseNumber", true, Text(20), Text(21), (input, value) => input with { HouseNumber = value! }),
        new("postalCode", true, Text(20), Text(21), (input, value) => input with { PostalCode = value! }),
        new("city", true, Text(100), Text(101), (input, value) => input with { City = value! }),
        new("country", true, "DE", "GER", (input, value) => input with { Country = value! }),
        new("email", false, Email(254), Email(255), (input, value) => input with { Email = value }),
    ];

    public static IReadOnlyList<AddressField> Required { get; } = [.. All.Where(field => field.IsRequired)];

    public override string ToString() => ParameterName;

    private static string Text(int length) => new('a', length);

    private static string Email(int length) => new string('a', length - EmailDomain.Length) + EmailDomain;
}
