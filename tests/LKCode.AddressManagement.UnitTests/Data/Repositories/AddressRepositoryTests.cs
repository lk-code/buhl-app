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

    private async Task<IReadOnlyList<Address>> LoadAllAsync()
    {
        await using AddressDbContext context = CreateContext();

        return await context.Addresses.AsNoTracking().ToListAsync();
    }
}
