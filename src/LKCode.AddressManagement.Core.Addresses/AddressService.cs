using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Domain.Contracts;
using Microsoft.Extensions.Logging;

namespace LKCode.AddressManagement.Core.Addresses;

/// <summary>
/// Default implementation of <see cref="IAddressService"/>.
/// </summary>
/// <param name="addressRepository">The repository that loads and stores the addresses.</param>
/// <param name="textNormalizer">The normalizer for the city and the postal code.</param>
/// <param name="timeProvider">The provider of the current time.</param>
/// <param name="logger">The logger.</param>
/// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
public sealed class AddressService(
    IAddressRepository addressRepository,
    ITextNormalizer textNormalizer,
    TimeProvider timeProvider,
    ILogger<AddressService> logger) : IAddressService
{
    private readonly IAddressRepository _addressRepository = addressRepository;
    private readonly ITextNormalizer _textNormalizer = textNormalizer;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<AddressService> _logger = logger;

    /// <inheritdoc />
    public Task<AddressResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AddressResponse>> GetListAsync(
        AddressListQuery query,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<AddressResponse> CreateAsync(CreateAddressRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<AddressResponse?> UpdateAsync(
        Guid id,
        UpdateAddressRequest request,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
