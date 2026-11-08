using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace LKCode.AddressManagement.Service.Controllers;

/// <summary>
/// The endpoints to create and read addresses.
/// </summary>
/// <param name="addressService">The service that reads and stores the addresses.</param>
/// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
[ApiController]
[Route("api/addresses")]
public sealed class AddressesController(IAddressService addressService) : ControllerBase
{
    private readonly IAddressService _addressService =
        addressService ?? throw new ArgumentNullException(nameof(addressService));

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
}
