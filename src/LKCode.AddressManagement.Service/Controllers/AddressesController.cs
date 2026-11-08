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
    private readonly IAddressService _addressService = addressService;

    /// <summary>
    /// Gets the address with the given id.
    /// </summary>
    /// <param name="id">The id of the address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>The address, or <c>404 Not Found</c> if there is no address with the id.</returns>
    [HttpGet("{id:guid}")]
    public Task<ActionResult<AddressResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
