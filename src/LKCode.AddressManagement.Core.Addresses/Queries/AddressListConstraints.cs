namespace LKCode.AddressManagement.Core.Addresses.Queries;

/// <summary>
/// The defaults and limits of the page and the sort order of an address list.
/// </summary>
/// <remarks>
/// The single source of the validation, the defaults of the service and the OpenAPI document.
/// </remarks>
public static class AddressListConstraints
{
    /// <summary>
    /// The number of the first page.
    /// </summary>
    public const int MinPage = 1;

    /// <summary>
    /// The page if the client requests none.
    /// </summary>
    public const int DefaultPage = MinPage;

    /// <summary>
    /// The smallest allowed page size.
    /// </summary>
    public const int MinPageSize = 1;

    /// <summary>
    /// The largest allowed page size.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// The page size if the client requests none.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// The field to sort by if the client requests none.
    /// </summary>
    public const AddressSortField DefaultSortBy = AddressSortField.LastName;

    /// <summary>
    /// The sort direction if the client requests none.
    /// </summary>
    public const SortDirection DefaultSortDirection = SortDirection.Ascending;
}
