using DriftRide.Models;

namespace DriftRide.Data.SeedData
{
    /// <summary>
    /// Provides default user data for application seeding
    /// </summary>
    public static class DefaultUsers
    {
        /// <summary>
        /// Gets default admin user with properly hashed password
        /// </summary>
        /// <returns>User entity with admin credentials</returns>
        public static User GetDefaultAdminUser()
        {
            return new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                PasswordHash = HashPassword("DriftRide123!"),
                DisplayName = "System Administrator",
                Email = "admin@driftride.com",
                Role = UserRole.Sales,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = null
            };
        }

        /// <summary>
        /// Hashes a password using BCrypt algorithm for secure storage
        /// </summary>
        /// <param name="password">Plain text password to hash</param>
        /// <returns>Hashed password string</returns>
        private static string HashPassword(string password)
        {
            // Use BCrypt for secure password hashing with salt factor of 12
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }
    }
}