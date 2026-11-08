using System.Text.Json;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Results;
using LKCode.AddressManagement.Core.Addresses.Sanitizers;
using LKCode.AddressManagement.Service.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LKCode.AddressManagement.UnitTests.Service.Controllers;

public class AddressesControllerTests
{
    private const string GetByIdRouteName = "GetAddressById";

    private static readonly DateTimeOffset _createdAt = new(2026, 3, 14, 9, 26, 53, 589, TimeSpan.Zero);

    private static readonly AddressResponse _address = new(
        Guid.CreateVersion7(_createdAt),
        "Erika",
        "Mustermann",
        "Heidestraße",
        "17",
        "51147",
        "Köln",
        "DE",
        "erika.mustermann@example.com",
        _createdAt,
        _createdAt,
        1);

    private static readonly CreateAddressRequest _validRequest = new(
        "Erika",
        "Mustermann",
        "Heidestraße",
        "17",
        "51147",
        "Köln",
        "DE",
        "erika.mustermann@example.com");

    private static readonly CreateAddressRequest _unsanitizedRequest = new(
        "  Erika ",
        " Mustermann",
        "Heidestraße  ",
        " 17 ",
        "\t51147",
        "Köln\n",
        " de ",
        "   ");

    private static readonly UpdateAddressRequest _validUpdateRequest = new(
        "Max",
        "Mustermann",
        "Hauptstraße",
        "1",
        "10115",
        "Berlin",
        "DE",
        null);

    private static readonly UpdateAddressRequest _unsanitizedUpdateRequest = new(
        " Max ",
        "Mustermann\t",
        "  Hauptstraße",
        "1 ",
        " 10115",
        "\nBerlin",
        "de",
        " max.mustermann@example.com ");

    private const string ReplaceFirstName = """{"op": "replace", "path": "/firstName", "value": "Max"}""";

