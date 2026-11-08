using LKCode.AddressManagement.Domain.Addresses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LKCode.AddressManagement.Data.Configurations;

/// <summary>
/// Maps the <see cref="Address"/> entity to the database.
/// </summary>
public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        throw new NotImplementedException();
    }
}
