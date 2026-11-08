using FluentValidation;
using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.Core.Addresses.Validators;

/// <summary>
/// The validation rules of the page and the sort order of an address list.
/// </summary>
/// <remarks>
/// The query is expected to be sanitized before. Missing values are valid, they stand for the defaults. The filters
/// are not validated: a search term is no stored value, and a filter without match just yields an empty list.
/// </remarks>
public sealed class AddressListQueryValidator : AbstractValidator<AddressListQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddressListQueryValidator"/> class and defines the rules.
    /// </summary>
    public AddressListQueryValidator()
    {
    }
}
