namespace LKCode.AddressManagement.IntegrationTests.Addresses;

/// <summary>
/// A field of the JSON body of an address with its values at and above the maximum length.
/// </summary>
public sealed record AddressPayloadField(
    string Name,
    bool IsRequired,
    string ValueAtMaxLength,
    string ValueExceedingMaxLength)
{
    public override string ToString() => Name;
}
