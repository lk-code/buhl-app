using System.Globalization;
using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.IntegrationTests.Addresses;

public static class AddressResponseExtensions
{
    /// <summary>
    /// Gets the strong entity tag of the version of the <paramref name="address"/>, e.g. <c>"3"</c>.
    /// </summary>
    public static string ToETag(this AddressResponse address)
    {
        ArgumentNullException.ThrowIfNull(address);

        return ToETag(address.Version);
    }

    /// <summary>
    /// Gets the strong entity tag of the <paramref name="version"/>, e.g. <c>"3"</c>.
    /// </summary>
    public static string ToETag(long version) => string.Create(CultureInfo.InvariantCulture, $"\"{version}\"");
}
