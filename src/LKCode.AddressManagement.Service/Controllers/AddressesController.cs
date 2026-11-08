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
using LKCode.AddressManagement.Service.OutputCachePolicies;
using LKCode.AddressManagement.Service.Preconditions;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace LKCode.AddressManagement.Service.Controllers;

/// <summary>
/// The endpoints to create, read, list, change and delete addresses.
/// </summary>
/// <param name="addressService">The service that reads and stores the addresses.</param>
/// <param name="createAddressValidator">The validator of the requests to create an address.</param>
/// <param name="updateAddressValidator">The validator of the requests to change an address.</param>
/// <param name="addressListQueryValidator">The validator of the paging and sorting parameters of the list.</param>
/// <param name="jsonOptions">
/// The JSON options of the controllers; their naming policy converts the property names of validation errors.
/// </param>
/// <param name="outputCacheStore">
/// The store of the output cache, from which every successful change removes the affected responses.
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
    IValidator<AddressListQuery> addressListQueryValidator,
    IOptions<JsonOptions> jsonOptions,
    IOutputCacheStore outputCacheStore) : ControllerBase
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

    private readonly IValidator<AddressListQuery> _addressListQueryValidator =
        addressListQueryValidator ?? throw new ArgumentNullException(nameof(addressListQueryValidator));

    private readonly JsonNamingPolicy? _jsonNamingPolicy =
        (jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions)))
        .Value.JsonSerializerOptions.PropertyNamingPolicy;

    private readonly IOutputCacheStore _outputCacheStore =
        outputCacheStore ?? throw new ArgumentNullException(nameof(outputCacheStore));

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
    /// Gets one page of the addresses, optionally filtered by city and postal code and sorted by a field.
    /// </summary>
    /// <param name="city">A part of the city to search for. Missing or blank to ignore the city.</param>
    /// <param name="postalCode">
    /// A part of the postal code to search for. Missing or blank to ignore the postal code.
    /// </param>
    /// <param name="page">The number of the page, starting at 1. Missing or blank for the first page.</param>
    /// <param name="pageSize">
    /// The maximum number of addresses on a page, from 1 to 100. Missing or blank for 20.
    /// </param>
    /// <param name="sortBy">
    /// The field to sort by: `lastName`, `firstName`, `city`, `postalCode`, `country`, `createdAt` or `updatedAt`.
    /// Missing or blank for `lastName`.
    /// </param>
    /// <param name="sortDirection">The sort direction: `asc` or `desc`. Missing or blank for `asc`.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>
    /// The page of the matching addresses, or <c>400 Bad Request</c> with all errors of the paging and sorting
    /// parameters.
    /// </returns>
    /// <remarks>
    /// Both filters are optional and search for a part of the value, e.g. `sol` finds `Solingen`. They are compared
    /// with normalized values: the case, surrounding whitespace and accents do not matter, umlauts and their
    /// spellings with `e` are the same (`Koeln` finds `Köln`), and spaces in postal codes are ignored. If both filters
    /// are given, an address must match both.
    ///
    /// The response contains the addresses of the requested page in `items`, the page and the page size as
    /// requested, the number of all matching addresses in `totalCount` and the number of pages in `totalPages`. A
    /// page behind the end has no `items`; it is no error.
    ///
    /// Addresses with the same value of the sort field are sorted by their id in the same direction, so that the
    /// order is stable and every address is on exactly one page; `desc` is the exact reverse of `asc`. The names of
    /// the sort field and the sort direction may be in any case. Texts are compared as the database compares them.
    ///
    /// A page below 1, a page size outside 1 to 100, a value that is no integer, and an unknown sort field or
    /// direction result in `400 Bad Request` with all errors together, keyed by the name of the parameter, e.g.
    /// `pageSize`.
    /// </remarks>
    /// <response code="200">
    /// The page of the matching addresses; `items` is empty if none matches or the page is behind the end.
    /// </response>
    /// <response code="400">A paging or sorting parameter is invalid.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet(Name = ListRouteName)]
    [ProducesResponseType<AddressListResponse>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest,
        MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<AddressListResponse>> GetListAsync(
        [FromQuery] string? city,
        [FromQuery] string? postalCode,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        CancellationToken cancellationToken)
    {
        // A value that is no integer is already in the model state; it is reported together with the other errors.
        AddressListQuery query =
            new AddressListQuery(city, postalCode, page, pageSize, sortBy, sortDirection).Sanitize();
        ValidationResult validationResult = await _addressListQueryValidator.ValidateAsync(query, cancellationToken);
        ModelState.AddValidationErrors(validationResult, _jsonNamingPolicy);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        AddressListResponse addresses = await _addressService.GetListAsync(query, cancellationToken);

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

        // A new address can appear on every page of the list; no cached single address is affected.
        await _outputCacheStore.EvictByTagAsync(AddressCacheTags.List, cancellationToken);
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
        StatusCodes.Status412PreconditionFailed,
        MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status415UnsupportedMediaType,
        MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status428PreconditionRequired,
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
        StatusCodes.Status412PreconditionFailed,
        MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status415UnsupportedMediaType,
        MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status428PreconditionRequired,
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
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest,
        MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status412PreconditionFailed,
        MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status428PreconditionRequired,
        MediaTypeNames.Application.ProblemJson)]
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
        if (status != ChangeStatus.Success)
        {
            return status == ChangeStatus.NotFound ? NotFound() : VersionMismatchProblem();
        }

        await EvictAddressAsync(id, cancellationToken);

        return NoContent();
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

        await EvictAddressAsync(id, cancellationToken);
        Response.Headers.SetETag(result.Address.Version);

        return Ok(result.Address);
    }

    /// <summary>
    /// Removes the cached responses that contain the changed or deleted address from the output cache: the address
    /// itself and every page of the list.
    /// </summary>
    /// <param name="id">The id of the address.</param>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    /// <returns>A task that completes when the responses are removed.</returns>
    private async Task EvictAddressAsync(Guid id, CancellationToken cancellationToken)
    {
        await _outputCacheStore.EvictByTagAsync(AddressCacheTags.ForAddress(id), cancellationToken);
        await _outputCacheStore.EvictByTagAsync(AddressCacheTags.List, cancellationToken);
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
