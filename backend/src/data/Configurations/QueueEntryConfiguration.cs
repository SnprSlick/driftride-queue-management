using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DriftRide.Models;

namespace DriftRide.Data.Configurations
{
    /// <summary>
    /// Entity Framework configuration for QueueEntry entity
    /// </summary>
    public class QueueEntryConfiguration : IEntityTypeConfiguration<QueueEntry>
    {
        public void Configure(EntityTypeBuilder<QueueEntry> builder)
        {
            // Primary key
            builder.HasKey(q => q.Id);
            builder.Property(q => q.Id)
                .ValueGeneratedOnAdd();

            // Properties
            builder.Property(q => q.CustomerId)
                .IsRequired();

            builder.Property(q => q.PaymentId)
                .IsRequired();

            builder.Property(q => q.Position)
                .IsRequired();

            builder.Property(q => q.Status)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(q => q.QueuedAt)
                .IsRequired();

            builder.Property(q => q.StartedAt);

            builder.Property(q => q.CompletedAt);

            builder.Property(q => q.CompletedBy)
                .HasMaxLength(100);

            builder.Property(q => q.RowVersion)
                .IsRowVersion();

            // Indexes for performance - Position is clustered for queue ordering
            builder.HasIndex(q => q.Position)
                .HasDatabaseName("IX_QueueEntry_Position")
                .IsUnique();

            builder.HasIndex(q => new { q.Status, q.Position })
                .HasDatabaseName("IX_QueueEntry_Status_Position");

            builder.HasIndex(q => q.CustomerId)
                .HasDatabaseName("IX_QueueEntry_CustomerId");

            builder.HasIndex(q => q.PaymentId)
                .HasDatabaseName("IX_QueueEntry_PaymentId")
                .IsUnique(); // One-to-one relationship

            // Relationships
            builder.HasOne(q => q.Customer)
                .WithMany(c => c.QueueEntries)
                .HasForeignKey(q => q.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(q => q.Payment)
                .WithOne(p => p.QueueEntry)
                .HasForeignKey<QueueEntry>(q => q.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Table name
            builder.ToTable("QueueEntries");
        }
    }
}