    private ServiceProvider _serviceProvider = null!;
    private IAddressService _addressService = null!;
    private IValidator<CreateAddressRequest> _createAddressValidator = null!;
    private IValidator<UpdateAddressRequest> _updateAddressValidator = null!;
    private IOptions<JsonOptions> _jsonOptions = null!;
    private AddressesController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = new ServiceCollection().AddControllers().Services.BuildServiceProvider();
        _addressService = Substitute.For<IAddressService>();
        _addressService.CreateAsync(Arg.Any<CreateAddressRequest>(), Arg.Any<CancellationToken>()).Returns(_address);
        _createAddressValidator = Substitute.For<IValidator<CreateAddressRequest>>();
        _createAddressValidator
            .ValidateAsync(Arg.Any<CreateAddressRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _updateAddressValidator = Substitute.For<IValidator<UpdateAddressRequest>>();
        _updateAddressValidator
            .ValidateAsync(Arg.Any<UpdateAddressRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _jsonOptions = Options.Create(new JsonOptions());
        _controller = new AddressesController(
            _addressService,
            _createAddressValidator,
            _updateAddressValidator,
            _jsonOptions)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = _serviceProvider },
            },
        };
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [Test]
    public void Constructor_NullAddressService_ThrowsArgumentNullException()
    {
        // Arrange
        IAddressService addressService = null!;

        // Act
        Action act = () => _ = new AddressesController(
            addressService,
            _createAddressValidator,
            _updateAddressValidator,
            _jsonOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("addressService");
    }

    [Test]
    public void Constructor_NullCreateAddressValidator_ThrowsArgumentNullException()
    {
        // Arrange
        IValidator<CreateAddressRequest> createAddressValidator = null!;

        // Act
        Action act = () => _ = new AddressesController(
            _addressService,
            createAddressValidator,
            _updateAddressValidator,
            _jsonOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("createAddressValidator");
    }

    [Test]
    public void Constructor_NullUpdateAddressValidator_ThrowsArgumentNullException()
    {
        // Arrange
        IValidator<UpdateAddressRequest> updateAddressValidator = null!;

        // Act
        Action act = () => _ = new AddressesController(
            _addressService,
            _createAddressValidator,
            updateAddressValidator,
            _jsonOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("updateAddressValidator");
    }

    [Test]
    public void Constructor_NullJsonOptions_ThrowsArgumentNullException()
    {
        // Arrange
        IOptions<JsonOptions> jsonOptions = null!;

        // Act
        Action act = () => _ = new AddressesController(
            _addressService,
            _createAddressValidator,
            _updateAddressValidator,
            jsonOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("jsonOptions");
    }

    [Test]
    public async Task GetByIdAsync_KnownId_ReturnsOkWithAddress()
    {
        // Arrange
        _addressService.GetByIdAsync(_address.Id, Arg.Any<CancellationToken>()).Returns(_address);

        // Act
        ActionResult<AddressResponse> result = await _controller.GetByIdAsync(_address.Id, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(_address);
    }

    [Test]
    public async Task GetByIdAsync_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.CreateVersion7(_createdAt);
        _addressService.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((AddressResponse?)null);

        // Act
        ActionResult<AddressResponse> result = await _controller.GetByIdAsync(id, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task GetByIdAsync_AnyId_PassesIdAndCancellationToken()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _controller.GetByIdAsync(_address.Id, cancellationTokenSource.Token);

        // Assert
        await _addressService.Received(1).GetByIdAsync(_address.Id, cancellationTokenSource.Token);
    }

    [TestCase("sol", "426")]
    [TestCase("sol", null)]
    [TestCase(null, "426")]
    [TestCase(null, null)]
    [TestCase(" ", "")]
    public async Task GetListAsync_Filters_PassesQueryAndCancellationToken(string? city, string? postalCode)
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _controller.GetListAsync(city, postalCode, cancellationTokenSource.Token);

        // Assert
        await _addressService.Received(1)
            .GetListAsync(new AddressListQuery(city, postalCode), cancellationTokenSource.Token);
    }

    [Test]
    public async Task GetListAsync_MatchingAddresses_ReturnsOkWithAddresses()
    {
        // Arrange
        IReadOnlyList<AddressResponse> addresses = [_address, _address with { Id = Guid.CreateVersion7(_createdAt) }];
        _addressService.GetListAsync(Arg.Any<AddressListQuery>(), Arg.Any<CancellationToken>()).Returns(addresses);

        // Act
        ActionResult<IReadOnlyList<AddressResponse>> result =
            await _controller.GetListAsync("Köln", null, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(addresses);
    }

    [Test]
    public async Task GetListAsync_NoMatches_ReturnsOkWithEmptyList()
    {
        // Arrange
        IReadOnlyList<AddressResponse> noAddresses = [];
        _addressService.GetListAsync(Arg.Any<AddressListQuery>(), Arg.Any<CancellationToken>()).Returns(noAddresses);

        // Act
        ActionResult<IReadOnlyList<AddressResponse>> result =
            await _controller.GetListAsync("Berlin", null, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IReadOnlyList<AddressResponse>>().Which.Should().BeEmpty();
    }

    [Test]
    public async Task CreateAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        CreateAddressRequest request = null!;

        // Act
        Func<Task> act = () => _controller.CreateAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    [Test]
    public async Task CreateAsync_UnsanitizedRequest_ValidatesSanitizedRequest()
    {
        // Act
        await _controller.CreateAsync(_unsanitizedRequest, CancellationToken.None);

        // Assert
        await _createAddressValidator.Received(1)
            .ValidateAsync(_unsanitizedRequest.Sanitize(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateAsync_UnsanitizedValidRequest_CreatesSanitizedRequest()
    {
        // Act
        await _controller.CreateAsync(_unsanitizedRequest, CancellationToken.None);

        // Assert
        await _addressService.Received(1).CreateAsync(_unsanitizedRequest.Sanitize(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateAsync_ValidRequest_PassesCancellationTokenToValidatorAndService()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _controller.CreateAsync(_validRequest, cancellationTokenSource.Token);

        // Assert
        await _createAddressValidator.Received(1)
            .ValidateAsync(Arg.Any<CreateAddressRequest>(), cancellationTokenSource.Token);
        await _addressService.Received(1).CreateAsync(Arg.Any<CreateAddressRequest>(), cancellationTokenSource.Token);
    }

    [Test]
    public async Task CreateAsync_ValidRequest_ReturnsCreatedAtRouteOfGetById()
    {
        // Act
        ActionResult<AddressResponse> result = await _controller.CreateAsync(_validRequest, CancellationToken.None);

        // Assert
        CreatedAtRouteResult created = result.Result.Should().BeOfType<CreatedAtRouteResult>().Which;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.RouteName.Should().Be(GetByIdRouteName);
        created.RouteValues.Should().ContainSingle().Which.Should().Be(
            new KeyValuePair<string, object?>("id", _address.Id));
        created.Value.Should().BeSameAs(_address);
    }

    [Test]
    public async Task CreateAsync_InvalidRequest_ReturnsValidationProblemWithJsonNames()
    {
        // Arrange
        ReturnValidationErrors(
            new ValidationFailure("FirstName", "'First Name' must not be empty."),
            new ValidationFailure("Country", "'Country' must consist of exactly two uppercase letters."),
            new ValidationFailure("Email", "'Email' is not a valid email address."));

        // Act
        ActionResult<AddressResponse> result = await _controller.CreateAsync(_validRequest, CancellationToken.None);

        // Assert
        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Which;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ValidationProblemDetails problem = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Which;
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["firstName"] = ["'First Name' must not be empty."],
            ["country"] = ["'Country' must consist of exactly two uppercase letters."],
            ["email"] = ["'Email' is not a valid email address."],
        });
    }

    [Test]
    public async Task CreateAsync_InvalidRequest_DoesNotCreateAddress()
    {
        // Arrange
        ReturnValidationErrors(new ValidationFailure("FirstName", "'First Name' must not be empty."));

        // Act
        await _controller.CreateAsync(_validRequest, CancellationToken.None);

        // Assert
        await _addressService.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Test]
    public async Task UpdateAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        UpdateAddressRequest request = null!;

        // Act
        Func<Task> act = () => _controller.UpdateAsync(_address.Id, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    [Test]
    public async Task UpdateAsync_UnsanitizedRequest_ValidatesSanitizedRequest()
    {
        // Act
        await _controller.UpdateAsync(_address.Id, _unsanitizedUpdateRequest, CancellationToken.None);

        // Assert
        await _updateAddressValidator.Received(1)
            .ValidateAsync(_unsanitizedUpdateRequest.Sanitize(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_UnsanitizedValidRequest_UpdatesAddressWithSanitizedRequest()
    {
        // Act
        await _controller.UpdateAsync(_address.Id, _unsanitizedUpdateRequest, CancellationToken.None);

        // Assert
        await _addressService.Received(1)
            .UpdateAsync(
                _address.Id,
                Arg.Any<long?>(),
                _unsanitizedUpdateRequest.Sanitize(),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_ValidRequest_PassesCancellationTokenToValidatorAndService()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _controller.UpdateAsync(_address.Id, _validUpdateRequest, cancellationTokenSource.Token);

        // Assert
        await _updateAddressValidator.Received(1)
            .ValidateAsync(Arg.Any<UpdateAddressRequest>(), cancellationTokenSource.Token);
        await _addressService.Received(1)
            .UpdateAsync(
                Arg.Any<Guid>(),
                Arg.Any<long?>(),
                Arg.Any<UpdateAddressRequest>(),
                cancellationTokenSource.Token);
    }

    [Test]
    public async Task UpdateAsync_KnownId_ReturnsOkWithAddress()
    {
        // Arrange
        AddressResponse updated = _address with { FirstName = "Max", City = "Berlin" };
        _addressService
            .UpdateAsync(_address.Id, Arg.Any<long?>(), Arg.Any<UpdateAddressRequest>(), Arg.Any<CancellationToken>())
            .Returns(AddressChangeResult.Success(updated));

        // Act
        ActionResult<AddressResponse> result =
            await _controller.UpdateAsync(_address.Id, _validUpdateRequest, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(updated);
    }

    [Test]
    public async Task UpdateAsync_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.CreateVersion7(_createdAt);
        _addressService
            .UpdateAsync(id, Arg.Any<long?>(), Arg.Any<UpdateAddressRequest>(), Arg.Any<CancellationToken>())
            .Returns(AddressChangeResult.NotFound);

        // Act
        ActionResult<AddressResponse> result =
            await _controller.UpdateAsync(id, _validUpdateRequest, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task UpdateAsync_InvalidRequest_ReturnsValidationProblemWithJsonNames()
    {
        // Arrange
        ReturnUpdateValidationErrors(
            new ValidationFailure("LastName", "'Last Name' must not be empty."),
            new ValidationFailure("HouseNumber", "The length of 'House Number' must be 20 characters or fewer."),
            new ValidationFailure("Country", "'Country' must be an assigned ISO 3166-1 alpha-2 country code."));

        // Act
        ActionResult<AddressResponse> result =
            await _controller.UpdateAsync(_address.Id, _validUpdateRequest, CancellationToken.None);

        // Assert
        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Which;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ValidationProblemDetails problem = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Which;
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["lastName"] = ["'Last Name' must not be empty."],
            ["houseNumber"] = ["The length of 'House Number' must be 20 characters or fewer."],
            ["country"] = ["'Country' must be an assigned ISO 3166-1 alpha-2 country code."],
        });
    }

    [Test]
    public async Task UpdateAsync_InvalidRequest_DoesNotUpdateAddress()
    {
        // Arrange
        ReturnUpdateValidationErrors(new ValidationFailure("FirstName", "'First Name' must not be empty."));

        // Act
        await _controller.UpdateAsync(_address.Id, _validUpdateRequest, CancellationToken.None);

        // Assert
        await _addressService.DidNotReceiveWithAnyArgs().UpdateAsync(default, default, default!, default);
    }

    [Test]
    public async Task PatchAsync_NullPatchDocument_ThrowsArgumentNullException()
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument = null!;

        // Act
        Func<Task> act = () => _controller.PatchAsync(_address.Id, patchDocument, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("patchDocument");
    }

    [Test]
    public async Task PatchAsync_InvalidOperations_ReturnsValidationProblemWithJsonPaths()
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument = ParsePatch(
            """{"op": "replace", "path": "/firstName", "value": "Max"}""",
            """{"op": "foo", "path": "/FirstName"}""");

        // Act
        ActionResult<AddressResponse> result =
            await _controller.PatchAsync(_address.Id, patchDocument, CancellationToken.None);

        // Assert
        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Which;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ValidationProblemDetails problem = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Which;
        problem.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["$[1].op"] = ["The operation must be 'add', 'remove', 'replace', 'move', 'copy' or 'test'."],
            ["$[1].path"] = ["The path must point to a field of the address, e.g. '/firstName'."],
        });
    }

    [Test]
    public async Task PatchAsync_InvalidOperations_DoesNotLoadOrUpdateAddress()
    {
        // Arrange
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            ParsePatch("""{"op": "replace", "path": "/unknown", "value": "Max"}""");

        // Act
        await _controller.PatchAsync(_address.Id, patchDocument, CancellationToken.None);

        // Assert
        await _addressService.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
        await _addressService.DidNotReceiveWithAnyArgs().UpdateAsync(default, default, default!, default);
    }

    [Test]
    public async Task PatchAsync_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.CreateVersion7(_createdAt);
        _addressService.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((AddressResponse?)null);

        // Act
        ActionResult<AddressResponse> result =
            await _controller.PatchAsync(id, ParsePatch(ReplaceFirstName), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
        await _addressService.DidNotReceiveWithAnyArgs().UpdateAsync(default, default, default!, default);
    }

    [Test]
    public async Task PatchAsync_FailedTest_ReturnsConflictWithProblemDetails()
    {
        // Arrange
        ReturnAddressForPatch();
        JsonPatchDocument<UpdateAddressRequest> patchDocument = ParsePatch(
            ReplaceFirstName,
            """{"op": "test", "path": "/city", "value": "Berlin"}""");

        // Act
        ActionResult<AddressResponse> result =
            await _controller.PatchAsync(_address.Id, patchDocument, CancellationToken.None);

        // Assert
        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Which;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Which;
        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Conflict");
        problem.Detail.Should().Be(
            "The test operation at index 1 failed: the value at '/city' is not equal to the test value.");
    }

    [Test]
    public async Task PatchAsync_FailedTest_DoesNotValidateOrUpdateAddress()
    {
        // Arrange
        ReturnAddressForPatch();
        JsonPatchDocument<UpdateAddressRequest> patchDocument =
            ParsePatch("""{"op": "test", "path": "/firstName", "value": "Max"}""", ReplaceFirstName);

        // Act
        await _controller.PatchAsync(_address.Id, patchDocument, CancellationToken.None);

        // Assert
        await _updateAddressValidator.DidNotReceiveWithAnyArgs().ValidateAsync(default!, default);
        await _addressService.DidNotReceiveWithAnyArgs().UpdateAsync(default, default, default!, default);
    }

    [Test]
    public async Task PatchAsync_ValidPatch_ValidatesSanitizedPatchedValues()
    {
        // Arrange
        ReturnAddressForPatch();
        JsonPatchDocument<UpdateAddressRequest> patchDocument = ParsePatch(
            """{"op": "replace", "path": "/firstName", "value": " Max "}""",
            """{"op": "replace", "path": "/country", "value": "at"}""",
            """{"op": "replace", "path": "/email", "value": "   "}""");

        // Act
        await _controller.PatchAsync(_address.Id, patchDocument, CancellationToken.None);

        // Assert
        await _updateAddressValidator.Received(1).ValidateAsync(
            new UpdateAddressRequest("Max", "Mustermann", "Heidestraße", "17", "51147", "Köln", "AT", null),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PatchAsync_ValidPatch_UpdatesAddressWithSanitizedPatchedValues()
    {
        // Arrange
        ReturnAddressForPatch();
        JsonPatchDocument<UpdateAddressRequest> patchDocument = ParsePatch(
            """{"op": "test", "path": "/lastName", "value": "Mustermann"}""",
            """{"op": "copy", "from": "/city", "path": "/street"}""",
            """{"op": "remove", "path": "/email"}""",
            """{"op": "add", "path": "/houseNumber", "value": " 4a"}""");

        // Act
        await _controller.PatchAsync(_address.Id, patchDocument, CancellationToken.None);

        // Assert
        await _addressService.Received(1).UpdateAsync(
            _address.Id,
            Arg.Any<long?>(),
            new UpdateAddressRequest("Erika", "Mustermann", "Köln", "4a", "51147", "Köln", "DE", null),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PatchAsync_NoOperations_UpdatesAddressWithCurrentValues()
    {
        // Arrange
        ReturnAddressForPatch();

        // Act
        await _controller.PatchAsync(_address.Id, ParsePatch(), CancellationToken.None);

        // Assert
        await _addressService.Received(1).UpdateAsync(
            _address.Id,
            Arg.Any<long?>(),
            new UpdateAddressRequest(
                "Erika",
                "Mustermann",
                "Heidestraße",
                "17",
                "51147",
                "Köln",
                "DE",
                "erika.mustermann@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PatchAsync_ValidPatch_ReturnsOkWithAddress()
    {
        // Arrange
        ReturnAddressForPatch();
        AddressResponse updated = _address with { FirstName = "Max" };
        _addressService
            .UpdateAsync(_address.Id, Arg.Any<long?>(), Arg.Any<UpdateAddressRequest>(), Arg.Any<CancellationToken>())
            .Returns(AddressChangeResult.Success(updated));

        // Act
        ActionResult<AddressResponse> result =
            await _controller.PatchAsync(_address.Id, ParsePatch(ReplaceFirstName), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(updated);
    }

    [Test]
    public async Task PatchAsync_AddressDeletedBeforeUpdate_ReturnsNotFound()
    {
        // Arrange
        ReturnAddressForPatch();
        _addressService
            .UpdateAsync(_address.Id, Arg.Any<long?>(), Arg.Any<UpdateAddressRequest>(), Arg.Any<CancellationToken>())
            .Returns(AddressChangeResult.NotFound);

        // Act
        ActionResult<AddressResponse> result =
            await _controller.PatchAsync(_address.Id, ParsePatch(ReplaceFirstName), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task PatchAsync_InvalidPatchedValues_ReturnsValidationProblemWithJsonNames()
    {
        // Arrange
        ReturnAddressForPatch();
        ReturnUpdateValidationErrors(
            new ValidationFailure("FirstName", "'First Name' must not be empty."),
            new ValidationFailure("PostalCode", "'Postal Code' must not be empty."));
        JsonPatchDocument<UpdateAddressRequest> patchDocument = ParsePatch(
            """{"op": "remove", "path": "/firstName"}""",
            """{"op": "move", "from": "/postalCode", "path": "/city"}""");

        // Act
        ActionResult<AddressResponse> result =
            await _controller.PatchAsync(_address.Id, patchDocument, CancellationToken.None);

        // Assert
        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Which;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ValidationProblemDetails problem = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Which;
        problem.Errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["firstName"] = ["'First Name' must not be empty."],
            ["postalCode"] = ["'Postal Code' must not be empty."],
        });
        await _updateAddressValidator.Received(1).ValidateAsync(
            new UpdateAddressRequest(
                string.Empty,
                "Mustermann",
                "Heidestraße",
                "17",
                string.Empty,
                "51147",
                "DE",
                "erika.mustermann@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PatchAsync_InvalidPatchedValues_DoesNotUpdateAddress()
    {
        // Arrange
        ReturnAddressForPatch();
        ReturnUpdateValidationErrors(new ValidationFailure("FirstName", "'First Name' must not be empty."));

        // Act
        await _controller.PatchAsync(
            _address.Id,
            ParsePatch("""{"op": "remove", "path": "/firstName"}"""),
            CancellationToken.None);

        // Assert
        await _addressService.DidNotReceiveWithAnyArgs().UpdateAsync(default, default, default!, default);
    }

    [Test]
    public async Task PatchAsync_ValidPatch_PassesIdAndCancellationTokenToServiceAndValidator()
    {
        // Arrange
        ReturnAddressForPatch();
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _controller.PatchAsync(_address.Id, ParsePatch(ReplaceFirstName), cancellationTokenSource.Token);

        // Assert
        await _addressService.Received(1).GetByIdAsync(_address.Id, cancellationTokenSource.Token);
        await _updateAddressValidator.Received(1)
            .ValidateAsync(Arg.Any<UpdateAddressRequest>(), cancellationTokenSource.Token);
        await _addressService.Received(1)
            .UpdateAsync(_address.Id, Arg.Any<long?>(), Arg.Any<UpdateAddressRequest>(), cancellationTokenSource.Token);
    }

    [Test]
    public async Task DeleteAsync_KnownId_ReturnsNoContent()
    {
        // Arrange
        _addressService.DeleteAsync(_address.Id, Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(ChangeStatus.Success);

        // Act
        IActionResult result = await _controller.DeleteAsync(_address.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task DeleteAsync_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.CreateVersion7(_createdAt);
        _addressService.DeleteAsync(id, Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(ChangeStatus.NotFound);

        // Act
        IActionResult result = await _controller.DeleteAsync(id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task DeleteAsync_AnyId_PassesIdAndCancellationToken()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _controller.DeleteAsync(_address.Id, cancellationTokenSource.Token);

        // Assert
        await _addressService.Received(1).DeleteAsync(_address.Id, Arg.Any<long?>(), cancellationTokenSource.Token);
    }

    private static JsonPatchDocument<UpdateAddressRequest> ParsePatch(params string[] operations) =>
        JsonSerializer.Deserialize<JsonPatchDocument<UpdateAddressRequest>>(
            $"[{string.Join(", ", operations)}]",
            JsonSerializerOptions.Web)!;

    private void ReturnAddressForPatch()
    {
        _addressService.GetByIdAsync(_address.Id, Arg.Any<CancellationToken>()).Returns(_address);
    }

    private void ReturnValidationErrors(params ValidationFailure[] failures)
    {
        _createAddressValidator
            .ValidateAsync(Arg.Any<CreateAddressRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));
    }

    private void ReturnUpdateValidationErrors(params ValidationFailure[] failures)
    {
        _updateAddressValidator
            .ValidateAsync(Arg.Any<UpdateAddressRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));
    }
}
