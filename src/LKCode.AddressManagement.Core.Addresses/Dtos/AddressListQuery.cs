namespace LKCode.AddressManagement.Core.Addresses.Dtos;

/// <summary>
/// The optional filters, the page and the sort order for listing addresses, as the client sent them. Both filters
/// must match.
/// </summary>
/// <param name="City">
/// A part of the city to search for, or <see langword="null"/> or blank to ignore the city.
/// </param>
/// <param name="PostalCode">
/// A part of the postal code to search for, or <see langword="null"/> or blank to ignore the postal code.
/// </param>
/// <param name="Page">
/// The number of the page, starting at 1, or <see langword="null"/> for the first page.
/// </param>
/// <param name="PageSize">
/// The maximum number of addresses on a page, or <see langword="null"/> for the default size.
/// </param>
/// <param name="SortBy">
/// The name of the field to sort by, e.g. <c>lastName</c>, or <see langword="null"/> for the default field.
/// </param>
/// <param name="SortDirection">
/// The sort direction, <c>asc</c> or <c>desc</c>, or <see langword="null"/> for ascending.
/// </param>
/// <remarks>
/// The values are not yet checked: they are sanitized with <c>Sanitize()</c> and validated before listing. The
/// defaults and limits are defined in <see cref="Queries.AddressListConstraints"/>.
/// </remarks>
public sealed record AddressListQuery(
    string? City,
    string? PostalCode,
    int? Page = null,
    int? PageSize = null,
    string? SortBy = null,
    string? SortDirection = null);
