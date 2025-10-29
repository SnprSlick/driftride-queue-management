using System.ComponentModel.DataAnnotations;

namespace DriftRide.Models
{
    /// <summary>
    /// Request model for user authentication
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// Username for authentication
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Password for authentication
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string Password { get; set; } = string.Empty;
    }
}