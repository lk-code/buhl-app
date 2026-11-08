using System.Globalization;
using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Queries;
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
/// Tests listing addresses with <see cref="AddressRepository.ListAsync"/> against the EF Core InMemory provider:
/// filters, sort order and pages. Every test uses its own database, and arrange and act use separate contexts.
/// </summary>
public class AddressRepositoryListTests
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
    public async Task ListAsync_NoFilters_ReturnsAllAddresses()
    {
        // Arrange
        FilterAddresses seeded = await SeedFilterAddressesAsync();

        // Act
        AddressPage result = await ListAsync(Criteria());

        // Assert
        result.Addresses.Should().BeEquivalentTo(seeded.All);
        result.TotalCount.Should().Be(seeded.All.Count);
    }

    [TestCase(1)]
    [TestCase(5)]
    public async Task ListAsync_EmptyDatabase_ReturnsEmptyPageWithoutAddresses(int page)
    {
        // Act
        AddressPage result = await ListAsync(Criteria(page: page));

        // Assert
        result.Addresses.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t\n")]
    public async Task ListAsync_BlankFilters_IgnoresFilters(string? blankFilter)
    {
        // Arrange
        FilterAddresses seeded = await SeedFilterAddressesAsync();

        // Act
        AddressPage result = await ListAsync(Criteria(blankFilter, blankFilter));

        // Assert
        AssertAddresses(result, seeded.All);
    }

    [TestCase("Solingen")]
    [TestCase("sol")]
    [TestCase("ingen")]
    [TestCase("SOLin")]
    [TestCase("  olinge ")]
    public async Task ListAsync_CityPart_ReturnsMatchingAddresses(string city)
    {
        // Arrange
        FilterAddresses seeded = await SeedFilterAddressesAsync();

        // Act
        AddressPage result = await ListAsync(Criteria(_textNormalizer.Normalize(city)));

        // Assert
        AssertAddresses(result, seeded.Solingen);
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
        FilterAddresses seeded = await SeedFilterAddressesAsync();

        // Act
        AddressPage result = await ListAsync(Criteria(_textNormalizer.Normalize(city)));

        // Assert
        AssertAddresses(result, seeded.Koeln);
    }

    [TestCase("BAD  HOMBURG")]
    [TestCase("homburg vor der hohe")]
    [TestCase("Höhe")]
    public async Task ListAsync_CityWithSeveralWords_ReturnsMatchingAddresses(string city)
    {
        // Arrange
        FilterAddresses seeded = await SeedFilterAddressesAsync();

        // Act
        AddressPage result = await ListAsync(Criteria(_textNormalizer.Normalize(city)));

        // Assert
        AssertAddresses(result, seeded.BadHomburg);
    }

    [TestCase("SW1A 1AA")]
    [TestCase("sw1a 1aa")]
    [TestCase("SW1A1AA")]
    [TestCase("1A 1")]
    [TestCase(" a 1 a ")]
    public async Task ListAsync_PostalCodeWithSpaces_ReturnsMatchingAddresses(string postalCode)
    {
        // Arrange
        FilterAddresses seeded = await SeedFilterAddressesAsync();

        // Act
        AddressPage result =
            await ListAsync(Criteria(normalizedPostalCode: _textNormalizer.NormalizePostalCode(postalCode)));

        // Assert
        AssertAddresses(result, seeded.London);
    }

    [Test]
    public async Task ListAsync_PostalCodePart_ReturnsAllMatchingAddresses()
    {
        // Arrange
        FilterAddresses seeded = await SeedFilterAddressesAsync();

        // Act
        AddressPage result =
            await ListAsync(Criteria(normalizedPostalCode: _textNormalizer.NormalizePostalCode("4")));

        // Assert
        AssertAddresses(result, seeded.Koeln, seeded.Solingen, seeded.BadHomburg);
    }

    [Test]
    public async Task ListAsync_CityAndPostalCode_ReturnsAddressesMatchingBoth()
    {
        // Arrange
        FilterAddresses seeded = await SeedFilterAddressesAsync();
        await SeedAsync(CreateAddress(AddressInput.Valid with { PostalCode = "50667" }));

        // Act
        AddressPage result = await ListAsync(
            Criteria(_textNormalizer.Normalize("Koeln"), _textNormalizer.NormalizePostalCode("511")));

        // Assert
        AssertAddresses(result, seeded.Koeln);
    }

    [Test]
    public async Task ListAsync_CityAndPostalCodeOfDifferentAddresses_ReturnsEmptyPage()
    {
        // Arrange
        await SeedFilterAddressesAsync();

        // Act
        AddressPage result = await ListAsync(
            Criteria(_textNormalizer.Normalize("Köln"), _textNormalizer.NormalizePostalCode("42651")));

        // Assert
        AssertAddresses(result);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task ListAsync_BlankCityWithPostalCode_FiltersByPostalCodeOnly(string? blankCity)
    {
        // Arrange
        FilterAddresses seeded = await SeedFilterAddressesAsync();

        // Act
        AddressPage result = await ListAsync(Criteria(blankCity, _textNormalizer.NormalizePostalCode("42651")));

        // Assert
        AssertAddresses(result, seeded.Solingen);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task ListAsync_CityWithBlankPostalCode_FiltersByCityOnly(string? blankPostalCode)
    {
        // Arrange
        FilterAddresses seeded = await SeedFilterAddressesAsync();

        // Act
        AddressPage result = await ListAsync(Criteria(_textNormalizer.Normalize("London"), blankPostalCode));

        // Assert
        AssertAddresses(result, seeded.London);
    }

    [TestCase("Berlin")]
    [TestCase("Kölner")]
    [TestCase("solingenx")]
    public async Task ListAsync_CityWithoutMatch_ReturnsEmptyPage(string city)
    {
        // Arrange
        await SeedFilterAddressesAsync();

        // Act
        AddressPage result = await ListAsync(Criteria(_textNormalizer.Normalize(city)));

        // Assert
        AssertAddresses(result);
    }

    [TestCase("99999")]
    [TestCase("SW1A 1AB")]
    [TestCase("511470")]
    public async Task ListAsync_PostalCodeWithoutMatch_ReturnsEmptyPage(string postalCode)
    {
        // Arrange
        await SeedFilterAddressesAsync();

        // Act
        AddressPage result =
            await ListAsync(Criteria(normalizedPostalCode: _textNormalizer.NormalizePostalCode(postalCode)));

        // Assert
        AssertAddresses(result);
    }

    [TestCase(null, null, new[] { "Köln", "Solingen", "London", "Bad Homburg vor der Höhe" })]
    [TestCase("Koeln", null, new[] { "Köln" })]
    [TestCase(null, "4", new[] { "Köln", "Solingen", "Bad Homburg vor der Höhe" })]
    [TestCase("Koeln", "511", new[] { "Köln" })]
    public async Task ListAsync_ActiveAndDeletedAddresses_ReturnsAndCountsOnlyMatchingActiveAddresses(
        string? city,
        string? postalCode,
        string[] expectedCities)
    {
        // Arrange
        FilterAddresses seeded = await SeedFilterAddressesAsync();
        await SeedAsync([.. seeded.All.Select(address => CreateDeletedAddress(AddressInput.From(address)))]);

        // Act
        AddressPage result = await ListAsync(Criteria(
            city is null ? null : _textNormalizer.Normalize(city),
            postalCode is null ? null : _textNormalizer.NormalizePostalCode(postalCode)));

        // Assert
        AssertAddresses(result, [.. seeded.All.Where(address => expectedCities.Contains(address.City))]);
        result.Addresses.Should().OnlyContain(address => !address.IsDeleted);
    }

    [Test]
    public async Task ListAsync_OnlyDeletedAddresses_ReturnsEmptyPage()
    {
        // Arrange
        await SeedAsync(CreateDeletedAddress(AddressInput.Valid), CreateDeletedAddress(AddressInput.Changed));

        // Act
        AddressPage result = await ListAsync(Criteria());

        // Assert
        AssertAddresses(result);
    }

    [Test]
    public async Task ListAsync_FilterMatchingOnlyDeletedAddress_ReturnsEmptyPage()
    {
        // Arrange
        await SeedFilterAddressesAsync();
        await SeedAsync(CreateDeletedAddress(AddressInput.Valid with { City = "Berlin", PostalCode = "10115" }));

        // Act
        AddressPage result = await ListAsync(
            Criteria(_textNormalizer.Normalize("Berlin"), _textNormalizer.NormalizePostalCode("10115")));

        // Assert
        AssertAddresses(result);
    }

    [TestCase(AddressSortField.LastName, SortDirection.Ascending, new[] { "B", "D", "E", "A", "C" })]
    [TestCase(AddressSortField.LastName, SortDirection.Descending, new[] { "C", "A", "E", "D", "B" })]
    [TestCase(AddressSortField.FirstName, SortDirection.Ascending, new[] { "A", "E", "C", "B", "D" })]
    [TestCase(AddressSortField.FirstName, SortDirection.Descending, new[] { "D", "B", "C", "E", "A" })]
    [TestCase(AddressSortField.City, SortDirection.Ascending, new[] { "E", "C", "D", "A", "B" })]
    [TestCase(AddressSortField.City, SortDirection.Descending, new[] { "B", "A", "D", "C", "E" })]
    [TestCase(AddressSortField.PostalCode, SortDirection.Ascending, new[] { "D", "B", "A", "C", "E" })]
    [TestCase(AddressSortField.PostalCode, SortDirection.Descending, new[] { "E", "C", "A", "B", "D" })]
    [TestCase(AddressSortField.Country, SortDirection.Ascending, new[] { "B", "E", "C", "A", "D" })]
    [TestCase(AddressSortField.Country, SortDirection.Descending, new[] { "D", "A", "C", "E", "B" })]
    [TestCase(AddressSortField.CreatedAt, SortDirection.Ascending, new[] { "A", "B", "C", "D", "E" })]
    [TestCase(AddressSortField.CreatedAt, SortDirection.Descending, new[] { "E", "D", "C", "B", "A" })]
    [TestCase(AddressSortField.UpdatedAt, SortDirection.Ascending, new[] { "B", "C", "E", "D", "A" })]
    [TestCase(AddressSortField.UpdatedAt, SortDirection.Descending, new[] { "A", "D", "E", "C", "B" })]
    public async Task ListAsync_SortField_ReturnsAddressesInOrder(
        AddressSortField sortBy,
        SortDirection sortDirection,
        string[] expectedOrder)
    {
        // Arrange
        IReadOnlyDictionary<string, Address> seeded = await SeedSortAddressesAsync();

        // Act
        AddressPage result = await ListAsync(Criteria(sortBy: sortBy, sortDirection: sortDirection));

        // Assert
        result.Addresses.Select(address => address.Id)
            .Should().Equal(expectedOrder.Select(key => seeded[key].Id));
        result.TotalCount.Should().Be(seeded.Count);
    }

    [TestCase(AddressSortField.LastName)]
    [TestCase(AddressSortField.FirstName)]
    [TestCase(AddressSortField.City)]
    [TestCase(AddressSortField.PostalCode)]
    [TestCase(AddressSortField.Country)]
    [TestCase(AddressSortField.CreatedAt)]
    [TestCase(AddressSortField.UpdatedAt)]
    public async Task ListAsync_Descending_ReturnsReverseOfAscendingOrder(AddressSortField sortBy)
    {
        // Arrange
        await SeedSortAddressesAsync();
        await SeedAsync(CreateAddresses(3, _ => AddressInput.Valid));

        // Act
        AddressPage ascending = await ListAsync(Criteria(sortBy: sortBy, sortDirection: SortDirection.Ascending));
        AddressPage descending = await ListAsync(Criteria(sortBy: sortBy, sortDirection: SortDirection.Descending));

        // Assert
        descending.Addresses.Select(address => address.Id)
            .Should().Equal(ascending.Addresses.Select(address => address.Id).Reverse());
    }

    [TestCase(AddressSortField.LastName)]
    [TestCase(AddressSortField.FirstName)]
    [TestCase(AddressSortField.City)]
    [TestCase(AddressSortField.PostalCode)]
    [TestCase(AddressSortField.Country)]
    [TestCase(AddressSortField.CreatedAt)]
    [TestCase(AddressSortField.UpdatedAt)]
    public async Task ListAsync_EqualValuesAscending_SortsByIdAscending(AddressSortField sortBy)
    {
        // Arrange
        IReadOnlyList<Address> addresses = await SeedEqualAddressesAsync();

        // Act
        AddressPage result = await ListAsync(Criteria(sortBy: sortBy, sortDirection: SortDirection.Ascending));

        // Assert
        result.Addresses.Select(address => address.Id)
            .Should().Equal(addresses.Select(address => address.Id).Order());
    }

    [TestCase(AddressSortField.LastName)]
    [TestCase(AddressSortField.FirstName)]
    [TestCase(AddressSortField.City)]
    [TestCase(AddressSortField.PostalCode)]
    [TestCase(AddressSortField.Country)]
    [TestCase(AddressSortField.CreatedAt)]
    [TestCase(AddressSortField.UpdatedAt)]
    public async Task ListAsync_EqualValuesDescending_SortsByIdDescending(AddressSortField sortBy)
    {
        // Arrange
        IReadOnlyList<Address> addresses = await SeedEqualAddressesAsync();

        // Act
        AddressPage result = await ListAsync(Criteria(sortBy: sortBy, sortDirection: SortDirection.Descending));

        // Assert
        result.Addresses.Select(address => address.Id)
            .Should().Equal(addresses.Select(address => address.Id).OrderDescending());
    }

    [Test]
    public async Task ListAsync_EqualValuesOnSeveralPages_ReturnsEveryAddressOnce()
    {
        // Arrange
        IReadOnlyList<Address> addresses = await SeedEqualAddressesAsync();

        // Act
        AddressPage first = await ListAsync(Criteria(page: 1, pageSize: 2));
        AddressPage second = await ListAsync(Criteria(page: 2, pageSize: 2));

        // Assert
        first.Addresses.Concat(second.Addresses).Select(address => address.Id)
            .Should().Equal(addresses.Select(address => address.Id).Order());
    }

    [TestCase(1, 10, 1, 10)]
    [TestCase(2, 10, 11, 20)]
    [TestCase(3, 10, 21, 25)]
    [TestCase(1, 1, 1, 1)]
    [TestCase(7, 1, 7, 7)]
    [TestCase(25, 1, 25, 25)]
    [TestCase(1, 25, 1, 25)]
    [TestCase(5, 5, 21, 25)]
    [TestCase(1, 100, 1, 25)]
    [TestCase(2, 24, 25, 25)]
    public async Task ListAsync_PageWithinList_ReturnsAddressesOfPageAndTotalCount(
        int page,
        int pageSize,
        int expectedFirst,
        int expectedLast)
    {
        // Arrange
        await SeedNumberedAddressesAsync(25);

        // Act
        AddressPage result = await ListAsync(Criteria(page: page, pageSize: pageSize));

        // Assert
        result.Addresses.Select(address => address.LastName)
            .Should().Equal(NumberedLastNames(expectedFirst, expectedLast));
        result.TotalCount.Should().Be(25);
    }

    [TestCase(4, 10)]
    [TestCase(6, 5)]
    [TestCase(26, 1)]
    [TestCase(2, 25)]
    [TestCase(2, 100)]
    [TestCase(1000, 20)]
    [TestCase(int.MaxValue, 1)]
    [TestCase(int.MaxValue, 100)]
    public async Task ListAsync_PageBehindEnd_ReturnsEmptyPageWithTotalCount(int page, int pageSize)
    {
        // Arrange
        await SeedNumberedAddressesAsync(25);

        // Act
        AddressPage result = await ListAsync(Criteria(page: page, pageSize: pageSize));

        // Assert
        result.Addresses.Should().BeEmpty();
        result.TotalCount.Should().Be(25);
    }

    [TestCase(1)]
    [TestCase(7)]
    [TestCase(25)]
    public async Task ListAsync_AllPages_ReturnEveryAddressOnceInOrder(int pageSize)
    {
        // Arrange
        await SeedNumberedAddressesAsync(25);
        var lastNames = new List<string>();

        // Act
        for (int page = 1; page <= (25 + pageSize - 1) / pageSize; page++)
        {
            AddressPage result = await ListAsync(Criteria(page: page, pageSize: pageSize));
            lastNames.AddRange(result.Addresses.Select(address => address.LastName));
        }

        // Assert
        lastNames.Should().Equal(NumberedLastNames(1, 25));
    }

    [Test]
    public async Task ListAsync_FilterAndPage_PagesAndCountsOnlyMatchingAddresses()
    {
        // Arrange
        await SeedNumberedAddressesAsync(25, number => number % 2 == 0 ? "Bonn" : "Köln");

        // Act
        AddressPage result = await ListAsync(Criteria(
            _textNormalizer.Normalize("Koeln"),
            sortDirection: SortDirection.Descending,
            page: 2,
            pageSize: 5));

        // Assert
        result.Addresses.Select(address => address.LastName)
            .Should().Equal("Name15", "Name13", "Name11", "Name09", "Name07");
        result.TotalCount.Should().Be(13);
    }

    [Test]
    public async Task ListAsync_FilterAndPageBehindEnd_ReturnsEmptyPageWithFilteredTotalCount()
    {
        // Arrange
        await SeedNumberedAddressesAsync(25, number => number % 2 == 0 ? "Bonn" : "Köln");

        // Act
        AddressPage result = await ListAsync(Criteria(_textNormalizer.Normalize("Bonn"), page: 3, pageSize: 6));

        // Assert
        result.Addresses.Should().BeEmpty();
        result.TotalCount.Should().Be(12);
    }

    [Test]
    public async Task ListAsync_DeletedAddressesBetweenActiveOnes_PagesAndCountsOnlyActiveAddresses()
    {
        // Arrange
        await SeedNumberedAddressesAsync(10);
        await SeedAsync(
            CreateDeletedAddress(AddressInput.Valid with { LastName = "Name00" }),
            CreateDeletedAddress(AddressInput.Valid with { LastName = "Name05a" }),
            CreateDeletedAddress(AddressInput.Valid with { LastName = "Name99" }));

        // Act
        AddressPage result = await ListAsync(Criteria(page: 2, pageSize: 4));

        // Assert
        result.Addresses.Select(address => address.LastName).Should().Equal(NumberedLastNames(5, 8));
        result.TotalCount.Should().Be(10);
    }

    [Test]
    public async Task ListAsync_AnyPage_ReturnsUntrackedAddresses()
    {
        // Arrange
        await SeedNumberedAddressesAsync(3);
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        // Act
        await repository.ListAsync(Criteria(), CancellationToken.None);

        // Assert
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Test]
    public async Task ListAsync_NullCriteria_ThrowsArgumentNullException()
    {
        // Arrange
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        AddressListCriteria criteria = null!;

        // Act
        Func<Task> act = () => repository.ListAsync(criteria, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("criteria");
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public async Task ListAsync_PageBelowOne_ThrowsArgumentOutOfRangeException(int page)
    {
        // Act
        Func<Task> act = () => ListAsync(Criteria(page: page));

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public async Task ListAsync_PageSizeBelowOne_ThrowsArgumentOutOfRangeException(int pageSize)
    {
        // Act
        Func<Task> act = () => ListAsync(Criteria(pageSize: pageSize));

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [TestCase(-1)]
    [TestCase(7)]
    public async Task ListAsync_UndefinedSortField_ThrowsArgumentOutOfRangeException(int sortBy)
    {
        // Arrange
        await SeedNumberedAddressesAsync(3);

        // Act
        Func<Task> act = () => ListAsync(Criteria(sortBy: (AddressSortField)sortBy));

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [TestCase(-1)]
    [TestCase(2)]
    public async Task ListAsync_UndefinedSortDirection_ThrowsArgumentOutOfRangeException(int sortDirection)
    {
        // Arrange
        await SeedNumberedAddressesAsync(3);

        // Act
        Func<Task> act = () => ListAsync(Criteria(sortDirection: (SortDirection)sortDirection));

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ListAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        await SeedFilterAddressesAsync();
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        Func<Task> act = () => repository.ListAsync(Criteria(), cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static AddressListCriteria Criteria(
        string? normalizedCity = null,
        string? normalizedPostalCode = null,
        AddressSortField sortBy = AddressSortField.LastName,
        SortDirection sortDirection = SortDirection.Ascending,
        int page = 1,
        int pageSize = AddressListConstraints.MaxPageSize) =>
        new(normalizedCity, normalizedPostalCode, sortBy, sortDirection, page, pageSize);

    private static string NumberedLastName(int number) =>
        string.Create(CultureInfo.InvariantCulture, $"Name{number:00}");

    private static IEnumerable<string> NumberedLastNames(int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(NumberedLastName);

    /// <summary>
    /// Asserts that the page contains exactly the <paramref name="expected"/> addresses in any order and counts them.
    /// </summary>
    private static void AssertAddresses(AddressPage result, params IReadOnlyList<Address> expected)
    {
        result.Addresses.Select(address => address.Id).Should().BeEquivalentTo(expected.Select(address => address.Id));
        result.TotalCount.Should().Be(expected.Count);
    }

    private async Task<AddressPage> ListAsync(AddressListCriteria criteria)
    {
        await using AddressDbContext context = CreateContext();
        var repository = new AddressRepository(context);

        return await repository.ListAsync(criteria, CancellationToken.None);
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

    /// <summary>
    /// Creates the addresses one minute apart, so that their ids and creation times have the order of their index.
    /// </summary>
    private Address[] CreateAddresses(int count, Func<int, AddressInput> input)
    {
        var addresses = new Address[count];
        for (int index = 0; index < count; index++)
        {
            _timeProvider.Advance(TimeSpan.FromMinutes(1));
            addresses[index] = CreateAddress(input(index));
        }

        return addresses;
    }

    private async Task SeedAsync(params Address[] addresses)
    {
        await using AddressDbContext context = CreateContext();
        context.Addresses.AddRange(addresses);
        await context.SaveChangesAsync();
    }

    private async Task<FilterAddresses> SeedFilterAddressesAsync()
    {
        var seeded = new FilterAddresses(
            CreateAddress(AddressInput.Valid),
            CreateAddress(AddressInput.Valid with { City = "Solingen", PostalCode = "42651" }),
            CreateAddress(AddressInput.Changed),
            CreateAddress(AddressInput.Valid with { City = "Bad Homburg vor der Höhe", PostalCode = "61348" }));
        await SeedAsync([.. seeded.All]);

        return seeded;
    }

    /// <summary>
    /// Seeds the addresses A to E, created in this order one minute apart. Each sort field has another order:
    /// last name B D E A C, first name A E C B D, city E C D A B, postal code D B A C E, country B E C A D, creation
    /// time A B C D E. D and A are changed afterwards, so the order of the last change is B C E D A. The addresses are
    /// stored in yet another order (C A E B D).
    /// </summary>
    private async Task<IReadOnlyDictionary<string, Address>> SeedSortAddressesAsync()
    {
        AddressInput[] inputs =
        [
            AddressInput.Valid with
            {
                LastName = "Schmidt", FirstName = "Anna", City = "Hamburg", PostalCode = "20095", Country = "DE",
            },
            AddressInput.Valid with
            {
                LastName = "Becker", FirstName = "Paul", City = "Wien", PostalCode = "1010", Country = "AT",
            },
            AddressInput.Valid with
            {
                LastName = "Wagner", FirstName = "Lena", City = "Bern", PostalCode = "3011", Country = "CH",
            },
            AddressInput.Valid with
            {
                LastName = "Fischer", FirstName = "Tom", City = "Dresden", PostalCode = "01067", Country = "LU",
            },
            AddressInput.Valid with
            {
                LastName = "Meyer", FirstName = "Clara", City = "Aachen", PostalCode = "52062", Country = "BE",
            },
        ];
        Address[] addresses = CreateAddresses(inputs.Length, index => inputs[index]);
        var seeded = new Dictionary<string, Address>(StringComparer.Ordinal)
        {
            ["A"] = addresses[0],
            ["B"] = addresses[1],
            ["C"] = addresses[2],
            ["D"] = addresses[3],
            ["E"] = addresses[4],
        };
        foreach (string key in new[] { "D", "A" })
        {
            _timeProvider.Advance(TimeSpan.FromMinutes(1));
            Address address = seeded[key];
            address.Update(
                address.FirstName,
                address.LastName,
                address.Street,
                address.HouseNumber,
                address.PostalCode,
                address.City,
                address.Country,
                address.Email,
                _textNormalizer,
                _timeProvider);
        }

        await SeedAsync(seeded["C"], seeded["A"], seeded["E"], seeded["B"], seeded["D"]);

        return seeded;
    }

    /// <summary>
    /// Seeds four addresses with the same values, created at the same time, so that only their ids differ. They are
    /// stored in an order that differs from the order of their ids in both directions.
    /// </summary>
    private async Task<IReadOnlyList<Address>> SeedEqualAddressesAsync()
    {
        Address[] byId =
        [
            .. Enumerable.Range(0, 4)
                .Select(_ => CreateAddress(AddressInput.Valid))
                .OrderBy(address => address.Id),
        ];
        await SeedAsync(byId[1], byId[3], byId[0], byId[2]);

        return byId;
    }

    /// <summary>
    /// Seeds addresses with the last names <c>Name01</c> to <c>Name{count}</c>, stored in reverse order.
    /// </summary>
    private async Task SeedNumberedAddressesAsync(int count, Func<int, string>? city = null)
    {
        Address[] addresses = CreateAddresses(count, index => AddressInput.Valid with
        {
            LastName = NumberedLastName(index + 1),
            City = city?.Invoke(index + 1) ?? AddressInput.Valid.City,
        });
        await SeedAsync([.. addresses.Reverse()]);
    }

    /// <summary>
    /// The addresses for the filter tests: Köln (51147), Solingen (42651), London (SW1A 1AA) and
    /// Bad Homburg vor der Höhe (61348).
    /// </summary>
    private sealed record FilterAddresses(Address Koeln, Address Solingen, Address London, Address BadHomburg)
    {
        public IReadOnlyList<Address> All => [Koeln, Solingen, London, BadHomburg];
    }
}
