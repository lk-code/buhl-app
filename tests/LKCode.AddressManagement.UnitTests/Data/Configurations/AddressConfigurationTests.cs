using AwesomeAssertions;
using LKCode.AddressManagement.Data;
using LKCode.AddressManagement.Data.Configurations;
using LKCode.AddressManagement.Domain.Addresses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LKCode.AddressManagement.UnitTests.Data.Configurations;

public class AddressConfigurationTests
{
    private AddressDbContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        DbContextOptions<AddressDbContext> options = new DbContextOptionsBuilder<AddressDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AddressDbContext(options);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public void Configure_Address_MapsAllProperties()
    {
        // Arrange
        string[] expectedProperties =
        [
            nameof(Address.Id),
            nameof(Address.FirstName),
            nameof(Address.LastName),
            nameof(Address.Street),
            nameof(Address.HouseNumber),
            nameof(Address.PostalCode),
            nameof(Address.City),
            nameof(Address.Country),
            nameof(Address.Email),
            nameof(Address.NormalizedCity),
            nameof(Address.NormalizedPostalCode),
            nameof(Address.CreatedAt),
            nameof(Address.UpdatedAt),
            nameof(Address.Version),
        ];

        // Act
        IEntityType entityType = GetAddressEntityType();

        // Assert
        entityType.GetProperties().Select(property => property.Name).Should().BeEquivalentTo(expectedProperties);
    }

    [Test]
    public void Configure_Address_UsesIdAsPrimaryKey()
    {
        // Arrange
        IEntityType entityType = GetAddressEntityType();

        // Act
        IKey? primaryKey = entityType.FindPrimaryKey();

        // Assert
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().ContainSingle().Which.Name.Should().Be(nameof(Address.Id));
    }

    [Test]
    public void Configure_Id_IsNeverGeneratedByDatabase()
    {
        // Arrange
        IEntityType entityType = GetAddressEntityType();

        // Act
        IProperty property = GetProperty(entityType, nameof(Address.Id));

        // Assert
        property.ValueGenerated.Should().Be(ValueGenerated.Never);
    }

    [TestCase(nameof(Address.Id))]
    [TestCase(nameof(Address.FirstName))]
    [TestCase(nameof(Address.LastName))]
    [TestCase(nameof(Address.Street))]
    [TestCase(nameof(Address.HouseNumber))]
    [TestCase(nameof(Address.PostalCode))]
    [TestCase(nameof(Address.City))]
    [TestCase(nameof(Address.Country))]
    [TestCase(nameof(Address.NormalizedCity))]
    [TestCase(nameof(Address.NormalizedPostalCode))]
    [TestCase(nameof(Address.CreatedAt))]
    [TestCase(nameof(Address.UpdatedAt))]
    public void Configure_RequiredProperty_IsNotNullable(string propertyName)
    {
        // Arrange
        IEntityType entityType = GetAddressEntityType();

        // Act
        IProperty property = GetProperty(entityType, propertyName);

        // Assert
        property.IsNullable.Should().BeFalse();
    }

    [Test]
    public void Configure_Email_IsNullable()
    {
        // Arrange
        IEntityType entityType = GetAddressEntityType();

        // Act
        IProperty property = GetProperty(entityType, nameof(Address.Email));

        // Assert
        property.IsNullable.Should().BeTrue();
    }

    [TestCase(nameof(Address.FirstName), AddressConstraints.FirstNameMaxLength)]
    [TestCase(nameof(Address.LastName), AddressConstraints.LastNameMaxLength)]
    [TestCase(nameof(Address.Street), AddressConstraints.StreetMaxLength)]
    [TestCase(nameof(Address.HouseNumber), AddressConstraints.HouseNumberMaxLength)]
    [TestCase(nameof(Address.PostalCode), AddressConstraints.PostalCodeMaxLength)]
    [TestCase(nameof(Address.City), AddressConstraints.CityMaxLength)]
    [TestCase(nameof(Address.Country), AddressConstraints.CountryMaxLength)]
    [TestCase(nameof(Address.Email), AddressConstraints.EmailMaxLength)]
    [TestCase(nameof(Address.NormalizedPostalCode), AddressConstraints.PostalCodeMaxLength)]
    public void Configure_TextProperty_HasMaxLengthFromConstraints(string propertyName, int expectedMaxLength)
    {
        // Arrange
        IEntityType entityType = GetAddressEntityType();

        // Act
        IProperty property = GetProperty(entityType, propertyName);

        // Assert
        property.GetMaxLength().Should().Be(expectedMaxLength);
    }

    [Test]
    public void Configure_NormalizedCity_HasNoMaxLength()
    {
        // Arrange
        IEntityType entityType = GetAddressEntityType();

        // Act
        IProperty property = GetProperty(entityType, nameof(Address.NormalizedCity));

        // Assert
        property.GetMaxLength().Should().BeNull();
    }

    [TestCase(nameof(Address.NormalizedCity))]
    [TestCase(nameof(Address.NormalizedPostalCode))]
    public void Configure_NormalizedProperty_HasNonUniqueSingleColumnIndex(string propertyName)
    {
        // Arrange
        IEntityType entityType = GetAddressEntityType();

        // Act
        IEnumerable<IIndex> indexes = entityType.GetIndexes();

        // Assert
        indexes.Should().ContainSingle(index =>
                index.Properties.Count == 1 && index.Properties[0].Name == propertyName)
            .Which.IsUnique.Should().BeFalse();
    }

    [Test]
    public void Configure_Address_HasOnlyIndexesOnNormalizedProperties()
    {
        // Arrange
        IEntityType entityType = GetAddressEntityType();

        // Act
        IEnumerable<IIndex> indexes = entityType.GetIndexes();

        // Assert
        indexes.Should().HaveCount(2);
    }

    [Test]
    public void Configure_NullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var configuration = new AddressConfiguration();
        EntityTypeBuilder<Address> builder = null!;

        // Act
        Action act = () => configuration.Configure(builder);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    private IEntityType GetAddressEntityType()
    {
        IEntityType? entityType = _context.Model.FindEntityType(typeof(Address));
        entityType.Should().NotBeNull();

        return entityType!;
    }

    private static IProperty GetProperty(IEntityType entityType, string propertyName)
    {
        IProperty? property = entityType.FindProperty(propertyName);
        property.Should().NotBeNull();

        return property!;
    }
}
