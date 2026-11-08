namespace LKCode.AddressManagement.Core.Addresses.Queries;

/// <summary>
/// The names of the <see cref="SortDirection"/> values in the API: <c>asc</c> and <c>desc</c>.
/// </summary>
public static class SortDirections
{
    private static readonly EnumNames<SortDirection> _names = new(
        (SortDirection.Ascending, "asc"),
        (SortDirection.Descending, "desc"));

    /// <summary>
    /// Gets the names of all directions in the order of <see cref="SortDirection"/>.
    /// </summary>
    public static IReadOnlyList<string> Names => _names.Names;

    /// <summary>
    /// Gets the name of the <paramref name="direction"/> in the API.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <returns>The name, <c>asc</c> or <c>desc</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is not defined.</exception>
    public static string GetName(SortDirection direction) => _names.GetName(direction, nameof(direction));

    /// <summary>
    /// Finds the direction with the <paramref name="name"/>, ignoring the case.
    /// </summary>
    /// <param name="name">The name of the direction; it must be trimmed.</param>
    /// <param name="direction">The direction if it was found; otherwise the default value.</param>
    /// <returns>
    /// <see langword="true"/> if there is a direction with the name; <see langword="false"/> if the name is unknown
    /// or <see langword="null"/>.
    /// </returns>
    public static bool TryParse(string? name, out SortDirection direction) => _names.TryParse(name, out direction);
}
