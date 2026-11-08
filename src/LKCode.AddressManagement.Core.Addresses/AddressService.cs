using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Mappings;
using LKCode.AddressManagement.Core.Addresses.Results;
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
/// Only ids, versions, counts and normalized filters are logged, never personal data such as names or email
/// addresses.
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
    public Task<AddressListResponse> GetListAsync(AddressListQuery query, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

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
    public async Task<AddressChangeResult> UpdateAsync(
        Guid id,
        long? expectedVersion,
        UpdateAddressRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Address? address = await _addressRepository.GetByIdAsync(id, cancellationToken);
        if (address is null)
        {
            LogAddressNotFound(_logger, id);
            return AddressChangeResult.NotFound;
        }

        request.ApplyTo(address, _textNormalizer, _timeProvider);
        ChangeStatus status = await _addressRepository.UpdateAsync(address, expectedVersion, cancellationToken);
        switch (status)
        {
            case ChangeStatus.Success:
                LogAddressUpdated(_logger, id);
                return AddressChangeResult.Success(address.ToResponse());
            case ChangeStatus.NotFound:
                LogAddressNotFound(_logger, id);
                return AddressChangeResult.NotFound;
            default:
                LogVersionMismatch(_logger, id, expectedVersion);
                return AddressChangeResult.VersionMismatch;
        }
    }

    /// <inheritdoc />
    public async Task<ChangeStatus> DeleteAsync(Guid id, long? expectedVersion, CancellationToken cancellationToken)
    {
        Address? address = await _addressRepository.GetByIdAsync(id, cancellationToken);
        if (address is null)
        {
            LogAddressNotFound(_logger, id);
            return ChangeStatus.NotFound;
        }

        address.Delete(_timeProvider);
        ChangeStatus status = await _addressRepository.UpdateAsync(address, expectedVersion, cancellationToken);
        switch (status)
        {
            case ChangeStatus.Success:
                LogAddressDeleted(_logger, id);
                break;
            case ChangeStatus.NotFound:
                LogAddressNotFound(_logger, id);
                break;
            default:
                LogVersionMismatch(_logger, id, expectedVersion);
                break;
        }

        return status;
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

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Updated address {AddressId}")]
    private static partial void LogAddressUpdated(ILogger logger, Guid addressId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Deleted address {AddressId}")]
    private static partial void LogAddressDeleted(ILogger logger, Guid addressId);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Address {AddressId} was not saved: its version differs from the expected version {ExpectedVersion}")]
    private static partial void LogVersionMismatch(ILogger logger, Guid addressId, long? expectedVersion);
}
