namespace LKCode.AddressManagement.Core.Addresses.Dtos;

/// <summary>
/// The optional filters for listing addresses. Both filters must match.
/// </summary>
/// <param name="City">
/// A part of the city to search for, or <see langword="null"/> or blank to ignore the city.
/// </param>
/// <param name="PostalCode">
/// A part of the postal code to search for, or <see langword="null"/> or blank to ignore the postal code.
/// </param>
public sealed record AddressListQuery(string? City, string? PostalCode);
