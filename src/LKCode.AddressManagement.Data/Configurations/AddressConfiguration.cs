using LKCode.AddressManagement.Domain.Addresses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LKCode.AddressManagement.Data.Configurations;

/// <summary>
/// Maps the <see cref="Address"/> entity to the database.
/// </summary>
/// <remarks>
/// The maximum lengths come from <see cref="AddressConstraints"/>. <see cref="Address.NormalizedPostalCode"/> is never
/// longer than <see cref="Address.PostalCode"/>. <see cref="Address.NormalizedCity"/> has no maximum length because
/// the normalization can lengthen a value (e.g. <c>ß</c> becomes <c>ss</c>).
/// </remarks>
public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(address => address.Id);
        builder.Property(address => address.Id).ValueGeneratedNever();

        builder.Property(address => address.FirstName).IsRequired().HasMaxLength(AddressConstraints.FirstNameMaxLength);
        builder.Property(address => address.LastName).IsRequired().HasMaxLength(AddressConstraints.LastNameMaxLength);
        builder.Property(address => address.Street).IsRequired().HasMaxLength(AddressConstraints.StreetMaxLength);
        builder.Property(address => address.HouseNumber)
            .IsRequired()
            .HasMaxLength(AddressConstraints.HouseNumberMaxLength);
        builder.Property(address => address.PostalCode)
            .IsRequired()
            .HasMaxLength(AddressConstraints.PostalCodeMaxLength);
        builder.Property(address => address.City).IsRequired().HasMaxLength(AddressConstraints.CityMaxLength);
        builder.Property(address => address.Country).IsRequired().HasMaxLength(AddressConstraints.CountryMaxLength);
        builder.Property(address => address.Email).IsRequired(false).HasMaxLength(AddressConstraints.EmailMaxLength);

        builder.Property(address => address.NormalizedCity).IsRequired();
        builder.Property(address => address.NormalizedPostalCode)
            .IsRequired()
            .HasMaxLength(AddressConstraints.PostalCodeMaxLength);

        builder.Property(address => address.CreatedAt).IsRequired();
        builder.Property(address => address.UpdatedAt).IsRequired();

        builder.HasIndex(address => address.NormalizedCity);
        builder.HasIndex(address => address.NormalizedPostalCode);
    }
}
