using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.Core.Addresses.Validators;

/// <summary>
/// Validates a sanitized <see cref="CreateAddressRequest"/>.
/// </summary>
public sealed class CreateAddressRequestValidator : AddressRequestValidator<CreateAddressRequest>;
