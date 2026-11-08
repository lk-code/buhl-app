using AwesomeAssertions;
using LKCode.AddressManagement.Core.Addresses.Dtos;
using LKCode.AddressManagement.Core.Addresses.Results;

namespace LKCode.AddressManagement.UnitTests.Core.Addresses.Results;

public class AddressChangeResultTests
{
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
        null,
        _createdAt,
        _createdAt,
        3);

    [Test]
    public void Success_Address_HasStatusSuccessAndAddress()
    {
        // Act
        var result = AddressChangeResult.Success(_address);

        // Assert
        result.Status.Should().Be(ChangeStatus.Success);
        result.IsSuccess.Should().BeTrue();
        result.Address.Should().BeSameAs(_address);
    }

    [Test]
    public void Success_NullAddress_ThrowsArgumentNullException()
    {
        // Arrange
        AddressResponse address = null!;

        // Act
        Action act = () => AddressChangeResult.Success(address);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("address");
    }

    [Test]
    public void NotFound_Always_HasStatusNotFoundWithoutAddress()
    {
        // Act
        AddressChangeResult result = AddressChangeResult.NotFound;

        // Assert
        result.Status.Should().Be(ChangeStatus.NotFound);
        result.IsSuccess.Should().BeFalse();
        result.Address.Should().BeNull();
    }

    [Test]
    public void VersionMismatch_Always_HasStatusVersionMismatchWithoutAddress()
    {
        // Act
        AddressChangeResult result = AddressChangeResult.VersionMismatch;

        // Assert
        result.Status.Should().Be(ChangeStatus.VersionMismatch);
        result.IsSuccess.Should().BeFalse();
        result.Address.Should().BeNull();
    }

    [Test]
    public void Equals_SuccessWithEqualAddresses_ReturnsTrue()
    {
        // Arrange
        var result = AddressChangeResult.Success(_address);

        // Act
        bool equal = result.Equals(AddressChangeResult.Success(_address with { }));

        // Assert
        equal.Should().BeTrue();
    }

    [Test]
    public void Equals_DifferentStatus_ReturnsFalse()
    {
        // Act
        bool equal = AddressChangeResult.NotFound.Equals(AddressChangeResult.VersionMismatch);

        // Assert
        equal.Should().BeFalse();
    }
}
