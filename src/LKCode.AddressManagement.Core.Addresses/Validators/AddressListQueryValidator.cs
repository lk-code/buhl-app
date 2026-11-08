using FluentValidation;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Queries;

namespace LKCode.AddressManagement.Core.Addresses.Validators;

/// <summary>
/// The validation rules of the page and the sort order of an address list.
/// </summary>
/// <remarks>
/// The query is expected to be sanitized before. Missing values are valid, they stand for the defaults. The names of
/// the sort field and the sort direction are compared ignoring the case. The filters are not validated: a search term
/// is no stored value, and a filter without match just yields an empty list.
/// </remarks>
public sealed class AddressListQueryValidator : AbstractValidator<AddressListQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddressListQueryValidator"/> class and defines the rules.
    /// </summary>
    public AddressListQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(AddressListConstraints.MinPage);
        RuleFor(query => query.PageSize)
            .InclusiveBetween(AddressListConstraints.MinPageSize, AddressListConstraints.MaxPageSize);
        RuleFor(query => query.SortBy)
            .Must(sortBy => AddressSortFields.TryParse(sortBy, out _))
            .When(query => query.SortBy is not null)
            .WithMessage(MustBeOneOf(AddressSortFields.Names));
        RuleFor(query => query.SortDirection)
            .Must(sortDirection => SortDirections.TryParse(sortDirection, out _))
            .When(query => query.SortDirection is not null)
            .WithMessage(MustBeOneOf(SortDirections.Names));
    }

    private static string MustBeOneOf(IEnumerable<string> names) =>
        $"'{{PropertyName}}' must be one of: {string.Join(", ", names)}.";
}
