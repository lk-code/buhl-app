using LKCode.AddressManagement.Domain.Addresses;

namespace LKCode.AddressManagement.Core.Addresses.Results;

/// <summary>
/// A page of a sorted address list and the number of all addresses of the list.
/// </summary>
/// <param name="Addresses">The addresses on the page in their order; empty if the page is behind the end.</param>
/// <param name="TotalCount">The number of all addresses that match the filters, on all pages.</param>
public sealed record AddressPage(IReadOnlyList<Address> Addresses, int TotalCount);
