using Customers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Customers.Infrastructure.Migrations;

[DbContext(typeof(CustomersDbContext))]
public sealed class CustomersDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(customer => customer.Id);
            entity.Property(customer => customer.PersonType).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(customer => customer.Document).HasMaxLength(14).IsRequired();
            entity.HasIndex(customer => customer.Document).IsUnique();
            entity.Property(customer => customer.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(customer => customer.LegalName).HasMaxLength(200);
            entity.Property(customer => customer.StateRegistration).HasMaxLength(30);
            entity.Property(customer => customer.MunicipalRegistration).HasMaxLength(30);
            entity.Property(customer => customer.Email).HasMaxLength(254).IsRequired();
            entity.Property(customer => customer.Phone).HasMaxLength(13).IsRequired();
            entity.HasIndex(customer => customer.DisplayName);
            entity.HasIndex(customer => customer.Email);
            entity.HasMany(customer => customer.Addresses)
                .WithOne()
                .HasForeignKey(address => address.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("customer_addresses");
            entity.HasKey(address => address.Id);
            entity.Property(address => address.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(address => address.PostalCode).HasMaxLength(8).IsRequired();
            entity.Property(address => address.Street).HasMaxLength(200).IsRequired();
            entity.Property(address => address.Number).HasMaxLength(30).IsRequired();
            entity.Property(address => address.Complement).HasMaxLength(120);
            entity.Property(address => address.Neighborhood).HasMaxLength(120);
            entity.Property(address => address.City).HasMaxLength(120).IsRequired();
            entity.Property(address => address.State).HasMaxLength(2).IsRequired();
            entity.Property(address => address.Country).HasMaxLength(2).IsRequired();
            entity.Property(address => address.IbgeCityCode).HasMaxLength(10);
            entity.Property(address => address.Latitude).HasPrecision(12, 8);
            entity.Property(address => address.Longitude).HasPrecision(12, 8);
            entity.HasIndex(address => address.CustomerId);
            entity.HasIndex(address => address.PostalCode);
        });
    }
}
