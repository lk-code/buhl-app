using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Sanitizers;
using LKCode.AddressManagement.Service.Controllers;
using Microsoft.AspNetCore.Http;
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
        _createdAt);

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

    private ServiceProvider _serviceProvider = null!;
    private IAddressService _addressService = null!;
    private IValidator<CreateAddressRequest> _createAddressValidator = null!;
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
        _jsonOptions = Options.Create(new JsonOptions());
        _controller = new AddressesController(_addressService, _createAddressValidator, _jsonOptions)
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
        Action act = () => _ = new AddressesController(addressService, _createAddressValidator, _jsonOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("addressService");
    }

    [Test]
    public void Constructor_NullCreateAddressValidator_ThrowsArgumentNullException()
    {
        // Arrange
        IValidator<CreateAddressRequest> createAddressValidator = null!;

        // Act
        Action act = () => _ = new AddressesController(_addressService, createAddressValidator, _jsonOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("createAddressValidator");
    }

    [Test]
    public void Constructor_NullJsonOptions_ThrowsArgumentNullException()
    {
        // Arrange
        IOptions<JsonOptions> jsonOptions = null!;

        // Act
        Action act = () => _ = new AddressesController(_addressService, _createAddressValidator, jsonOptions);

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

    private void ReturnValidationErrors(params ValidationFailure[] failures)
    {
        _createAddressValidator
            .ValidateAsync(Arg.Any<CreateAddressRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));
    }
}
