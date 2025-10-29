using System.ComponentModel.DataAnnotations;

namespace DriftRide.Models
{
    /// <summary>
    /// Represents sales staff and drivers with role-based access
    /// </summary>
    public class User
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Unique login identifier, max 50 chars
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Hashed password, max 255 chars
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Full name for display, max 100 chars
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Email address, max 255 chars
        /// </summary>
        [Required]
        [MaxLength(255)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User role for access control
        /// </summary>
        [Required]
        public UserRole Role { get; set; }

        /// <summary>
        /// Account status
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Last successful login
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Account creation date
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Concurrency token
        /// </summary>
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
    }
}