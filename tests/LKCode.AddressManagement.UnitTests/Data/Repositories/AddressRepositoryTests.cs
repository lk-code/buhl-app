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

    [Test]
    public async Task UpdateAsync_AddressDeletedInOtherContext_ReturnsNotFound()
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
        await otherRepository.DeleteAsync(otherAddress!, expectedVersion: 1, CancellationToken.None);
        UpdateAddress(address!, AddressInput.Changed);

        // Act
        ChangeStatus status = await repository.UpdateAsync(address!, expectedVersion: 1, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.NotFound);
        (await LoadAllAsync()).Should().BeEmpty();
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

    [TestCase(1L)]
    [TestCase(null)]
    public async Task DeleteAsync_TrackedAddress_RemovesOnlyThisAddressAndReturnsSuccess(long? expectedVersion)
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        Address other = CreateAddress(AddressInput.Changed);
        await SeedAsync(original, other);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        Address? address = await repository.GetByIdAsync(original.Id, CancellationToken.None);

        // Act
        ChangeStatus status = await repository.DeleteAsync(address!, expectedVersion, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.Success);
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().ContainSingle().Which.Should().BeEquivalentTo(other);
    }

    [TestCase(1L)]
    [TestCase(null)]
    public async Task DeleteAsync_DetachedAddress_RemovesAddressAndReturnsSuccess(long? expectedVersion)
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        await SeedAsync(address);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        ChangeStatus status = await repository.DeleteAsync(address, expectedVersion, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.Success);
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().BeEmpty();
    }

    [TestCase(0L)]
    [TestCase(2L)]
    [TestCase(long.MaxValue)]
    public async Task DeleteAsync_ExpectedVersionNotStored_ReturnsVersionMismatchAndKeepsAddress(long expectedVersion)
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        await SeedAsync(original);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        Address? address = await repository.GetByIdAsync(original.Id, CancellationToken.None);

        // Act
        ChangeStatus status = await repository.DeleteAsync(address!, expectedVersion, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.VersionMismatch);
        await AssertStoredUnchangedAsync(AddressInput.Valid, expectedVersion: 1);
    }

    [TestCase(1L)]
    [TestCase(null)]
    public async Task DeleteAsync_AddressChangedInOtherContext_ReturnsVersionMismatchAndKeepsAddress(
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

        // Act
        ChangeStatus status = await repository.DeleteAsync(address!, expectedVersion, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.VersionMismatch);
        await AssertStoredUnchangedAsync(AddressInput.Changed, expectedVersion: 2);
    }

    [Test]
    public async Task DeleteAsync_AddressDeletedInOtherContext_ReturnsNotFound()
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
        ChangeStatus otherStatus =
            await otherRepository.DeleteAsync(otherAddress!, expectedVersion: 1, CancellationToken.None);

        // Act
        ChangeStatus status = await repository.DeleteAsync(address!, expectedVersion: 1, CancellationToken.None);

        // Assert
        otherStatus.Should().Be(ChangeStatus.Success);
        status.Should().Be(ChangeStatus.NotFound);
        (await LoadAllAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task DeleteAsync_NeverSavedAddress_ReturnsNotFoundAndKeepsOtherAddresses()
    {
        // Arrange
        Address existing = CreateAddress(AddressInput.Valid);
        await SeedAsync(existing);
        Address address = CreateAddress(AddressInput.Changed);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        ChangeStatus status = await repository.DeleteAsync(address, expectedVersion: 1, CancellationToken.None);

        // Assert
        status.Should().Be(ChangeStatus.NotFound);
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().ContainSingle().Which.Should().BeEquivalentTo(existing);
    }

    [TestCase(2L, ChangeStatus.VersionMismatch)]
    [TestCase(null, ChangeStatus.NotFound)]
    public async Task DeleteAsync_Failed_StopsTrackingAddress(long? expectedVersion, ChangeStatus expectedStatus)
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        await SeedAsync(original);
        Address address = expectedVersion is null ? CreateAddress(AddressInput.Changed) : original;
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        ChangeStatus status = await repository.DeleteAsync(address, expectedVersion, CancellationToken.None);

        // Assert
        status.Should().Be(expectedStatus);
        context.Entry(address).State.Should().Be(EntityState.Detached);
        (await context.SaveChangesAsync()).Should().Be(0);
    }

    [Test]
    public async Task DeleteAsync_DeletedAddress_CannotBeLoadedById()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        await SeedAsync(address);
        await using (AddressDbContext deleteContext = CreateContext())
        {
            await new AddressRepository(deleteContext).DeleteAsync(address, expectedVersion: 1, CancellationToken.None);
        }

        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        Address? result = await repository.GetByIdAsync(address.Id, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task DeleteAsync_NullAddress_ThrowsArgumentNullException()
    {
        // Arrange
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        Func<Task> act = () => repository.DeleteAsync(null!, expectedVersion: 1, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("address");
    }

    [Test]
    public async Task DeleteAsync_CanceledToken_ThrowsOperationCanceledExceptionAndKeepsAddress()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        await SeedAsync(address);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        Func<Task> act = () => repository.DeleteAsync(address, expectedVersion: 1, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().ContainSingle().Which.Should().BeEquivalentTo(address);
    }

    [Test]
    public async Task ListAsync_NoFilters_ReturnsAllAddresses()
    {
        // Arrange
        SeededAddresses seeded = await SeedListAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(null, null, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(seeded.All);
    }

    [Test]
    public async Task ListAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(null, null, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t\n")]
    public async Task ListAsync_BlankFilters_IgnoresFilters(string? blankFilter)
    {
        // Arrange
        SeededAddresses seeded = await SeedListAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(blankFilter, blankFilter, CancellationToken.None);

        // Assert
        result.Select(address => address.Id).Should().BeEquivalentTo(seeded.All.Select(address => address.Id));
    }

    [TestCase("Solingen")]
    [TestCase("sol")]
    [TestCase("ingen")]
    [TestCase("SOLin")]
    [TestCase("  olinge ")]
    public async Task ListAsync_CityPart_ReturnsMatchingAddresses(string city)
    {
        // Arrange
        SeededAddresses seeded = await SeedListAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(
            _textNormalizer.Normalize(city),
            null,
            CancellationToken.None);

        // Assert
        result.Select(address => address.Id).Should().BeEquivalentTo([seeded.Solingen.Id]);
    }

    [TestCase("Köln")]
    [TestCase("Koeln")]
    [TestCase("KOELN")]
    [TestCase("KÖLN")]
    [TestCase("koln")]
    [TestCase("öln")]
    public async Task ListAsync_CitySpellingVariant_ReturnsKoeln(string city)
    {
        // Arrange
        SeededAddresses seeded = await SeedListAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(
            _textNormalizer.Normalize(city),
            null,
            CancellationToken.None);

        // Assert
        result.Select(address => address.Id).Should().BeEquivalentTo([seeded.Koeln.Id]);
    }

    [TestCase("BAD  HOMBURG")]
    [TestCase("homburg vor der hohe")]
    [TestCase("Höhe")]
    public async Task ListAsync_CityWithSeveralWords_ReturnsMatchingAddresses(string city)
    {
        // Arrange
        SeededAddresses seeded = await SeedListAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(
            _textNormalizer.Normalize(city),
            null,
            CancellationToken.None);

        // Assert
        result.Select(address => address.Id).Should().BeEquivalentTo([seeded.BadHomburg.Id]);
    }

    [TestCase("SW1A 1AA")]
    [TestCase("sw1a 1aa")]
    [TestCase("SW1A1AA")]
    [TestCase("1A 1")]
    [TestCase(" a 1 a ")]
    public async Task ListAsync_PostalCodeWithSpaces_ReturnsMatchingAddresses(string postalCode)
    {
        // Arrange
        SeededAddresses seeded = await SeedListAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(
            null,
            _textNormalizer.NormalizePostalCode(postalCode),
            CancellationToken.None);

        // Assert
        result.Select(address => address.Id).Should().BeEquivalentTo([seeded.London.Id]);
    }

    [Test]
    public async Task ListAsync_PostalCodePart_ReturnsAllMatchingAddresses()
    {
        // Arrange
        SeededAddresses seeded = await SeedListAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(
            null,
            _textNormalizer.NormalizePostalCode("4"),
            CancellationToken.None);

        // Assert
        result.Select(address => address.Id)
            .Should().BeEquivalentTo([seeded.Koeln.Id, seeded.Solingen.Id, seeded.BadHomburg.Id]);
    }

    [Test]
    public async Task ListAsync_CityAndPostalCode_ReturnsAddressesMatchingBoth()
    {
        // Arrange
        SeededAddresses seeded = await SeedListAddressesAsync();
        Address otherKoeln = CreateAddress(AddressInput.Valid with { PostalCode = "50667" });
        await SeedAsync(otherKoeln);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(
            _textNormalizer.Normalize("Koeln"),
            _textNormalizer.NormalizePostalCode("511"),
            CancellationToken.None);

        // Assert
        result.Select(address => address.Id).Should().BeEquivalentTo([seeded.Koeln.Id]);
    }

    [Test]
    public async Task ListAsync_CityAndPostalCodeOfDifferentAddresses_ReturnsEmptyList()
    {
        // Arrange
        await SeedListAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(
            _textNormalizer.Normalize("Köln"),
            _textNormalizer.NormalizePostalCode("42651"),
            CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task ListAsync_BlankCityWithPostalCode_FiltersByPostalCodeOnly(string? blankCity)
    {
        // Arrange
        SeededAddresses seeded = await SeedListAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(
            blankCity,
            _textNormalizer.NormalizePostalCode("42651"),
            CancellationToken.None);

        // Assert
        result.Select(address => address.Id).Should().BeEquivalentTo([seeded.Solingen.Id]);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task ListAsync_CityWithBlankPostalCode_FiltersByCityOnly(string? blankPostalCode)
    {
        // Arrange
        SeededAddresses seeded = await SeedListAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(
            _textNormalizer.Normalize("London"),
            blankPostalCode,
            CancellationToken.None);

        // Assert
        result.Select(address => address.Id).Should().BeEquivalentTo([seeded.London.Id]);
    }

    [TestCase("Berlin")]
    [TestCase("Kölner")]
    [TestCase("solingenx")]
    public async Task ListAsync_CityWithoutMatch_ReturnsEmptyList(string city)
    {
        // Arrange
        await SeedListAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(
            _textNormalizer.Normalize(city),
            null,
            CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [TestCase("99999")]
    [TestCase("SW1A 1AB")]
    [TestCase("511470")]
    public async Task ListAsync_PostalCodeWithoutMatch_ReturnsEmptyList(string postalCode)
    {
        // Arrange
        await SeedListAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        IReadOnlyList<Address> result = await repository.ListAsync(
            null,
            _textNormalizer.NormalizePostalCode(postalCode),
            CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public async Task ListAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        await SeedListAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        Func<Task> act = () => repository.ListAsync(null, null, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
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

    private async Task<SeededAddresses> SeedListAddressesAsync()
    {
        var seeded = new SeededAddresses(
            CreateAddress(AddressInput.Valid),
            CreateAddress(AddressInput.Valid with { City = "Solingen", PostalCode = "42651" }),
            CreateAddress(AddressInput.Changed),
            CreateAddress(AddressInput.Valid with { City = "Bad Homburg vor der Höhe", PostalCode = "61348" }));
        await SeedAsync([.. seeded.All]);

        return seeded;
    }

    private async Task<IReadOnlyList<Address>> LoadAllAsync()
    {
        await using AddressDbContext context = CreateContext();

        return await context.Addresses.AsNoTracking().ToListAsync();
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

    /// <summary>
    /// The addresses for the list tests: Köln (51147), Solingen (42651), London (SW1A 1AA) and
    /// Bad Homburg vor der Höhe (61348).
    /// </summary>
    private sealed record SeededAddresses(Address Koeln, Address Solingen, Address London, Address BadHomburg)
    {
        public IReadOnlyList<Address> All => [Koeln, Solingen, London, BadHomburg];
    }
}
