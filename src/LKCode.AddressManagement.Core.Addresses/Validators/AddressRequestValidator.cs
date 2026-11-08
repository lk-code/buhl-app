using System.Linq.Expressions;
using System.Net.Mail;
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
        RuleForRequiredText(request => request.Country, AddressConstraints.CountryMaxLength)
            .Matches(CountryCodes.Pattern)
            .WithMessage("'{PropertyName}' must consist of exactly two uppercase letters.")
            .Must(CountryCodes.IsValid)
            .WithMessage("'{PropertyName}' must be an assigned ISO 3166-1 alpha-2 country code.");

        RuleFor(request => request.Email)
            .MaximumLength(AddressConstraints.EmailMaxLength)
            .Must(IsValidEmail)
            .WithMessage("'{PropertyName}' is not a valid email address.")
            .When(request => !string.IsNullOrWhiteSpace(request.Email));
    }

    /// <summary>
    /// Checks the format with the parser of <see cref="MailAddress"/>, which does not throw. The parsed address must
    /// equal the whole value, which rejects display names, comments and lists of addresses.
    /// </summary>
    private static bool IsValidEmail(string? email) =>
        MailAddress.TryCreate(email, out MailAddress? address) && address.Address == email;

    private IRuleBuilderOptions<TRequest, string> RuleForRequiredText(
        Expression<Func<TRequest, string>> value,
        int maxLength) =>
        RuleFor(value)
            .NotEmpty()
            .MaximumLength(maxLength);
}
