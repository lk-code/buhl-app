namespace LKCode.AddressManagement.Core.Addresses.Queries;

/// <summary>
/// The names of the <see cref="AddressSortField"/> values in the API, e.g. <c>lastName</c>.
/// </summary>
/// <remarks>The names are the names of the fields in JSON.</remarks>
public static class AddressSortFields
{
    /// <summary>
    /// Gets the names of all fields in the order of <see cref="AddressSortField"/>.
    /// </summary>
    public static IReadOnlyList<string> Names => throw new NotImplementedException();

    /// <summary>
    /// Gets the name of the <paramref name="field"/> in the API.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The name, e.g. <c>lastName</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="field"/> is not defined.</exception>
    public static string GetName(AddressSortField field) => throw new NotImplementedException();

    /// <summary>
    /// Finds the field with the <paramref name="name"/>, ignoring the case.
    /// </summary>
    /// <param name="name">The name of the field; it must be trimmed.</param>
    /// <param name="field">The field if it was found; otherwise the default value.</param>
    /// <returns>
    /// <see langword="true"/> if there is a field with the name; <see langword="false"/> if the name is unknown or
    /// <see langword="null"/>.
    /// </returns>
    public static bool TryParse(string? name, out AddressSortField field) => throw new NotImplementedException();
}
