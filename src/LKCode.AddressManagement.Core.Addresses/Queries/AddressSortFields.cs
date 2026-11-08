namespace LKCode.AddressManagement.Core.Addresses.Queries;

/// <summary>
/// The names of the <see cref="AddressSortField"/> values in the API, e.g. <c>lastName</c>.
/// </summary>
/// <remarks>The names are the names of the fields in JSON.</remarks>
public static class AddressSortFields
{
    private static readonly EnumNames<AddressSortField> _names = new(
        (AddressSortField.LastName, "lastName"),
        (AddressSortField.FirstName, "firstName"),
        (AddressSortField.City, "city"),
        (AddressSortField.PostalCode, "postalCode"),
        (AddressSortField.Country, "country"),
        (AddressSortField.CreatedAt, "createdAt"),
        (AddressSortField.UpdatedAt, "updatedAt"));

    /// <summary>
    /// Gets the names of all fields in the order of <see cref="AddressSortField"/>.
    /// </summary>
    public static IReadOnlyList<string> Names => _names.Names;

    /// <summary>
    /// Gets the name of the <paramref name="field"/> in the API.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The name, e.g. <c>lastName</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="field"/> is not defined.</exception>
    public static string GetName(AddressSortField field) => _names.GetName(field, nameof(field));

    /// <summary>
    /// Finds the field with the <paramref name="name"/>, ignoring the case.
    /// </summary>
    /// <param name="name">The name of the field; it must be trimmed.</param>
    /// <param name="field">The field if it was found; otherwise the default value.</param>
    /// <returns>
    /// <see langword="true"/> if there is a field with the name; <see langword="false"/> if the name is unknown or
    /// <see langword="null"/>.
    /// </returns>
    public static bool TryParse(string? name, out AddressSortField field) => _names.TryParse(name, out field);
}
