using LKCode.AddressManagement.Core.Addresses.Dtos;

namespace LKCode.AddressManagement.Core.Addresses.Validators;

/// <summary>
/// Validates a sanitized <see cref="UpdateAddressRequest"/>.
/// </summary>
public sealed class UpdateAddressRequestValidator : AddressRequestValidator<UpdateAddressRequest>;
