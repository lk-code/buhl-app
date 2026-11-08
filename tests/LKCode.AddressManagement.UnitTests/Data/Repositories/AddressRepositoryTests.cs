using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Results;
using LKCode.AddressManagement.Data;
using LKCode.AddressManagement.Data.Repositories;
using LKCode.AddressManagement.Domain.Addresses;
using LKCode.AddressManagement.Domain.Normalizers;
using LKCode.AddressManagement.UnitTests.Domain.Addresses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace LKCode.AddressManagement.UnitTests.Data.Repositories;

/// <summary>
/// Tests against the EF Core InMemory provider. Every test uses its own database, and arrange, act and assert use
/// separate contexts, so that the change tracker cannot hide missing database operations.
/// </summary>
public class AddressRepositoryTests
{
    private static readonly DateTimeOffset _startTime = new(2026, 3, 14, 9, 26, 53, 589, TimeSpan.Zero);

    private readonly TextNormalizer _textNormalizer = new();
    private DbContextOptions<AddressDbContext> _options = null!;
    private FakeTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<AddressDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _timeProvider = new FakeTimeProvider(_startTime);
    }

    [Test]
    public void Constructor_NullDbContext_ThrowsArgumentNullException()
    {
        // Arrange
        AddressDbContext dbContext = null!;

        // Act
        Action act = () => _ = new AddressRepository(dbContext);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("dbContext");
    }

    [Test]
    public async Task AddAsync_NewAddress_PersistsAllFields()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        await repository.AddAsync(address, CancellationToken.None);

        // Assert
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().ContainSingle().Which.Should().BeEquivalentTo(address);
    }

    [Test]
    public async Task AddAsync_TwoAddresses_PersistsBoth()
    {
        // Arrange
        Address first = CreateAddress(AddressInput.Valid);
        Address second = CreateAddress(AddressInput.Changed);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        await repository.AddAsync(first, CancellationToken.None);
        await repository.AddAsync(second, CancellationToken.None);

        // Assert
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().BeEquivalentTo([first, second]);
    }

    [Test]
    public async Task AddAsync_NullAddress_ThrowsArgumentNullException()
    {
        // Arrange
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        Func<Task> act = () => repository.AddAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("address");
    }

    [Test]
    public async Task AddAsync_CanceledToken_ThrowsOperationCanceledExceptionAndSavesNothing()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        Func<Task> act = () => repository.AddAsync(address, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().BeEmpty();
    }

    [Test]
    public async Task GetByIdAsync_ExistingId_ReturnsAddressWithAllFields()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        Address other = CreateAddress(AddressInput.Changed);
        await SeedAsync(address, other);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        Address? result = await repository.GetByIdAsync(address.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeSameAs(address);
        result.Should().BeEquivalentTo(address);
    }

    [Test]
    public async Task GetByIdAsync_ExistingId_ReturnsTrackedAddress()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        await SeedAsync(address);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        Address? result = await repository.GetByIdAsync(address.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        context.Entry(result!).State.Should().Be(EntityState.Unchanged);
    }

    [Test]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        // Arrange
        await SeedAsync(CreateAddress(AddressInput.Valid));
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        Address? result = await repository.GetByIdAsync(Guid.CreateVersion7(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task GetByIdAsync_EmptyGuid_ReturnsNull()
    {
        // Arrange
        await SeedAsync(CreateAddress(AddressInput.Valid));
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        Address? result = await repository.GetByIdAsync(Guid.Empty, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task GetByIdAsync_DeletedAddress_ReturnsNull()
    {
        // Arrange
        Address deleted = CreateDeletedAddress(AddressInput.Valid);
        await SeedAsync(deleted, CreateAddress(AddressInput.Valid));
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        Address? result = await repository.GetByIdAsync(deleted.Id, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task GetByIdAsync_EmptyDatabase_ReturnsNull()
    {
        // Arrange
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        Address? result = await repository.GetByIdAsync(Guid.CreateVersion7(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task GetByIdAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        await SeedAsync(address);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        Func<Task> act = () => repository.GetByIdAsync(address.Id, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestCase(1L)]
    [TestCase(null)]
    public async Task UpdateAsync_TrackedAddressChanged_PersistsAllChangesAndReturnsSuccess(long? expectedVersion)
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        await SeedAsync(original);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        Address? address = await repository.GetByIdAsync(original.Id, CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        UpdateAddress(address!, AddressInput.Changed);

        // Act
        ChangeStatus status = await repository.UpdateAsync(address!, expectedVersion, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.Success);
        IReadOnlyList<Address> stored = await LoadAllAsync();
        Address storedAddress = stored.Should().ContainSingle().Subject;
        AddressInput.From(storedAddress).Should().Be(AddressInput.Changed);
        storedAddress.Id.Should().Be(original.Id);
        storedAddress.NormalizedCity.Should().Be("london");
        storedAddress.NormalizedPostalCode.Should().Be("SW1A1AA");
        storedAddress.CreatedAt.Should().Be(_startTime);
        storedAddress.UpdatedAt.Should().Be(_startTime.AddMinutes(5));
        storedAddress.Version.Should().Be(2);
    }

    [Test]
    public async Task UpdateAsync_DetachedAddressWithExpectedVersion_PersistsAllChangesAndReturnsSuccess()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        await SeedAsync(address);
        UpdateAddress(address, AddressInput.Changed);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        ChangeStatus status = await repository.UpdateAsync(address, expectedVersion: 1, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.Success);
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().ContainSingle().Which.Should().BeEquivalentTo(address);
    }

    [Test]
    public async Task UpdateAsync_DetachedAddressWithoutExpectedVersion_ReturnsVersionMismatchAndSavesNothing()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        await SeedAsync(address);
        UpdateAddress(address, AddressInput.Changed);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        ChangeStatus status = await repository.UpdateAsync(address, expectedVersion: null, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.VersionMismatch);
        await AssertStoredUnchangedAsync(AddressInput.Valid, expectedVersion: 1);
    }

    [TestCase(0L)]
    [TestCase(2L)]
    [TestCase(3L)]
    [TestCase(long.MaxValue)]
    public async Task UpdateAsync_ExpectedVersionNotStored_ReturnsVersionMismatchAndSavesNothing(long expectedVersion)
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        await SeedAsync(original);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        Address? address = await repository.GetByIdAsync(original.Id, CancellationToken.None);
        UpdateAddress(address!, AddressInput.Changed);

        // Act
        ChangeStatus status = await repository.UpdateAsync(address!, expectedVersion, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.VersionMismatch);
        await AssertStoredUnchangedAsync(AddressInput.Valid, expectedVersion: 1);
    }

    [TestCase(1L)]
    [TestCase(null)]
    public async Task UpdateAsync_AddressChangedInOtherContext_ReturnsVersionMismatchAndKeepsOtherChange(
        long? expectedVersion)
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        await SeedAsync(original);
        await using AddressDbContext context = CreateContext();
        await using AddressDbContext otherContext = CreateContext();
        var repository = new AddressRepository(context);
        var otherRepository = new AddressRepository(otherContext);
        Address? address = await repository.GetByIdAsync(original.Id, CancellationToken.None);
        Address? otherAddress = await otherRepository.GetByIdAsync(original.Id, CancellationToken.None);
        UpdateAddress(otherAddress!, AddressInput.Changed);
        ChangeStatus otherStatus =
            await otherRepository.UpdateAsync(otherAddress!, expectedVersion: 1, CancellationToken.None);
        UpdateAddress(address!, AddressInput.Valid with { FirstName = "Erika-Maria" });

        // Act
        ChangeStatus status = await repository.UpdateAsync(address!, expectedVersion, CancellationToken.None);

        // Assert
        otherStatus.Should().Be(ChangeStatus.Success);
        status.Should().Be(ChangeStatus.VersionMismatch);
        await AssertStoredUnchangedAsync(AddressInput.Changed, expectedVersion: 2);
    }

    [TestCase(1L)]
    [TestCase(null)]
    public async Task UpdateAsync_AddressDeletedInOtherContext_ReturnsNotFoundAndKeepsDeletion(long? expectedVersion)
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        await SeedAsync(original);
        await using AddressDbContext context = CreateContext();
        await using AddressDbContext otherContext = CreateContext();
        var repository = new AddressRepository(context);
        var otherRepository = new AddressRepository(otherContext);
        Address? address = await repository.GetByIdAsync(original.Id, CancellationToken.None);
        Address? otherAddress = await otherRepository.GetByIdAsync(original.Id, CancellationToken.None);
        otherAddress!.Delete(_timeProvider);
        ChangeStatus otherStatus =
            await otherRepository.UpdateAsync(otherAddress, expectedVersion: 1, CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        UpdateAddress(address!, AddressInput.Changed);

        // Act
        ChangeStatus status = await repository.UpdateAsync(address!, expectedVersion, CancellationToken.None);

        // Assert
        otherStatus.Should().Be(ChangeStatus.Success);
        status.Should().Be(ChangeStatus.NotFound);
        IReadOnlyList<Address> stored = await LoadAllIncludingDeletedAsync();
        stored.Should().ContainSingle().Which.Should().BeEquivalentTo(otherAddress);
    }

    [TestCase(1L)]
    [TestCase(null)]
    public async Task UpdateAsync_AddressDeletedAgainInOtherContext_ReturnsNotFoundAndKeepsFirstDeletion(
        long? expectedVersion)
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        await SeedAsync(original);
        await using AddressDbContext context = CreateContext();
        await using AddressDbContext otherContext = CreateContext();
        var repository = new AddressRepository(context);
        var otherRepository = new AddressRepository(otherContext);
        Address? address = await repository.GetByIdAsync(original.Id, CancellationToken.None);
        Address? otherAddress = await otherRepository.GetByIdAsync(original.Id, CancellationToken.None);
        otherAddress!.Delete(_timeProvider);
        ChangeStatus otherStatus =
            await otherRepository.UpdateAsync(otherAddress, expectedVersion: 1, CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        address!.Delete(_timeProvider);

        // Act
        ChangeStatus status = await repository.UpdateAsync(address, expectedVersion, CancellationToken.None);

        // Assert
        otherStatus.Should().Be(ChangeStatus.Success);
        status.Should().Be(ChangeStatus.NotFound);
        IReadOnlyList<Address> stored = await LoadAllIncludingDeletedAsync();
        stored.Should().ContainSingle().Which.DeletedAt.Should().Be(_startTime);
    }

    [TestCase(1L)]
    [TestCase(null)]
    public async Task UpdateAsync_DeletedAddress_KeepsAddressMarkedAsDeletedAndReturnsSuccess(long? expectedVersion)
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        Address other = CreateAddress(AddressInput.Changed);
        await SeedAsync(original, other);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        Address? address = await repository.GetByIdAsync(original.Id, CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        address!.Delete(_timeProvider);

        // Act
        ChangeStatus status = await repository.UpdateAsync(address, expectedVersion, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.Success);
        IReadOnlyList<Address> stored = await LoadAllIncludingDeletedAsync();
        Address storedAddress = stored.Should().ContainSingle(candidate => candidate.Id == original.Id).Subject;
        AddressInput.From(storedAddress).Should().Be(AddressInput.Valid);
        storedAddress.IsDeleted.Should().BeTrue();
        storedAddress.DeletedAt.Should().Be(_startTime.AddMinutes(5));
        storedAddress.CreatedAt.Should().Be(_startTime);
        storedAddress.UpdatedAt.Should().Be(_startTime.AddMinutes(5));
        storedAddress.Version.Should().Be(2);
        stored.Should().ContainSingle(candidate => candidate.Id == other.Id).Which.Should().BeEquivalentTo(other);
    }

    [TestCase(0L)]
    [TestCase(2L)]
    [TestCase(long.MaxValue)]
    public async Task UpdateAsync_DeletedAddressWithExpectedVersionNotStored_ReturnsVersionMismatchAndKeepsAddress(
        long expectedVersion)
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        await SeedAsync(original);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        Address? address = await repository.GetByIdAsync(original.Id, CancellationToken.None);
        address!.Delete(_timeProvider);

        // Act
        ChangeStatus status = await repository.UpdateAsync(address, expectedVersion, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.VersionMismatch);
        await AssertStoredUnchangedAsync(AddressInput.Valid, expectedVersion: 1);
    }

    [TestCase(1L)]
    [TestCase(null)]
    public async Task UpdateAsync_DeletedAddressChangedInOtherContext_ReturnsVersionMismatchAndKeepsOtherChange(
        long? expectedVersion)
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        await SeedAsync(original);
        await using AddressDbContext context = CreateContext();
        await using AddressDbContext otherContext = CreateContext();
        var repository = new AddressRepository(context);
        var otherRepository = new AddressRepository(otherContext);
        Address? address = await repository.GetByIdAsync(original.Id, CancellationToken.None);
        Address? otherAddress = await otherRepository.GetByIdAsync(original.Id, CancellationToken.None);
        UpdateAddress(otherAddress!, AddressInput.Changed);
        await otherRepository.UpdateAsync(otherAddress!, expectedVersion: 1, CancellationToken.None);
        address!.Delete(_timeProvider);

        // Act
        ChangeStatus status = await repository.UpdateAsync(address, expectedVersion, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.VersionMismatch);
        await AssertStoredUnchangedAsync(AddressInput.Changed, expectedVersion: 2);
    }

    [Test]
    public async Task UpdateAsync_NeverSavedAddress_ReturnsNotFoundAndSavesNothing()
    {
        // Arrange
        await SeedAsync(CreateAddress(AddressInput.Valid));
        Address address = CreateAddress(AddressInput.Changed);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        ChangeStatus status = await repository.UpdateAsync(address, expectedVersion: 1, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.NotFound);
        await AssertStoredUnchangedAsync(AddressInput.Valid, expectedVersion: 1);
    }

    [TestCase(2L, ChangeStatus.VersionMismatch)]
    [TestCase(null, ChangeStatus.NotFound)]
    public async Task UpdateAsync_Failed_StopsTrackingAddress(long? expectedVersion, ChangeStatus expectedStatus)
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        await SeedAsync(original);
        Address address = expectedVersion is null ? CreateAddress(AddressInput.Changed) : original;
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        ChangeStatus status = await repository.UpdateAsync(address, expectedVersion, CancellationToken.None);

        // Assert
        status.Should().Be(expectedStatus);
        context.Entry(address).State.Should().Be(EntityState.Detached);
        (await context.SaveChangesAsync()).Should().Be(0);
    }

    [Test]
    public async Task UpdateAsync_ChangedAddress_LeavesOtherAddressesUnchanged()
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        Address other = CreateAddress(AddressInput.Valid);
        await SeedAsync(original, other);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        Address? address = await repository.GetByIdAsync(original.Id, CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        UpdateAddress(address!, AddressInput.Changed);

        // Act
        await repository.UpdateAsync(address!, expectedVersion: 1, CancellationToken.None);

        // Assert
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().ContainSingle(storedAddress => storedAddress.Id == other.Id)
            .Which.Should().BeEquivalentTo(other);
    }

    [Test]
    public async Task UpdateAsync_NullAddress_ThrowsArgumentNullException()
    {
        // Arrange
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        Func<Task> act = () => repository.UpdateAsync(null!, expectedVersion: 1, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("address");
    }

    [Test]
    public async Task UpdateAsync_CanceledToken_ThrowsOperationCanceledExceptionAndSavesNothing()
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        await SeedAsync(original);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        Address? address = await repository.GetByIdAsync(original.Id, CancellationToken.None);
        UpdateAddress(address!, AddressInput.Changed);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        Func<Task> act = () => repository.UpdateAsync(address!, expectedVersion: 1, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().ContainSingle().Which.Should().BeEquivalentTo(original);
    }

    private AddressDbContext CreateContext() => new(_options);

    private Address CreateAddress(AddressInput input) => Address.Create(
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

    private Address CreateDeletedAddress(AddressInput input)
    {
        Address address = CreateAddress(input);
        address.Delete(_timeProvider);

        return address;
    }

    private void UpdateAddress(Address address, AddressInput input) => address.Update(
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

    private async Task SeedAsync(params Address[] addresses)
    {
        await using AddressDbContext context = CreateContext();
        context.Addresses.AddRange(addresses);
        await context.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<Address>> LoadAllAsync()
    {
        await using AddressDbContext context = CreateContext();

        return await context.Addresses.AsNoTracking().ToListAsync();
    }

    private async Task<IReadOnlyList<Address>> LoadAllIncludingDeletedAsync()
    {
        await using AddressDbContext context = CreateContext();

        return await context.Addresses.AsNoTracking().IgnoreQueryFilters().ToListAsync();
    }

    /// <summary>
    /// Asserts that the only stored address has the values of the <paramref name="expectedInput"/> and the
    /// <paramref name="expectedVersion"/>.
    /// </summary>
    private async Task AssertStoredUnchangedAsync(AddressInput expectedInput, long expectedVersion)
    {
        Address stored = (await LoadAllAsync()).Should().ContainSingle().Subject;
        AddressInput.From(stored).Should().Be(expectedInput);
        stored.Version.Should().Be(expectedVersion);
    }
}
