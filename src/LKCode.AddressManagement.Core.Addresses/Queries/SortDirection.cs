namespace LKCode.AddressManagement.Core.Addresses.Queries;

/// <summary>
/// The direction of a sort order.
/// </summary>
/// <remarks>The names used in the API are defined in <see cref="SortDirections"/>.</remarks>
public enum SortDirection
{
    /// <summary>
    /// From the smallest to the largest value.
    /// </summary>
    Ascending,

    /// <summary>
    /// From the largest to the smallest value.
    /// </summary>
    Descending,
}
