using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Mappings;
using LKCode.AddressManagement.Domain.Addresses;
using LKCode.AddressManagement.Domain.Contracts;
using LKCode.AddressManagement.UnitTests.Domain.Addresses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses;

public class AddressServiceTests
{
    private static readonly DateTimeOffset _startTime = new(2026, 3, 14, 9, 26, 53, 589, TimeSpan.Zero);

    private IAddressRepository _addressRepository = null!;
    private ITextNormalizer _textNormalizer = null!;
    private TimeProvider _timeProvider = null!;
    private FakeLogger<AddressService> _logger = null!;
    private AddressService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _addressRepository = Substitute.For<IAddressRepository>();
        _textNormalizer = Substitute.For<ITextNormalizer>();
        _textNormalizer.Normalize(Arg.Any<string>()).Returns(call => "city:" + call.Arg<string>());
        _textNormalizer.NormalizePostalCode(Arg.Any<string>()).Returns(call => "postal:" + call.Arg<string>());
        _timeProvider = Substitute.For<TimeProvider>();
        _timeProvider.GetUtcNow().Returns(_startTime);
        _logger = new FakeLogger<AddressService>();
        _service = new AddressService(_addressRepository, _textNormalizer, _timeProvider, _logger);
    }

    [Test]
    public void Constructor_NullAddressRepository_ThrowsArgumentNullException()
    {
        // Arrange
        IAddressRepository addressRepository = null!;

        // Act
        Action act = () => _ = new AddressService(addressRepository, _textNormalizer, _timeProvider, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("addressRepository");
    }

    [Test]
    public void Constructor_NullTextNormalizer_ThrowsArgumentNullException()
    {
        // Arrange
        ITextNormalizer textNormalizer = null!;

        // Act
        Action act = () => _ = new AddressService(_addressRepository, textNormalizer, _timeProvider, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("textNormalizer");
    }

    [Test]
    public void Constructor_NullTimeProvider_ThrowsArgumentNullException()
    {
        // Arrange
        TimeProvider timeProvider = null!;

        // Act
        Action act = () => _ = new AddressService(_addressRepository, _textNormalizer, timeProvider, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("timeProvider");
    }

    [Test]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        ILogger<AddressService> logger = null!;

        // Act
        Action act = () => _ = new AddressService(_addressRepository, _textNormalizer, _timeProvider, logger);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Test]
    public async Task CreateAsync_ValidRequest_AddsAddressWithAllValues()
    {
        // Arrange
        CreateAddressRequest request = AddressInput.Valid.ToCreateRequest();

        // Act
        await _service.CreateAsync(request, CancellationToken.None);

        // Assert
        AddressInput.From(GetAddedAddress()).Should().Be(AddressInput.Valid);
    }

    [Test]
    public async Task CreateAsync_ValidRequest_AddsAddressWithIdTimestampsAndNormalizedFields()
    {
        // Arrange
        CreateAddressRequest request = AddressInput.Valid.ToCreateRequest();

        // Act
        await _service.CreateAsync(request, CancellationToken.None);

        // Assert
        Address address = GetAddedAddress();
        address.Id.Version.Should().Be(7);
        address.CreatedAt.Should().Be(_startTime);
        address.UpdatedAt.Should().Be(_startTime);
        address.NormalizedCity.Should().Be("city:Köln");
        address.NormalizedPostalCode.Should().Be("postal:51147");
    }

    [Test]
    public async Task CreateAsync_ValidRequest_ReturnsResponseOfAddedAddress()
    {
        // Arrange
        CreateAddressRequest request = AddressInput.Valid.ToCreateRequest();

        // Act
        AddressResponse response = await _service.CreateAsync(request, CancellationToken.None);

        // Assert
        response.Should().Be(GetAddedAddress().ToResponse());
    }

    [Test]
    public async Task CreateAsync_RequestWithoutEmail_ReturnsResponseWithoutEmail()
    {
        // Arrange
        CreateAddressRequest request = (AddressInput.Valid with { Email = null }).ToCreateRequest();

        // Act
        AddressResponse response = await _service.CreateAsync(request, CancellationToken.None);

        // Assert
        response.Email.Should().BeNull();
    }

    [Test]
    public async Task CreateAsync_ValidRequest_PassesCancellationToken()
    {
        // Arrange
        CreateAddressRequest request = AddressInput.Valid.ToCreateRequest();
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _service.CreateAsync(request, cancellationTokenSource.Token);

        // Assert
        await _addressRepository.Received(1).AddAsync(Arg.Any<Address>(), cancellationTokenSource.Token);
    }

    [Test]
    public async Task CreateAsync_ValidRequest_LogsCreatedAddress()
    {
        // Arrange
        CreateAddressRequest request = AddressInput.Valid.ToCreateRequest();

        // Act
        AddressResponse response = await _service.CreateAsync(request, CancellationToken.None);

        // Assert
        ShouldHaveLoggedSingle(LogLevel.Information, 1, $"Created address {response.Id}", response.Id);
    }

    [Test]
    public async Task CreateAsync_SavingCanceled_ThrowsOperationCanceledExceptionAndLogsNothing()
    {
        // Arrange
        CreateAddressRequest request = AddressInput.Valid.ToCreateRequest();
        _addressRepository.AddAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        Func<Task> act = () => _service.CreateAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _logger.Collector.Count.Should().Be(0);
    }

    [Test]
    public async Task CreateAsync_InvalidRequest_ThrowsArgumentExceptionAndAddsNothing()
    {
        // Arrange
        CreateAddressRequest request = (AddressInput.Valid with { FirstName = " " }).ToCreateRequest();

        // Act
        Func<Task> act = () => _service.CreateAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("firstName");
        await _addressRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        _logger.Collector.Count.Should().Be(0);
    }

    [Test]
    public async Task CreateAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        CreateAddressRequest request = null!;

        // Act
        Func<Task> act = () => _service.CreateAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
        await _addressRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    /// <summary>
    /// Returns the address passed to the only call of <see cref="IAddressRepository.AddAsync"/>.
    /// </summary>
    private Address GetAddedAddress() =>
        _addressRepository.ReceivedCalls()
            .Should().ContainSingle(call => call.GetMethodInfo().Name == nameof(IAddressRepository.AddAsync))
            .Which.GetArguments()[0].Should().BeOfType<Address>()
            .Subject;

    private void ShouldHaveLoggedSingle(LogLevel level, int eventId, string message, Guid addressId)
    {
        FakeLogRecord record = _logger.Collector.GetSnapshot().Should().ContainSingle().Which;
        record.Level.Should().Be(level);
        record.Id.Id.Should().Be(eventId);
        record.Message.Should().Be(message);
        record.GetStructuredStateValue("AddressId").Should().Be(addressId.ToString());
    }
}
