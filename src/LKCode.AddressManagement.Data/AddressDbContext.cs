using LKCode.AddressManagement.Data.Configurations;
using LKCode.AddressManagement.Domain.Addresses;
using Microsoft.EntityFrameworkCore;

namespace LKCode.AddressManagement.Data;

/// <summary>
/// The EF Core database context of the address management.
/// </summary>
/// <param name="options">The options of the context, e.g. the database provider.</param>
public sealed class AddressDbContext(DbContextOptions<AddressDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets the addresses.
    /// </summary>
    public DbSet<Address> Addresses => Set<Address>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AddressConfiguration());
    }
}
