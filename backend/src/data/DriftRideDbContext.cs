using Microsoft.EntityFrameworkCore;
using DriftRide.Models;
using DriftRide.Data.Configurations;

namespace DriftRide.Data
{
    /// <summary>
    /// Entity Framework DbContext for DriftRide application
    /// </summary>
    public class DriftRideDbContext : DbContext
    {
        public DriftRideDbContext(DbContextOptions<DriftRideDbContext> options) : base(options)
        {
        }

        // DbSet properties for all entities
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<QueueEntry> QueueEntries { get; set; }
        public DbSet<Models.PaymentConfiguration> PaymentConfigurations { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply entity configurations
            modelBuilder.ApplyConfiguration(new CustomerConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.PaymentConfiguration());
            modelBuilder.ApplyConfiguration(new QueueEntryConfiguration());
            modelBuilder.ApplyConfiguration(new PaymentConfigurationEntityConfiguration());
            modelBuilder.ApplyConfiguration(new UserConfiguration());

            // Configure enum to string conversions
            modelBuilder.Entity<Payment>()
                .Property(p => p.PaymentMethod)
                .HasConversion<string>();

            modelBuilder.Entity<Payment>()
                .Property(p => p.Status)
                .HasConversion<string>();

            modelBuilder.Entity<QueueEntry>()
                .Property(q => q.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Models.PaymentConfiguration>()
                .Property(pc => pc.PaymentMethod)
                .HasConversion<string>();

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();
        }
    }
}