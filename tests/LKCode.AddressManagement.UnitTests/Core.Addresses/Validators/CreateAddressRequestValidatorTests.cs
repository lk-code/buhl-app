using FluentValidation;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Validators;
using LKCode.AddressManagement.UnitTests.Domain.Addresses;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses.Validators;

public class CreateAddressRequestValidatorTests : AddressRequestValidatorTests<CreateAddressRequest>
{
    protected override IValidator<CreateAddressRequest> CreateValidator() => new CreateAddressRequestValidator();

    protected override CreateAddressRequest CreateRequest(AddressInput input) => input.ToCreateRequest();
}
