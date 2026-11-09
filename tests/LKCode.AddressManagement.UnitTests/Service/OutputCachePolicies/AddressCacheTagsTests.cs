using AwesomeAssertions;
using LKCode.AddressManagement.Service.OutputCachePolicies;

namespace LKCode.AddressManagement.UnitTests.Service.OutputCachePolicies;

public class AddressCacheTagsTests
{
    [TestCase("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00")]
    [TestCase("0198F2A4-6E0B-7C3D-9A1E-5B4C3D2E1F00")]
    [TestCase("0198f2a46e0b7c3d9a1e5b4c3d2e1f00")]
    [TestCase("{0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00}")]
    [TestCase("(0198F2A4-6E0B-7C3D-9A1E-5B4C3D2E1F00)")]
    public void ForAddress_IdInAnyFormat_ReturnsTagWithIdInLowercaseWithHyphens(string id)
    {
        // Act
        string tag = AddressCacheTags.ForAddress(Guid.Parse(id));

        // Assert
        tag.Should().Be("address-0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00");
    }

    [Test]
    public void ForAddress_EmptyId_ReturnsTagWithEmptyId()
    {
        // Act
        string tag = AddressCacheTags.ForAddress(Guid.Empty);

        // Assert
        tag.Should().Be("address-00000000-0000-0000-0000-000000000000");
    }

    [Test]
    public void ForAddress_DifferentIds_ReturnsDifferentTags()
    {
        // Act
        string tag = AddressCacheTags.ForAddress(Guid.Parse("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f00"));
        string otherTag = AddressCacheTags.ForAddress(Guid.Parse("0198f2a4-6e0b-7c3d-9a1e-5b4c3d2e1f01"));

        // Assert
        tag.Should().NotBe(otherTag);
    }

    [Test]
    public void List_Always_IsAddresses()
    {
        // Act
        string tag = AddressCacheTags.List;

        // Assert
        tag.Should().Be("addresses");
    }

    [Test]
    public void List_Always_DiffersFromTagOfAnyAddress()
    {
        // Act
        string addressTag = AddressCacheTags.ForAddress(Guid.Empty);

        // Assert
        addressTag.Should().NotBe(AddressCacheTags.List);
    }
}
