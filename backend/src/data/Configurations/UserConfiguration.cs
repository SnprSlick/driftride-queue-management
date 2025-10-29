using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DriftRide.Models;

namespace DriftRide.Data.Configurations
{
    /// <summary>
    /// Entity Framework configuration for User entity
    /// </summary>
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            // Primary key
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id)
                .ValueGeneratedOnAdd();

            // Properties
            builder.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(u => u.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(u => u.Role)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(u => u.IsActive)
                .IsRequired();

            builder.Property(u => u.LastLoginAt);

            builder.Property(u => u.CreatedAt)
                .IsRequired();

            builder.Property(u => u.RowVersion)
                .IsRowVersion();

            // Constraints - Username must be unique
            builder.HasIndex(u => u.Username)
                .IsUnique()
                .HasDatabaseName("IX_User_Username_Unique");

            // Table name
            builder.ToTable("Users");
        }
    }
}