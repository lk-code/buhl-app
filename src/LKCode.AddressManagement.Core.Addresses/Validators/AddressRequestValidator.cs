using System.Linq.Expressions;
using FluentValidation;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Domain.Addresses;

namespace LKCode.AddressManagement.Core.Addresses.Validators;

/// <summary>
/// The validation rules shared by all address requests.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <remarks>
/// The request is expected to be sanitized before, e.g. the country must already be uppercase. Each value gets at
/// most one error, but all values are validated.
/// </remarks>
public abstract class AddressRequestValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : IAddressRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddressRequestValidator{TRequest}"/> class and defines the rules.
    /// </summary>
    protected AddressRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleForRequiredText(request => request.FirstName, AddressConstraints.FirstNameMaxLength);
        RuleForRequiredText(request => request.LastName, AddressConstraints.LastNameMaxLength);
        RuleForRequiredText(request => request.Street, AddressConstraints.StreetMaxLength);
        RuleForRequiredText(request => request.HouseNumber, AddressConstraints.HouseNumberMaxLength);
        RuleForRequiredText(request => request.PostalCode, AddressConstraints.PostalCodeMaxLength);
        RuleForRequiredText(request => request.City, AddressConstraints.CityMaxLength);
        RuleForRequiredText(request => request.Country, AddressConstraints.CountryMaxLength);

        RuleFor(request => request.Email)
            .MaximumLength(AddressConstraints.EmailMaxLength);
    }

    private IRuleBuilderOptions<TRequest, string> RuleForRequiredText(
        Expression<Func<TRequest, string>> value,
        int maxLength) =>
        RuleFor(value)
            .NotEmpty()
            .MaximumLength(maxLength);
}
