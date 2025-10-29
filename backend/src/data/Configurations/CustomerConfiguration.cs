using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DriftRide.Models;

namespace DriftRide.Data.Configurations
{
    /// <summary>
    /// Entity Framework configuration for Customer entity
    /// </summary>
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            // Primary key
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id)
                .ValueGeneratedOnAdd();

            // Properties
            builder.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(c => c.PhoneNumber)
                .HasMaxLength(20);

            builder.Property(c => c.CreatedAt)
                .IsRequired();

            builder.Property(c => c.RowVersion)
                .IsRowVersion();

            // Indexes for performance
            builder.HasIndex(c => new { c.Name, c.CreatedAt })
                .HasDatabaseName("IX_Customer_Name_CreatedAt");

            builder.HasIndex(c => c.CreatedAt)
                .HasDatabaseName("IX_Customer_CreatedAt");

            // Relationships
            builder.HasMany(c => c.Payments)
                .WithOne(p => p.Customer)
                .HasForeignKey(p => p.CustomerId)
                .OnDelete(DeleteBehavior.Restrict); // Cannot delete customers with payments

            builder.HasMany(c => c.QueueEntries)
                .WithOne(q => q.Customer)
                .HasForeignKey(q => q.CustomerId)
                .OnDelete(DeleteBehavior.Restrict); // Cannot delete customers with queue entries

            // Table name
            builder.ToTable("Customers");
        }
    }
}