using System.Text.Json;

namespace LKCode.AddressManagement.Service.Extensions;

/// <summary>
/// Reads the JSON contract of types from <see cref="JsonSerializerOptions"/>.
/// </summary>
public static class JsonSerializerOptionsExtensions
{
    /// <summary>
    /// Gets the JSON Pointers (RFC 6901) of all properties that the <paramref name="options"/> serialize for the
    /// <paramref name="type"/>, e.g. <c>/firstName</c>.
    /// </summary>
    /// <param name="options">The options whose contract, e.g. the naming policy, defines the names.</param>
    /// <param name="type">The type whose properties are pointed to.</param>
    /// <returns>The pointers in the order of the properties in JSON.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> or <paramref name="type"/> is <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<string> GetPropertyJsonPointers(this JsonSerializerOptions options, Type type) =>
        throw new NotImplementedException();
}
