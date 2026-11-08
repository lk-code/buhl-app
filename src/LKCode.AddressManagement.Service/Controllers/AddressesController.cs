using FluentValidation;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LKCode.AddressManagement.Service.Controllers;

/// <summary>
/// The endpoints to create and read addresses.
/// </summary>
/// <param name="addressService">The service that reads and stores the addresses.</param>
/// <param name="createAddressValidator">The validator of the requests to create an address.</param>
/// <param name="jsonOptions">
/// The JSON options of the controllers; their naming policy converts the property names of validation errors.
/// </param>
/// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
[ApiController]
[Route("api/addresses")]
public sealed class AddressesController(
    IAddressService addressService,
    IValidator<CreateAddressRequest> createAddressValidator,
    IOptions<JsonOptions> jsonOptions) : ControllerBase
{
    private readonly IAddressService _addressService =
        addressService ?? throw new ArgumentNullException(nameof(addressService));

    private readonly IValidator<CreateAddressRequest> _createAddressValidator = createAddressValidator;

    private readonly IOptions<JsonOptions> _jsonOptions = jsonOptions;

    /// <summary>
    /// Gets the address with the given id.
    /// </summary>
    /// <param name="id">The id of the address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>The address, or <c>404 Not Found</c> if there is no address with the id.</returns>
    /// <remarks>
    /// An unknown id and an id that is no GUID both result in <c>404 Not Found</c> with Problem Details, like an
    /// unknown route. A client cannot tell whether an address with the id has ever existed.
    /// </remarks>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AddressResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        AddressResponse? address = await _addressService.GetByIdAsync(id, cancellationToken);

        return address is null ? NotFound() : Ok(address);
    }

    /// <summary>
    /// Creates a new address.
    /// </summary>
    /// <param name="request">The values of the new address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>
    /// <c>201 Created</c> with the new address and its URL in the <c>Location</c> header, or
    /// <c>400 Bad Request</c> with all validation errors.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    [HttpPost]
    public Task<ActionResult<AddressResponse>> CreateAsync(
        CreateAddressRequest request,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
