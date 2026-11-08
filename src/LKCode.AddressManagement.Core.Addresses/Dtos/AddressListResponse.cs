namespace LKCode.AddressManagement.Core.Addresses.Dtos;

/// <summary>
/// One page of a sorted address list as returned to clients.
/// </summary>
/// <param name="Items">
/// The addresses on the page in their order; empty if none matches or the page is behind the end.
/// </param>
/// <param name="Page">The number of the page, starting at 1, as requested.</param>
/// <param name="PageSize">The maximum number of addresses on a page, as requested.</param>
/// <param name="TotalCount">The number of all addresses that match the filters, on all pages.</param>
/// <param name="TotalPages">The number of pages with addresses; 0 if none matches.</param>
public sealed record AddressListResponse(
    IReadOnlyList<AddressResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
