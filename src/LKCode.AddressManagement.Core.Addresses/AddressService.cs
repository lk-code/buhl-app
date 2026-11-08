using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Mappings;
using LKCode.AddressManagement.Domain.Addresses;
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
/// <remarks>
/// Only ids, counts and normalized filters are logged, never personal data such as names or email addresses.
/// </remarks>
/// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
public sealed partial class AddressService(
    IAddressRepository addressRepository,
    ITextNormalizer textNormalizer,
    TimeProvider timeProvider,
    ILogger<AddressService> logger) : IAddressService
{
    private readonly IAddressRepository _addressRepository =
        addressRepository ?? throw new ArgumentNullException(nameof(addressRepository));

    private readonly ITextNormalizer _textNormalizer =
        textNormalizer ?? throw new ArgumentNullException(nameof(textNormalizer));

    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    private readonly ILogger<AddressService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<AddressResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Address? address = await _addressRepository.GetByIdAsync(id, cancellationToken);
        if (address is null)
        {
            LogAddressNotFound(_logger, id);
            return null;
        }

        LogAddressLoaded(_logger, id);

        return address.ToResponse();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AddressResponse>> GetListAsync(
        AddressListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        string? normalizedCity = string.IsNullOrWhiteSpace(query.City)
            ? null
            : _textNormalizer.Normalize(query.City);
        string? normalizedPostalCode = string.IsNullOrWhiteSpace(query.PostalCode)
            ? null
            : _textNormalizer.NormalizePostalCode(query.PostalCode);

        IReadOnlyList<Address> addresses =
            await _addressRepository.ListAsync(normalizedCity, normalizedPostalCode, cancellationToken);
        LogAddressesListed(_logger, addresses.Count, normalizedCity, normalizedPostalCode);

        return [.. addresses.Select(address => address.ToResponse())];
    }

    /// <inheritdoc />
    public async Task<AddressResponse> CreateAsync(CreateAddressRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var address = request.ToAddress(_textNormalizer, _timeProvider);
        await _addressRepository.AddAsync(address, cancellationToken);
        LogAddressCreated(_logger, address.Id);

        return address.ToResponse();
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Created address {AddressId}")]
    private static partial void LogAddressCreated(ILogger logger, Guid addressId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Loaded address {AddressId}")]
    private static partial void LogAddressLoaded(ILogger logger, Guid addressId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Address {AddressId} was not found")]
    private static partial void LogAddressNotFound(ILogger logger, Guid addressId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "Listed {Count} addresses for city {NormalizedCity} and postal code {NormalizedPostalCode}")]
    private static partial void LogAddressesListed(
        ILogger logger,
        int count,
        string? normalizedCity,
        string? normalizedPostalCode);
}
