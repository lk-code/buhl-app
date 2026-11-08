using System.Net.Mime;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Sanitizers;
using LKCode.AddressManagement.Service.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LKCode.AddressManagement.Service.Controllers;

/// <summary>
/// The endpoints to create, read and list addresses.
/// </summary>
/// <param name="addressService">The service that reads and stores the addresses.</param>
/// <param name="createAddressValidator">The validator of the requests to create an address.</param>
/// <param name="jsonOptions">
/// The JSON options of the controllers; their naming policy converts the property names of validation errors.
/// </param>
/// <remarks>
/// The XML documentation of the actions is the description of the endpoints in the OpenAPI document: the summary,
/// the remarks, the parameters and a <c>response</c> element for each status code.
/// </remarks>
/// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
[ApiController]
[Route("api/addresses")]
[ProducesResponseType<ProblemDetails>(
    StatusCodes.Status500InternalServerError,
    MediaTypeNames.Application.ProblemJson)]
public sealed class AddressesController(
    IAddressService addressService,
    IValidator<CreateAddressRequest> createAddressValidator,
    IOptions<JsonOptions> jsonOptions) : ControllerBase
{
    private const string GetByIdRouteName = "GetAddressById";
    private const string CreateRouteName = "CreateAddress";

    private readonly IAddressService _addressService =
        addressService ?? throw new ArgumentNullException(nameof(addressService));

    private readonly IValidator<CreateAddressRequest> _createAddressValidator =
        createAddressValidator ?? throw new ArgumentNullException(nameof(createAddressValidator));

    private readonly JsonNamingPolicy? _jsonNamingPolicy =
        (jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions)))
        .Value.JsonSerializerOptions.PropertyNamingPolicy;

    /// <summary>
    /// Gets the address with the given id.
    /// </summary>
    /// <param name="id">The id of the address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>The address, or <c>404 Not Found</c> if there is no address with the id.</returns>
    /// <remarks>
    /// An unknown id and an id that is no GUID both result in `404 Not Found`, like an unknown route. It is not
    /// possible to tell whether an address with the id has ever existed.
    /// </remarks>
    /// <response code="200">The address.</response>
    /// <response code="404">There is no address with the id.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{id:guid}", Name = GetByIdRouteName)]
    [ProducesResponseType<AddressResponse>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<AddressResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        AddressResponse? address = await _addressService.GetByIdAsync(id, cancellationToken);

        return address is null ? NotFound() : Ok(address);
    }

    /// <summary>
    /// Gets all addresses, optionally filtered by city and postal code.
    /// </summary>
    /// <param name="city">A part of the city to search for. Missing or blank to ignore the city.</param>
    /// <param name="postalCode">
    /// A part of the postal code to search for. Missing or blank to ignore the postal code.
    /// </param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>The matching addresses; an empty list if none matches.</returns>
    /// <remarks>
    /// Both filters are optional and search for a part of the value, e.g. `sol` finds `Solingen`. They are compared
    /// with normalized values: the case, surrounding whitespace and accents do not matter, umlauts and their
    /// spellings with `e` are the same (`Koeln` finds `Köln`), and spaces in postal codes are ignored. If both filters
    /// are given, an address must match both. The order of the addresses is not specified.
    /// </remarks>
    /// <response code="200">The matching addresses; an empty array if none matches.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AddressResponse>>> GetListAsync(
        [FromQuery] string? city,
        [FromQuery] string? postalCode,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AddressResponse> addresses =
            await _addressService.GetListAsync(new AddressListQuery(city, postalCode), cancellationToken);

        return Ok(addresses);
    }

    // The parameter "request" is documented last: the OpenAPI XML comment generator of .NET 10 writes the
    // description of every parameter that is no operation parameter, including the cancellation token, to the
    // request body, so the last one wins.

    /// <summary>
    /// Creates a new address.
    /// </summary>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <param name="request">The values of the new address.</param>
    /// <returns>
    /// <c>201 Created</c> with the new address and its URL in the <c>Location</c> header, or
    /// <c>400 Bad Request</c> with all validation errors.
    /// </returns>
    /// <remarks>
    /// Before the validation, all values are trimmed, the country is converted to uppercase and a blank email
    /// address is treated as missing. All validation errors are returned together, keyed by the name of the field,
    /// e.g. `firstName`. Errors in the JSON itself are keyed by their JSON path, e.g. `$.firstName`.
    /// </remarks>
    /// <response code="201">The address was created; the `Location` header contains its URL.</response>
    /// <response code="400">The body is no valid JSON object of an address or contains invalid values.</response>
    /// <response code="415">The body is not JSON.</response>
    /// <response code="500">An unexpected error occurred.</response>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    [HttpPost(Name = CreateRouteName)]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<AddressResponse>(StatusCodes.Status201Created, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest,
        MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status415UnsupportedMediaType,
        MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<AddressResponse>> CreateAsync(
        CreateAddressRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        CreateAddressRequest sanitizedRequest = request.Sanitize();
        ValidationResult validationResult =
            await _createAddressValidator.ValidateAsync(sanitizedRequest, cancellationToken);
        if (!validationResult.IsValid)
        {
            ModelState.AddValidationErrors(validationResult, _jsonNamingPolicy);

            return ValidationProblem(ModelState);
        }

        AddressResponse address = await _addressService.CreateAsync(sanitizedRequest, cancellationToken);

        return CreatedAtRoute(GetByIdRouteName, new { id = address.Id }, address);
    }
}
