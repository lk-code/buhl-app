using AwesomeAssertions;
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

    [Test]
    public async Task UpdateAsync_TrackedAddressChanged_PersistsAllChanges()
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
        await repository.UpdateAsync(address!, CancellationToken.None);

        // Assert
        IReadOnlyList<Address> stored = await LoadAllAsync();
        Address storedAddress = stored.Should().ContainSingle().Subject;
        AddressInput.From(storedAddress).Should().Be(AddressInput.Changed);
        storedAddress.Id.Should().Be(original.Id);
        storedAddress.NormalizedCity.Should().Be("london");
        storedAddress.NormalizedPostalCode.Should().Be("SW1A1AA");
        storedAddress.CreatedAt.Should().Be(_startTime);
        storedAddress.UpdatedAt.Should().Be(_startTime.AddMinutes(5));
    }

    [Test]
    public async Task UpdateAsync_DetachedAddressChanged_PersistsAllChanges()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        await SeedAsync(address);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        UpdateAddress(address, AddressInput.Changed);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        await repository.UpdateAsync(address, CancellationToken.None);

        // Assert
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().ContainSingle().Which.Should().BeEquivalentTo(address);
    }

    [Test]
    public async Task UpdateAsync_ChangedAddress_LeavesOtherAddressesUnchanged()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        Address other = CreateAddress(AddressInput.Valid);
        await SeedAsync(address, other);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        UpdateAddress(address, AddressInput.Changed);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        await repository.UpdateAsync(address, CancellationToken.None);

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
        Func<Task> act = () => repository.UpdateAsync(null!, CancellationToken.None);

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
        Func<Task> act = () => repository.UpdateAsync(address!, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().ContainSingle().Which.Should().BeEquivalentTo(original);
    }

    [Test]
    public async Task UpdateAsync_NeverSavedAddress_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange
        await SeedAsync(CreateAddress(AddressInput.Valid));
        Address address = CreateAddress(AddressInput.Changed);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        Func<Task> act = () => repository.UpdateAsync(address, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Test]
    public async Task DeleteAsync_TrackedAddress_RemovesOnlyThisAddress()
    {
        // Arrange
        Address original = CreateAddress(AddressInput.Valid);
        Address other = CreateAddress(AddressInput.Changed);
        await SeedAsync(original, other);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        Address? address = await repository.GetByIdAsync(original.Id, CancellationToken.None);

        // Act
        await repository.DeleteAsync(address!, CancellationToken.None);

        // Assert
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().ContainSingle().Which.Should().BeEquivalentTo(other);
    }

    [Test]
    public async Task DeleteAsync_DetachedAddress_RemovesAddress()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        await SeedAsync(address);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        await repository.DeleteAsync(address, CancellationToken.None);

        // Assert
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().BeEmpty();
    }

    [Test]
    public async Task DeleteAsync_DeletedAddress_CannotBeLoadedById()
    {
        // Arrange
        Address address = CreateAddress(AddressInput.Valid);
        await SeedAsync(address);
        await using (AddressDbContext deleteContext = CreateContext())
        {
            await new AddressRepository(deleteContext).DeleteAsync(address, CancellationToken.None);
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
        Func<Task> act = () => repository.DeleteAsync(null!, CancellationToken.None);

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
        Func<Task> act = () => repository.DeleteAsync(address, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().ContainSingle().Which.Should().BeEquivalentTo(address);
    }

    [Test]
    public async Task DeleteAsync_NeverSavedAddress_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange
        Address existing = CreateAddress(AddressInput.Valid);
        await SeedAsync(existing);
        Address address = CreateAddress(AddressInput.Changed);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        Func<Task> act = () => repository.DeleteAsync(address, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        IReadOnlyList<Address> stored = await LoadAllAsync();
        stored.Should().ContainSingle().Which.Should().BeEquivalentTo(existing);
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

    private async Task<IReadOnlyList<Address>> LoadAllAsync()
    {
        await using AddressDbContext context = CreateContext();

        return await context.Addresses.AsNoTracking().ToListAsync();
    }
}
