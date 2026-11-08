using FluentValidation;
using LKCode.AddressManagement.Core.Addresses.Contracts;

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
    }
}
