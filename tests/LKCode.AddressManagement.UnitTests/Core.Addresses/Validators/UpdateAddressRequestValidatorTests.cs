using FluentValidation;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Validators;
using LKCode.AddressManagement.UnitTests.Domain.Addresses;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses.Validators;

public class UpdateAddressRequestValidatorTests : AddressRequestValidatorTests<UpdateAddressRequest>
{
    protected override IValidator<UpdateAddressRequest> CreateValidator() => new UpdateAddressRequestValidator();

    protected override UpdateAddressRequest CreateRequest(AddressInput input) => input.ToUpdateRequest();
}
