using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DriftRide.Models;

namespace DriftRide.Data.Configurations
{
    /// <summary>
    /// Entity Framework configuration for PaymentConfiguration entity
    /// (Named PaymentConfigurationEntityConfiguration to avoid naming conflict)
    /// </summary>
    public class PaymentConfigurationEntityConfiguration : IEntityTypeConfiguration<Models.PaymentConfiguration>
    {
        public void Configure(EntityTypeBuilder<Models.PaymentConfiguration> builder)
        {
            // Primary key
            builder.HasKey(pc => pc.Id);
            builder.Property(pc => pc.Id)
                .ValueGeneratedOnAdd();

            // Properties
            builder.Property(pc => pc.PaymentMethod)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(pc => pc.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(pc => pc.PaymentUrl)
                .HasMaxLength(500);

            builder.Property(pc => pc.IsEnabled)
                .IsRequired();

            builder.Property(pc => pc.PricePerRide)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(pc => pc.ApiIntegrationEnabled)
                .IsRequired();

            builder.Property(pc => pc.ApiCredentials)
                .HasMaxLength(1000);

            builder.Property(pc => pc.UpdatedAt)
                .IsRequired();

            builder.Property(pc => pc.UpdatedBy)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(pc => pc.RowVersion)
                .IsRowVersion();

            // Constraints - Only one configuration per PaymentMethod
            builder.HasIndex(pc => pc.PaymentMethod)
                .IsUnique()
                .HasDatabaseName("IX_PaymentConfiguration_PaymentMethod_Unique");

            // Table name
            builder.ToTable("PaymentConfigurations");
        }
    }
}