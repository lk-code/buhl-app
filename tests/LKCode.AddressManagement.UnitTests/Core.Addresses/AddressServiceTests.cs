using System.Globalization;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses;
using LKCode.AddressManagement.Core.Addresses.Contracts;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Mappings;
using LKCode.AddressManagement.Core.Addresses.Results;
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

    [Test]
    public async Task GetByIdAsync_KnownId_ReturnsResponse()
    {
        // Arrange
        Address address = CreateStoredAddress();

        // Act
        AddressResponse? response = await _service.GetByIdAsync(address.Id, CancellationToken.None);

        // Assert
        response.Should().Be(address.ToResponse());
    }

    [Test]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        // Arrange
        var id = Guid.CreateVersion7();

        // Act
        AddressResponse? response = await _service.GetByIdAsync(id, CancellationToken.None);

        // Assert
        response.Should().BeNull();
    }

    [Test]
    public async Task GetByIdAsync_AnyId_PassesIdAndCancellationToken()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _service.GetByIdAsync(id, cancellationTokenSource.Token);

        // Assert
        await _addressRepository.Received(1).GetByIdAsync(id, cancellationTokenSource.Token);
    }

    [Test]
    public async Task GetByIdAsync_KnownId_LogsLoadedAddress()
    {
        // Arrange
        Address address = CreateStoredAddress();

        // Act
        await _service.GetByIdAsync(address.Id, CancellationToken.None);

        // Assert
        ShouldHaveLoggedSingle(LogLevel.Debug, 2, $"Loaded address {address.Id}", address.Id);
    }

    [Test]
    public async Task GetByIdAsync_UnknownId_LogsNotFound()
    {
        // Arrange
        var id = Guid.CreateVersion7();

        // Act
        await _service.GetByIdAsync(id, CancellationToken.None);

        // Assert
        ShouldHaveLoggedSingle(LogLevel.Debug, 3, $"Address {id} was not found", id);
    }

    [Test]
    public async Task GetListAsync_CityAndPostalCode_PassesNormalizedFilters()
    {
        // Arrange
        var query = new AddressListQuery("Köln", "51 147");

        // Act
        await _service.GetListAsync(query, CancellationToken.None);

        // Assert
        await _addressRepository.Received(1).ListAsync("city:Köln", "postal:51 147", Arg.Any<CancellationToken>());
    }

    [TestCase("Köln", null, "city:Köln", null)]
    [TestCase(null, "51147", null, "postal:51147")]
    public async Task GetListAsync_SingleFilter_PassesOnlyThisFilterNormalized(
        string? city,
        string? postalCode,
        string? expectedCity,
        string? expectedPostalCode)
    {
        // Arrange
        var query = new AddressListQuery(city, postalCode);

        // Act
        await _service.GetListAsync(query, CancellationToken.None);

        // Assert
        await _addressRepository.Received(1)
            .ListAsync(expectedCity, expectedPostalCode, Arg.Any<CancellationToken>());
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t\n")]
    public async Task GetListAsync_BlankFilters_PassesNullWithoutNormalizing(string? filter)
    {
        // Arrange
        var query = new AddressListQuery(filter, filter);

        // Act
        await _service.GetListAsync(query, CancellationToken.None);

        // Assert
        await _addressRepository.Received(1).ListAsync(null, null, Arg.Any<CancellationToken>());
        _textNormalizer.DidNotReceiveWithAnyArgs().Normalize(default!);
        _textNormalizer.DidNotReceiveWithAnyArgs().NormalizePostalCode(default!);
    }

    [Test]
    public async Task GetListAsync_MatchingAddresses_ReturnsResponsesInRepositoryOrder()
    {
        // Arrange
        Address first = CreateStoredAddress(AddressInput.Changed);
        Address second = CreateStoredAddress(AddressInput.Valid);
        _addressRepository.ListAsync(default, default, default).ReturnsForAnyArgs([first, second]);

        // Act
        IReadOnlyList<AddressResponse> responses =
            await _service.GetListAsync(new AddressListQuery(null, null), CancellationToken.None);

        // Assert
        responses.Should().Equal(first.ToResponse(), second.ToResponse());
    }

    [Test]
    public async Task GetListAsync_NoMatches_ReturnsEmptyList()
    {
        // Arrange
        _addressRepository.ListAsync(default, default, default).ReturnsForAnyArgs([]);

        // Act
        IReadOnlyList<AddressResponse> responses =
            await _service.GetListAsync(new AddressListQuery("Köln", null), CancellationToken.None);

        // Assert
        responses.Should().BeEmpty();
    }

    [Test]
    public async Task GetListAsync_AnyQuery_PassesCancellationToken()
    {
        // Arrange
        var query = new AddressListQuery("Köln", "51147");
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _service.GetListAsync(query, cancellationTokenSource.Token);

        // Assert
        await _addressRepository.Received(1)
            .ListAsync(Arg.Any<string?>(), Arg.Any<string?>(), cancellationTokenSource.Token);
    }

    [Test]
    public async Task GetListAsync_MatchingAddresses_LogsCountAndNormalizedFilters()
    {
        // Arrange
        Address first = CreateStoredAddress(AddressInput.Valid);
        Address second = CreateStoredAddress(AddressInput.Valid);
        _addressRepository.ListAsync(default, default, default).ReturnsForAnyArgs([first, second]);

        // Act
        await _service.GetListAsync(new AddressListQuery("Köln", "51147"), CancellationToken.None);

        // Assert
        FakeLogRecord record = ShouldHaveLoggedSingle(
            LogLevel.Debug,
            4,
            "Listed 2 addresses for city city:Köln and postal code postal:51147");
        record.GetStructuredStateValue("Count").Should().Be("2");
        record.GetStructuredStateValue("NormalizedCity").Should().Be("city:Köln");
        record.GetStructuredStateValue("NormalizedPostalCode").Should().Be("postal:51147");
    }

    [Test]
    public async Task GetListAsync_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        AddressListQuery query = null!;

        // Act
        Func<Task> act = () => _service.GetListAsync(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("query");
        await _addressRepository.DidNotReceiveWithAnyArgs().ListAsync(default, default, default);
    }

    [Test]
    public async Task UpdateAsync_KnownId_ChangesAllValuesAndSavesAddress()
    {
        // Arrange
        Address address = CreateStoredAddress();
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        await _service.UpdateAsync(address.Id, expectedVersion: 1, request, CancellationToken.None);

        // Assert
        AddressInput.From(address).Should().Be(AddressInput.Changed);
        address.Version.Should().Be(2);
        await _addressRepository.Received(1).UpdateAsync(address, Arg.Any<long?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_KnownId_KeepsIdAndCreatedAtAndUpdatesUpdatedAtAndNormalizedFields()
    {
        // Arrange
        Address address = CreateStoredAddress();
        Guid id = address.Id;
        _timeProvider.GetUtcNow().Returns(_startTime.AddHours(2));
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        await _service.UpdateAsync(id, expectedVersion: 1, request, CancellationToken.None);

        // Assert
        address.Id.Should().Be(id);
        address.CreatedAt.Should().Be(_startTime);
        address.UpdatedAt.Should().Be(_startTime.AddHours(2));
        address.NormalizedCity.Should().Be("city:London");
        address.NormalizedPostalCode.Should().Be("postal:SW1A 1AA");
    }

    [TestCase(1L)]
    [TestCase(7L)]
    [TestCase(null)]
    public async Task UpdateAsync_KnownId_PassesExpectedVersionToRepository(long? expectedVersion)
    {
        // Arrange
        Address address = CreateStoredAddress();
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        await _service.UpdateAsync(address.Id, expectedVersion, request, CancellationToken.None);

        // Assert
        await _addressRepository.Received(1).UpdateAsync(address, expectedVersion, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_Saved_ReturnsSuccessWithResponseOfUpdatedAddress()
    {
        // Arrange
        Address address = CreateStoredAddress();
        _timeProvider.GetUtcNow().Returns(_startTime.AddHours(2));
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        AddressChangeResult result =
            await _service.UpdateAsync(address.Id, expectedVersion: 1, request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ChangeStatus.Success);
        result.Address.Should().Be(new AddressResponse(
            address.Id,
            "Max",
            "Musterfrau",
            "Hauptstraße",
            "4a",
            "SW1A 1AA",
            "London",
            "GB",
            "max.musterfrau@example.org",
            _startTime,
            _startTime.AddHours(2),
            2));
    }

    [Test]
    public async Task UpdateAsync_RequestWithoutEmail_RemovesEmail()
    {
        // Arrange
        Address address = CreateStoredAddress();
        UpdateAddressRequest request = (AddressInput.Changed with { Email = null }).ToUpdateRequest();

        // Act
        AddressChangeResult result =
            await _service.UpdateAsync(address.Id, expectedVersion: 1, request, CancellationToken.None);

        // Assert
        address.Email.Should().BeNull();
        result.Address!.Email.Should().BeNull();
    }

    [Test]
    public async Task UpdateAsync_UnknownId_ReturnsNotFoundAndSavesNothing()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        AddressChangeResult result =
            await _service.UpdateAsync(id, expectedVersion: 1, request, CancellationToken.None);

        // Assert
        result.Should().Be(AddressChangeResult.NotFound);
        await _addressRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default, default);
    }

    [Test]
    public async Task UpdateAsync_RepositoryReportsNotFound_ReturnsNotFound()
    {
        // Arrange
        Address address = CreateStoredAddress();
        ReturnOnUpdate(address, ChangeStatus.NotFound);
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        AddressChangeResult result =
            await _service.UpdateAsync(address.Id, expectedVersion: 1, request, CancellationToken.None);

        // Assert
        result.Should().Be(AddressChangeResult.NotFound);
    }

    [Test]
    public async Task UpdateAsync_RepositoryReportsVersionMismatch_ReturnsVersionMismatch()
    {
        // Arrange
        Address address = CreateStoredAddress();
        ReturnOnUpdate(address, ChangeStatus.VersionMismatch);
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        AddressChangeResult result =
            await _service.UpdateAsync(address.Id, expectedVersion: 1, request, CancellationToken.None);

        // Assert
        result.Should().Be(AddressChangeResult.VersionMismatch);
    }

    [Test]
    public async Task UpdateAsync_KnownId_PassesIdAndCancellationToken()
    {
        // Arrange
        Address address = CreateStoredAddress();
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _service.UpdateAsync(address.Id, expectedVersion: 1, request, cancellationTokenSource.Token);

        // Assert
        await _addressRepository.Received(1).GetByIdAsync(address.Id, cancellationTokenSource.Token);
        await _addressRepository.Received(1).UpdateAsync(address, Arg.Any<long?>(), cancellationTokenSource.Token);
    }

    [Test]
    public async Task UpdateAsync_Saved_LogsUpdatedAddress()
    {
        // Arrange
        Address address = CreateStoredAddress();
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        await _service.UpdateAsync(address.Id, expectedVersion: 1, request, CancellationToken.None);

        // Assert
        ShouldHaveLoggedSingle(LogLevel.Information, 5, $"Updated address {address.Id}", address.Id);
    }

    [Test]
    public async Task UpdateAsync_UnknownId_LogsNotFound()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        await _service.UpdateAsync(id, expectedVersion: 1, request, CancellationToken.None);

        // Assert
        ShouldHaveLoggedSingle(LogLevel.Debug, 3, $"Address {id} was not found", id);
    }

    [Test]
    public async Task UpdateAsync_RepositoryReportsNotFound_LogsNotFound()
    {
        // Arrange
        Address address = CreateStoredAddress();
        ReturnOnUpdate(address, ChangeStatus.NotFound);
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        await _service.UpdateAsync(address.Id, expectedVersion: 1, request, CancellationToken.None);

        // Assert
        ShouldHaveLoggedSingle(LogLevel.Debug, 3, $"Address {address.Id} was not found", address.Id);
    }

    [TestCase(1L, "1")]
    [TestCase(null, "(null)")]
    public async Task UpdateAsync_RepositoryReportsVersionMismatch_LogsVersionMismatch(
        long? expectedVersion,
        string loggedVersion)
    {
        // Arrange
        Address address = CreateStoredAddress();
        ReturnOnUpdate(address, ChangeStatus.VersionMismatch);
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        await _service.UpdateAsync(address.Id, expectedVersion, request, CancellationToken.None);

        // Assert
        FakeLogRecord record = ShouldHaveLoggedSingle(
            LogLevel.Information,
            7,
            $"Address {address.Id} was not saved: its version differs from the expected version {loggedVersion}");
        record.GetStructuredStateValue("AddressId").Should().Be(address.Id.ToString());
        record.GetStructuredStateValue("ExpectedVersion")
            .Should().Be(expectedVersion?.ToString(CultureInfo.InvariantCulture));
    }

    [Test]
    public async Task UpdateAsync_SavingCanceled_ThrowsOperationCanceledExceptionAndLogsNothing()
    {
        // Arrange
        Address address = CreateStoredAddress();
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();
        _addressRepository.UpdateAsync(address, Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        Func<Task> act = () => _service.UpdateAsync(address.Id, expectedVersion: 1, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _logger.Collector.Count.Should().Be(0);
    }

    [Test]
    public async Task UpdateAsync_InvalidRequest_ThrowsArgumentExceptionAndLeavesAddressUnsaved()
    {
        // Arrange
        Address address = CreateStoredAddress();
        UpdateAddressRequest request = (AddressInput.Changed with { City = "" }).ToUpdateRequest();

        // Act
        Func<Task> act = () => _service.UpdateAsync(address.Id, expectedVersion: 1, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("city");
        AddressInput.From(address).Should().Be(AddressInput.Valid);
        await _addressRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default, default);
        _logger.Collector.Count.Should().Be(0);
    }

    [Test]
    public async Task UpdateAsync_NullRequest_ThrowsArgumentNullExceptionWithoutLoading()
    {
        // Arrange
        Address address = CreateStoredAddress();
        UpdateAddressRequest request = null!;

        // Act
        Func<Task> act = () => _service.UpdateAsync(address.Id, expectedVersion: 1, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
        await _addressRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Test]
    public async Task UpdateAsync_AfterDeletion_ReturnsNotFoundAndSavesNothing()
    {
        // Arrange
        Address address = CreateStoredAddress();
        ReturnNullAfterFirstLoad(address);
        await _service.DeleteAsync(address.Id, expectedVersion: 1, CancellationToken.None);
        _addressRepository.ClearReceivedCalls();
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        AddressChangeResult result =
            await _service.UpdateAsync(address.Id, expectedVersion: 2, request, CancellationToken.None);

        // Assert
        result.Should().Be(AddressChangeResult.NotFound);
        await _addressRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default, default);
    }

    [Test]
    public async Task UpdateAsync_RepositoryReturnsDeletedAddress_ThrowsInvalidOperationExceptionAndSavesNothing()
    {
        // Arrange
        Address address = CreateStoredAddress();
        address.Delete(_timeProvider);
        UpdateAddressRequest request = AddressInput.Changed.ToUpdateRequest();

        // Act
        Func<Task> act = () => _service.UpdateAsync(address.Id, expectedVersion: 2, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        AddressInput.From(address).Should().Be(AddressInput.Valid);
        await _addressRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default, default);
    }

    [Test]
    public async Task DeleteAsync_KnownId_MarksAddressAsDeletedSavesItAndReturnsSuccess()
    {
        // Arrange
        Address address = CreateStoredAddress();
        bool? deletedWhenSaved = null;
        _addressRepository.UpdateAsync(address, Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                deletedWhenSaved = call.Arg<Address>().IsDeleted;
                return ChangeStatus.Success;
            });

        // Act
        ChangeStatus status = await _service.DeleteAsync(address.Id, expectedVersion: 1, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.Success);
        deletedWhenSaved.Should().BeTrue();
        address.IsDeleted.Should().BeTrue();
        address.DeletedAt.Should().Be(_startTime);
        address.Version.Should().Be(2);
        AddressInput.From(address).Should().Be(AddressInput.Valid);
        await _addressRepository.Received(1).UpdateAsync(address, Arg.Any<long?>(), Arg.Any<CancellationToken>());
    }

    [TestCase(1L)]
    [TestCase(7L)]
    [TestCase(null)]
    public async Task DeleteAsync_KnownId_PassesExpectedVersionToRepository(long? expectedVersion)
    {
        // Arrange
        Address address = CreateStoredAddress();

        // Act
        await _service.DeleteAsync(address.Id, expectedVersion, CancellationToken.None);

        // Assert
        await _addressRepository.Received(1).UpdateAsync(address, expectedVersion, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteAsync_UnknownId_ReturnsNotFoundAndSavesNothing()
    {
        // Arrange
        var id = Guid.CreateVersion7();

        // Act
        ChangeStatus status = await _service.DeleteAsync(id, expectedVersion: 1, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.NotFound);
        await _addressRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default, default);
    }

    [TestCase(1L)]
    [TestCase(2L)]
    [TestCase(null)]
    public async Task DeleteAsync_CalledAgainAfterDeletion_ReturnsNotFoundAndSavesOnlyOnce(long? expectedVersion)
    {
        // Arrange
        Address address = CreateStoredAddress();
        ReturnNullAfterFirstLoad(address);
        ChangeStatus firstStatus =
            await _service.DeleteAsync(address.Id, expectedVersion: 1, CancellationToken.None);

        // Act
        ChangeStatus status = await _service.DeleteAsync(address.Id, expectedVersion, CancellationToken.None);

        // Assert
        firstStatus.Should().Be(ChangeStatus.Success);
        status.Should().Be(ChangeStatus.NotFound);
        address.Version.Should().Be(2);
        await _addressRepository.Received(1).UpdateAsync(address, Arg.Any<long?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteAsync_RepositoryReturnsDeletedAddress_ThrowsInvalidOperationExceptionAndSavesNothing()
    {
        // Arrange
        Address address = CreateStoredAddress();
        address.Delete(_timeProvider);

        // Act
        Func<Task> act = () => _service.DeleteAsync(address.Id, expectedVersion: 2, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        address.Version.Should().Be(2);
        await _addressRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default, default);
    }

    [TestCase(ChangeStatus.NotFound)]
    [TestCase(ChangeStatus.VersionMismatch)]
    public async Task DeleteAsync_RepositoryReportsFailure_ReturnsThisStatus(ChangeStatus repositoryStatus)
    {
        // Arrange
        Address address = CreateStoredAddress();
        ReturnOnUpdate(address, repositoryStatus);

        // Act
        ChangeStatus status = await _service.DeleteAsync(address.Id, expectedVersion: 1, CancellationToken.None);

        // Assert
        status.Should().Be(repositoryStatus);
    }

    [Test]
    public async Task DeleteAsync_KnownId_PassesIdAndCancellationToken()
    {
        // Arrange
        Address address = CreateStoredAddress();
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await _service.DeleteAsync(address.Id, expectedVersion: 1, cancellationTokenSource.Token);

        // Assert
        await _addressRepository.Received(1).GetByIdAsync(address.Id, cancellationTokenSource.Token);
        await _addressRepository.Received(1).UpdateAsync(address, Arg.Any<long?>(), cancellationTokenSource.Token);
    }

    [Test]
    public async Task DeleteAsync_Deleted_LogsDeletedAddress()
    {
        // Arrange
        Address address = CreateStoredAddress();

        // Act
        await _service.DeleteAsync(address.Id, expectedVersion: 1, CancellationToken.None);

        // Assert
        ShouldHaveLoggedSingle(LogLevel.Information, 6, $"Deleted address {address.Id}", address.Id);
    }

    [Test]
    public async Task DeleteAsync_UnknownId_LogsNotFound()
    {
        // Arrange
        var id = Guid.CreateVersion7();

        // Act
        await _service.DeleteAsync(id, expectedVersion: 1, CancellationToken.None);

        // Assert
        ShouldHaveLoggedSingle(LogLevel.Debug, 3, $"Address {id} was not found", id);
    }

    [Test]
    public async Task DeleteAsync_RepositoryReportsNotFound_LogsNotFound()
    {
        // Arrange
        Address address = CreateStoredAddress();
        ReturnOnUpdate(address, ChangeStatus.NotFound);

        // Act
        await _service.DeleteAsync(address.Id, expectedVersion: 1, CancellationToken.None);

        // Assert
        ShouldHaveLoggedSingle(LogLevel.Debug, 3, $"Address {address.Id} was not found", address.Id);
    }

    [TestCase(1L, "1")]
    [TestCase(null, "(null)")]
    public async Task DeleteAsync_RepositoryReportsVersionMismatch_LogsVersionMismatch(
        long? expectedVersion,
        string loggedVersion)
    {
        // Arrange
        Address address = CreateStoredAddress();
        ReturnOnUpdate(address, ChangeStatus.VersionMismatch);

        // Act
        await _service.DeleteAsync(address.Id, expectedVersion, CancellationToken.None);

        // Assert
        FakeLogRecord record = ShouldHaveLoggedSingle(
            LogLevel.Information,
            7,
            $"Address {address.Id} was not saved: its version differs from the expected version {loggedVersion}");
        record.GetStructuredStateValue("AddressId").Should().Be(address.Id.ToString());
        record.GetStructuredStateValue("ExpectedVersion")
            .Should().Be(expectedVersion?.ToString(CultureInfo.InvariantCulture));
    }

    [Test]
    public async Task DeleteAsync_DeletingCanceled_ThrowsOperationCanceledExceptionAndLogsNothing()
    {
        // Arrange
        Address address = CreateStoredAddress();
        _addressRepository.UpdateAsync(address, Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        Func<Task> act = () => _service.DeleteAsync(address.Id, expectedVersion: 1, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _logger.Collector.Count.Should().Be(0);
    }

    private void ReturnOnUpdate(Address address, ChangeStatus status) =>
        _addressRepository.UpdateAsync(address, Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(status);

    /// <summary>
    /// Lets the repository return the <paramref name="address"/> only once and <see langword="null"/> afterwards, like
    /// the real repository after the address was deleted.
    /// </summary>
    private void ReturnNullAfterFirstLoad(Address address) =>
        _addressRepository.GetByIdAsync(address.Id, Arg.Any<CancellationToken>()).Returns(address, [null]);

    /// <summary>
    /// Creates an address with the given values and lets the repository return it for its id. Unknown ids return
    /// <see langword="null"/>.
    /// </summary>
    private Address CreateStoredAddress(AddressInput? input = null)
    {
        input ??= AddressInput.Valid;
        var address = Address.Create(
            input.FirstName,
            input.LastName,
            input.Street,
            input.HouseNumber,
            input.PostalCode,
            input.City,
            input.Country,
            input.Email,
            _textNormalizer,
            _timeProvider);
        _addressRepository.GetByIdAsync(address.Id, Arg.Any<CancellationToken>()).Returns(address);

        return address;
    }

    /// <summary>
    /// Returns the address passed to the only call of <see cref="IAddressRepository.AddAsync"/>.
    /// </summary>
    private Address GetAddedAddress() =>
        _addressRepository.ReceivedCalls()
            .Should().ContainSingle(call => call.GetMethodInfo().Name == nameof(IAddressRepository.AddAsync))
            .Which.GetArguments()[0].Should().BeOfType<Address>()
            .Subject;

    private void ShouldHaveLoggedSingle(LogLevel level, int eventId, string message, Guid addressId) =>
        ShouldHaveLoggedSingle(level, eventId, message)
            .GetStructuredStateValue("AddressId").Should().Be(addressId.ToString());

    private FakeLogRecord ShouldHaveLoggedSingle(LogLevel level, int eventId, string message)
    {
        FakeLogRecord record = _logger.Collector.GetSnapshot().Should().ContainSingle().Which;
        record.Level.Should().Be(level);
        record.Id.Id.Should().Be(eventId);
        record.Message.Should().Be(message);

        return record;
    }
}
