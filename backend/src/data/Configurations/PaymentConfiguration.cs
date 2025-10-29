using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DriftRide.Models;

namespace DriftRide.Data.Configurations
{
    /// <summary>
    /// Entity Framework configuration for Payment entity
    /// </summary>
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            // Primary key
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id)
                .ValueGeneratedOnAdd();

            // Properties
            builder.Property(p => p.CustomerId)
                .IsRequired();

            builder.Property(p => p.Amount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(p => p.PaymentMethod)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(p => p.Status)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(p => p.ExternalTransactionId)
                .HasMaxLength(255);

            builder.Property(p => p.CreatedAt)
                .IsRequired();

            builder.Property(p => p.ConfirmedAt);

            builder.Property(p => p.ConfirmedBy)
                .HasMaxLength(100);

            builder.Property(p => p.Notes)
                .HasMaxLength(500);

            builder.Property(p => p.RowVersion)
                .IsRowVersion();

            // Indexes for performance
            builder.HasIndex(p => new { p.CustomerId, p.Status })
                .HasDatabaseName("IX_Payment_CustomerId_Status");

            builder.HasIndex(p => new { p.Status, p.CreatedAt })
                .HasDatabaseName("IX_Payment_Status_CreatedAt");

            builder.HasIndex(p => p.CustomerId)
                .HasDatabaseName("IX_Payment_CustomerId");

            // Relationships
            builder.HasOne(p => p.Customer)
                .WithMany(c => c.Payments)
                .HasForeignKey(p => p.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.QueueEntry)
                .WithOne(q => q.Payment)
                .HasForeignKey<QueueEntry>(q => q.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Table name
            builder.ToTable("Payments");
        }
    }
}