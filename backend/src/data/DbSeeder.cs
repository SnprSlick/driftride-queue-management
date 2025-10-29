using DriftRide.Data.SeedData;
using DriftRide.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DriftRide.Data
{
    /// <summary>
    /// Service responsible for seeding the database with initial data
    /// </summary>
    public class DbSeeder
    {
        private readonly DriftRideDbContext _context;
        private readonly ILogger<DbSeeder> _logger;

        /// <summary>
        /// Initializes a new instance of the DbSeeder
        /// </summary>
        /// <param name="context">Database context for Entity Framework operations</param>
        /// <param name="logger">Logger for tracking seeding operations</param>
        public DbSeeder(DriftRideDbContext context, ILogger<DbSeeder> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Seeds the database with initial data if it doesn't already exist
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operations</param>
        /// <returns>Task representing the async operation</returns>
        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting database seeding process");

                // Ensure database is created and up to date
                await _context.Database.MigrateAsync(cancellationToken);

                // Seed payment configurations
                await SeedPaymentConfigurationsAsync(cancellationToken);

                // Seed default admin user
                await SeedDefaultUsersAsync(cancellationToken);

                // Save all changes
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Database seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database seeding");
                throw;
            }
        }

        /// <summary>
        /// Seeds default payment configurations if they don't exist
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operations</param>
        /// <returns>Task representing the async operation</returns>
        private async Task SeedPaymentConfigurationsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Checking for existing payment configurations");

            // Check if any payment configurations already exist
            var existingConfigs = await _context.PaymentConfigurations
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var defaultConfigs = DefaultPaymentConfigurations.GetDefaultConfigurations();

            foreach (var defaultConfig in defaultConfigs)
            {
                // Check if configuration for this payment method already exists
                var existingConfig = existingConfigs
                    .FirstOrDefault(c => c.PaymentMethod == defaultConfig.PaymentMethod);

                if (existingConfig == null)
                {
                    _logger.LogInformation("Adding default payment configuration for {PaymentMethod}",
                        defaultConfig.PaymentMethod);

                    _context.PaymentConfigurations.Add(defaultConfig);
                }
                else
                {
                    _logger.LogInformation("Payment configuration for {PaymentMethod} already exists, skipping",
                        defaultConfig.PaymentMethod);
                }
            }
        }

        /// <summary>
        /// Seeds default admin user if no users exist
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operations</param>
        /// <returns>Task representing the async operation</returns>
        private async Task SeedDefaultUsersAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Checking for existing users");

            // Check if any users already exist
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(cancellationToken);

            if (!userExists)
            {
                _logger.LogInformation("No users found, creating default admin user");

                var defaultAdmin = DefaultUsers.GetDefaultAdminUser();
                _context.Users.Add(defaultAdmin);

                _logger.LogInformation("Default admin user created with username: {Username}",
                    defaultAdmin.Username);
            }
            else
            {
                _logger.LogInformation("Users already exist, skipping default admin user creation");
            }
        }
    }

    /// <summary>
    /// Extension methods for easier seeding service integration
    /// </summary>
    public static class DbSeederExtensions
    {
        /// <summary>
        /// Registers the DbSeeder service with the DI container
        /// </summary>
        /// <param name="services">Service collection to register with</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddDbSeeder(this IServiceCollection services)
        {
            services.AddScoped<DbSeeder>();
            return services;
        }

        /// <summary>
        /// Runs database seeding using the registered DbSeeder service
        /// </summary>
        /// <param name="serviceProvider">Service provider to resolve dependencies</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task SeedDatabaseAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
            await seeder.SeedAsync();
        }
    }
}