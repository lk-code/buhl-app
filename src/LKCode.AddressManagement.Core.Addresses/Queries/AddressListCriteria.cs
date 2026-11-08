using LKCode.AddressManagement.Domain.Addresses;

namespace LKCode.AddressManagement.Core.Addresses.Queries;

/// <summary>
/// The checked filters, sort order and page of an address list, as the repository expects them.
/// </summary>
/// <param name="NormalizedCity">
/// The normalized part of the city to search for, or <see langword="null"/>, empty or whitespace to ignore the city.
/// It is compared with <see cref="Address.NormalizedCity"/>.
/// </param>
/// <param name="NormalizedPostalCode">
/// The normalized part of the postal code to search for, or <see langword="null"/>, empty or whitespace to ignore the
/// postal code. It is compared with <see cref="Address.NormalizedPostalCode"/>.
/// </param>
/// <param name="SortBy">The field to sort by; addresses with the same value are sorted by their id.</param>
/// <param name="SortDirection">The direction of the sort order, also for the id.</param>
/// <param name="Page">The number of the page, starting at <see cref="AddressListConstraints.MinPage"/>.</param>
/// <param name="PageSize">The maximum number of addresses on a page, at least 1.</param>
public sealed record AddressListCriteria(
    string? NormalizedCity,
    string? NormalizedPostalCode,
    AddressSortField SortBy,
    SortDirection SortDirection,
    int Page,
    int PageSize);
