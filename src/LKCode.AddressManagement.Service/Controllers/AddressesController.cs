using System.Globalization;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Mappings;
using LKCode.AddressManagement.Core.Addresses.Results;
using LKCode.AddressManagement.Core.Addresses.Sanitizers;
using LKCode.AddressManagement.Service.Extensions;
using LKCode.AddressManagement.Service.Preconditions;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace LKCode.AddressManagement.Service.Controllers;

/// <summary>
/// The endpoints to create, read, list, change and delete addresses.
/// </summary>
/// <param name="addressService">The service that reads and stores the addresses.</param>
/// <param name="createAddressValidator">The validator of the requests to create an address.</param>
/// <param name="updateAddressValidator">The validator of the requests to change an address.</param>
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
    IValidator<UpdateAddressRequest> updateAddressValidator,
    IOptions<JsonOptions> jsonOptions) : ControllerBase
{
    private const string GetByIdRouteName = "GetAddressById";
    private const string ListRouteName = "ListAddresses";
    private const string CreateRouteName = "CreateAddress";
    private const string UpdateRouteName = "UpdateAddress";
    private const string PatchRouteName = "PatchAddress";
    private const string DeleteRouteName = "DeleteAddress";

    // MediaTypeNames has no constant for JSON Patch (RFC 6902, section 6).
    private const string JsonPatchMediaType = "application/json-patch+json";

    private const string MissingIfMatchDetail =
        "The request must contain the header If-Match with the current ETag of the address or '*'.";

    private const string InvalidIfMatchError =
        "The header If-Match must be '*' or a comma-separated list of entity tags, e.g. '\"3\"'.";

    private const string VersionMismatchDetail =
        "The version of the address does not match the header If-Match. Get the address again and repeat the "
        + "request with its current ETag.";

    private static readonly CompositeFormat _failedTestDetail = CompositeFormat.Parse(
        "The test operation at index {0} failed: the value at '{1}' is not equal to the test value.");

    private readonly IAddressService _addressService =
        addressService ?? throw new ArgumentNullException(nameof(addressService));

    private readonly IValidator<CreateAddressRequest> _createAddressValidator =
        createAddressValidator ?? throw new ArgumentNullException(nameof(createAddressValidator));

    private readonly IValidator<UpdateAddressRequest> _updateAddressValidator =
        updateAddressValidator ?? throw new ArgumentNullException(nameof(updateAddressValidator));

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
    /// The `ETag` header contains the strong entity tag of the current version of the address, e.g. `"3"`; a change
    /// requires it in its `If-Match` header. An unknown id and an id that is no GUID both result in `404 Not Found`,
    /// like an unknown route. It is not possible to tell whether an address with the id has ever existed.
    /// </remarks>
    /// <response code="200">The address; the `ETag` header contains its version.</response>
    /// <response code="404">There is no address with the id.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{id:guid}", Name = GetByIdRouteName)]
    [ProducesResponseType<AddressResponse>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<AddressResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        AddressResponse? address = await _addressService.GetByIdAsync(id, cancellationToken);
        if (address is null)
        {
            return NotFound();
        }

        Response.Headers.SetETag(address.Version);

        return Ok(address);
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
    [HttpGet(Name = ListRouteName)]
    [ProducesResponseType<IReadOnlyList<AddressResponse>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    public async Task<ActionResult<IReadOnlyList<AddressResponse>>> GetListAsync(
        [FromQuery] string? city,
        [FromQuery] string? postalCode,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AddressResponse> addresses =
            await _addressService.GetListAsync(new AddressListQuery(city, postalCode), cancellationToken);

        return Ok(addresses);
    }

    // In the actions with a request body (create and update), the parameter "request" is documented last: the
    // OpenAPI XML comment generator of .NET 10 writes the description of every parameter that is no operation
    // parameter, including the cancellation token, to the request body, so the last one wins.

    /// <summary>
    /// Creates a new address.
    /// </summary>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <param name="request">The values of the new address.</param>
    /// <returns>
    /// <c>201 Created</c> with the new address, its URL in the <c>Location</c> header and its version in the
    /// <c>ETag</c> header, or <c>400 Bad Request</c> with all validation errors.
    /// </returns>
    /// <remarks>
    /// Before the validation, all values are trimmed, the country is converted to uppercase and a blank email
    /// address is treated as missing. All validation errors are returned together, keyed by the name of the field,
    /// e.g. `firstName`. Errors in the JSON itself are keyed by their JSON path, e.g. `$.firstName`. A new address
    /// has the version 1.
    /// </remarks>
    /// <response code="201">
    /// The address was created; the `Location` header contains its URL, the `ETag` header its version.
    /// </response>
    /// <response code="400">The body is no valid JSON object of an address or contains invalid values.</response>
    /// <response code="415">The body is not JSON.</response>
    /// <response code="500">An unexpected error occurred.</response>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> is <see langword="null"/> although the model state is valid.
    /// </exception>
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
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

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
        Response.Headers.SetETag(address.Version);

        return CreatedAtRoute(GetByIdRouteName, new { id = address.Id }, address);
    }

    /// <summary>
    /// Replaces all values of the address with the given id.
    /// </summary>
    /// <param name="id">The id of the address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <param name="request">The new values of the address.</param>
    /// <returns>
    /// <c>200 OK</c> with the changed address and its new version in the <c>ETag</c> header; or an error, see
    /// the remarks.
    /// </returns>
    /// <remarks>
    /// All values are replaced; a missing or blank email address removes the existing one. Before the validation, all
    /// values are trimmed, the country is converted to uppercase and a blank email address is treated as missing. All
    /// validation errors are returned together, keyed by the name of the field, e.g. `firstName`. Errors in the JSON
    /// itself are keyed by their JSON path, e.g. `$.firstName`.
    ///
    /// The `If-Match` header is required and contains the `ETag` of the version to change, e.g. `"3"`, or `*` to
    /// change any version. Entity tags are compared strongly: a weak entity tag such as `W/"3"` never matches.
    ///
    /// The checks run in this order (RFC 9110, section 13.2.2): an unknown id or an id that is no GUID results in
    /// `404 Not Found`, like an unknown route; a missing `If-Match` header in `428 Precondition Required`; an
    /// `If-Match` header that is neither `*` nor a list of entity tags in `400 Bad Request`; an `If-Match` header
    /// without the current version in `412 Precondition Failed`; only then an invalid body in `400 Bad Request`. A
    /// change by someone else between these checks and the saving also results in `412 Precondition Failed`.
    /// </remarks>
    /// <response code="200">The address was changed; the `ETag` header contains its new version.</response>
    /// <response code="400">
    /// The `If-Match` header is invalid, or the body is no valid JSON object of an address or contains invalid
    /// values.
    /// </response>
    /// <response code="404">There is no address with the id.</response>
    /// <response code="412">
    /// The `If-Match` header does not contain the current version of the address; it was not changed.
    /// </response>
    /// <response code="415">The body is not JSON.</response>
    /// <response code="428">The `If-Match` header is missing.</response>
    /// <response code="500">An unexpected error occurred.</response>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> is <see langword="null"/> although the model state is valid.
    /// </exception>
    [HttpPut("{id:guid}", Name = UpdateRouteName)]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<AddressResponse>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest,
        MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status415UnsupportedMediaType,
        MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<AddressResponse>> UpdateAsync(
        Guid id,
        UpdateAddressRequest request,
        CancellationToken cancellationToken)
    {
        AddressResponse? address = await _addressService.GetByIdAsync(id, cancellationToken);
        if (address is null)
        {
            return NotFound();
        }

        if (CheckIfMatch(address.Version, out long? expectedVersion) is { } preconditionFailure)
        {
            return preconditionFailure;
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        ArgumentNullException.ThrowIfNull(request);

        return await SanitizeValidateAndUpdateAsync(id, expectedVersion, request, cancellationToken);
    }

    /// <summary>
    /// Changes single values of the address with the given id with a JSON Patch document (RFC 6902).
    /// </summary>
    /// <param name="id">The id of the address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <param name="patchDocument">The operations to apply to the values of the address.</param>
    /// <returns>
    /// <c>200 OK</c> with the changed address and its new version in the <c>ETag</c> header; or an error, see
    /// the remarks.
    /// </returns>
    /// <remarks>
    /// The operations are applied in their order to the current values of the address, as they would be sent with
    /// `PUT`. The changed values are then checked and stored like with `PUT`: all values are trimmed, the country is
    /// converted to uppercase and a blank email address is treated as missing. All operations take effect together
    /// or not at all.
    ///
    /// Supported are `add`, `remove`, `replace`, `move`, `copy` and `test`. `path` and `from` point to a field of the
    /// address with its exact name, e.g. `/firstName`; `add` and `replace` both set the value of a field, `remove`
    /// removes it. Removing a required value results in a validation error; a missing `value` is treated as `null`.
    ///
    /// The `If-Match` header is required and contains the `ETag` of the version to change, e.g. `"3"`, or `*` to
    /// change any version. Entity tags are compared strongly: a weak entity tag such as `W/"3"` never matches.
    ///
    /// The checks run in this order (RFC 9110, section 13.2.2): an unknown id or an id that is no GUID results in
    /// `404 Not Found`, like an unknown route; a missing `If-Match` header in `428 Precondition Required`; an
    /// `If-Match` header that is neither `*` nor a list of entity tags in `400 Bad Request`; an `If-Match` header
    /// without the current version in `412 Precondition Failed`. Only then the body is checked: errors in the patch
    /// document itself result in `400 Bad Request` with the errors keyed by their JSON path, e.g. `$[0].path`, a
    /// failed `test` operation in `409 Conflict`, and invalid changed values in `400 Bad Request` with the errors
    /// keyed by the name of the field, e.g. `firstName`. A change by someone else between these checks and the
    /// saving also results in `412 Precondition Failed`.
    /// </remarks>
    /// <response code="200">The address was changed; the `ETag` header contains its new version.</response>
    /// <response code="400">
    /// The `If-Match` header is invalid, the body is no valid JSON Patch document for an address, or the changed
    /// values are invalid.
    /// </response>
    /// <response code="404">There is no address with the id.</response>
    /// <response code="409">A `test` operation failed; the address was not changed.</response>
    /// <response code="412">
    /// The `If-Match` header does not contain the current version of the address; it was not changed.
    /// </response>
    /// <response code="415">The body is not a JSON Patch document (`application/json-patch+json`).</response>
    /// <response code="428">The `If-Match` header is missing.</response>
    /// <response code="500">An unexpected error occurred.</response>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="patchDocument"/> is <see langword="null"/> although the model state is valid.
    /// </exception>
    [HttpPatch("{id:guid}", Name = PatchRouteName)]
    [Consumes(JsonPatchMediaType)]
    [ProducesResponseType<AddressResponse>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest,
        MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status415UnsupportedMediaType,
        MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<AddressResponse>> PatchAsync(
        Guid id,
        JsonPatchDocument<UpdateAddressRequest> patchDocument,
        CancellationToken cancellationToken)
    {
        AddressResponse? address = await _addressService.GetByIdAsync(id, cancellationToken);
        if (address is null)
        {
            return NotFound();
        }

        if (CheckIfMatch(address.Version, out long? expectedVersion) is { } preconditionFailure)
        {
            return preconditionFailure;
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        ArgumentNullException.ThrowIfNull(patchDocument);

        ValidationResult operationsResult = patchDocument.ValidateOperations();
        if (!operationsResult.IsValid)
        {
            // The keys are JSON paths in the patch document, e.g. "$[0].path"; they are not converted.
            ModelState.AddValidationErrors(operationsResult, namingPolicy: null);

            return ValidationProblem(ModelState);
        }

        // The patch changes a new request with the current values, so a failed test leaves nothing changed.
        UpdateAddressRequest request = address.ToUpdateRequest();
        if (!patchDocument.TryApplyTo(request, out int failedTestIndex))
        {
            string detail = string.Format(
                CultureInfo.InvariantCulture,
                _failedTestDetail,
                failedTestIndex,
                patchDocument.Operations[failedTestIndex].path);

            return Problem(detail: detail, statusCode: StatusCodes.Status409Conflict);
        }

        return await SanitizeValidateAndUpdateAsync(id, expectedVersion, request, cancellationToken);
    }

    /// <summary>
    /// Deletes the address with the given id.
    /// </summary>
    /// <param name="id">The id of the address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>
    /// <c>204 No Content</c>; or an error, see the remarks.
    /// </returns>
    /// <remarks>
    /// The `If-Match` header is required and contains the `ETag` of the version to delete, e.g. `"3"`, or `*` to
    /// delete any version. Entity tags are compared strongly: a weak entity tag such as `W/"3"` never matches.
    ///
    /// The checks run in this order (RFC 9110, section 13.2.2): an unknown id or an id that is no GUID results in
    /// `404 Not Found`, like an unknown route; a missing `If-Match` header in `428 Precondition Required`; an
    /// `If-Match` header that is neither `*` nor a list of entity tags in `400 Bad Request`; an `If-Match` header
    /// without the current version in `412 Precondition Failed`. A change by someone else between these checks and
    /// the deletion also results in `412 Precondition Failed`.
    ///
    /// After the deletion, all requests for the id result in `404 Not Found`, including another deletion. It is not
    /// possible to tell whether an address with the id has ever existed.
    /// </remarks>
    /// <response code="204">The address was deleted.</response>
    /// <response code="400">The `If-Match` header is invalid.</response>
    /// <response code="404">There is no address with the id.</response>
    /// <response code="412">
    /// The `If-Match` header does not contain the current version of the address; it was not deleted.
    /// </response>
    /// <response code="428">The `If-Match` header is missing.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpDelete("{id:guid}", Name = DeleteRouteName)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        AddressResponse? address = await _addressService.GetByIdAsync(id, cancellationToken);
        if (address is null)
        {
            return NotFound();
        }

        if (CheckIfMatch(address.Version, out long? expectedVersion) is { } preconditionFailure)
        {
            return preconditionFailure;
        }

        ChangeStatus status = await _addressService.DeleteAsync(id, expectedVersion, cancellationToken);

        return status switch
        {
            ChangeStatus.Success => NoContent(),
            ChangeStatus.NotFound => NotFound(),
            _ => VersionMismatchProblem(),
        };
    }

    /// <summary>
    /// Sanitizes and validates the new values of an address and replaces the values of the address with them.
    /// </summary>
    /// <param name="id">The id of the address.</param>
    /// <param name="expectedVersion">
    /// The version the address must still have; <see langword="null"/> for any version.
    /// </param>
    /// <param name="request">The new values of the address, not yet sanitized.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>
    /// <c>200 OK</c> with the changed address and its new version in the <c>ETag</c> header,
    /// <c>400 Bad Request</c> with all validation errors, <c>404 Not Found</c> if the address was deleted in the
    /// meantime, or <c>412 Precondition Failed</c> if it does not have the expected version (anymore).
    /// </returns>
    private async Task<ActionResult<AddressResponse>> SanitizeValidateAndUpdateAsync(
        Guid id,
        long? expectedVersion,
        UpdateAddressRequest request,
        CancellationToken cancellationToken)
    {
        UpdateAddressRequest sanitizedRequest = request.Sanitize();
        ValidationResult validationResult =
            await _updateAddressValidator.ValidateAsync(sanitizedRequest, cancellationToken);
        if (!validationResult.IsValid)
        {
            ModelState.AddValidationErrors(validationResult, _jsonNamingPolicy);

            return ValidationProblem(ModelState);
        }

        AddressChangeResult result =
            await _addressService.UpdateAsync(id, expectedVersion, sanitizedRequest, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Status == ChangeStatus.NotFound ? NotFound() : VersionMismatchProblem();
        }

        Response.Headers.SetETag(result.Address.Version);

        return Ok(result.Address);
    }

    /// <summary>
    /// Evaluates the <c>If-Match</c> header of the request against the <paramref name="currentVersion"/> of the
    /// address (RFC 9110, section 13.1.1).
    /// </summary>
    /// <param name="currentVersion">The current version of the address.</param>
    /// <param name="expectedVersion">
    /// The version the change expects: the <paramref name="currentVersion"/> if the header contains its entity tag;
    /// <see langword="null"/> if the header is <c>*</c> or the precondition fails.
    /// </param>
    /// <returns>
    /// <see langword="null"/> if the change may continue; otherwise <c>428 Precondition Required</c>,
    /// <c>400 Bad Request</c> or <c>412 Precondition Failed</c>.
    /// </returns>
    private ActionResult? CheckIfMatch(long currentVersion, out long? expectedVersion)
    {
        expectedVersion = null;
        switch (Request.Headers.EvaluateIfMatch(currentVersion))
        {
            case IfMatchOutcome.Missing:
                return Problem(detail: MissingIfMatchDetail, statusCode: StatusCodes.Status428PreconditionRequired);
            case IfMatchOutcome.Invalid:
            {
                // Only the header is reported: the body is checked after the preconditions.
                var errors = new ModelStateDictionary();
                errors.AddModelError(HeaderNames.IfMatch, InvalidIfMatchError);

                return ValidationProblem(errors);
            }

            case IfMatchOutcome.NotMatched:
                return VersionMismatchProblem();
            case IfMatchOutcome.Matched:
                expectedVersion = currentVersion;
                return null;
            default:
                return null;
        }
    }

    private ObjectResult VersionMismatchProblem() =>
        Problem(detail: VersionMismatchDetail, statusCode: StatusCodes.Status412PreconditionFailed);
}
