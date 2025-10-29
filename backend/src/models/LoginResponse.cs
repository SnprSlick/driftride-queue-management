namespace DriftRide.Models
{
    /// <summary>
    /// Response model for successful authentication
    /// </summary>
    public class LoginResponse
    {
        /// <summary>
        /// JWT access token
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Refresh token for token renewal
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// Token expiration timestamp
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// User information
        /// </summary>
        public UserInfo User { get; set; } = null!;
    }

    /// <summary>
    /// User information for login response
    /// </summary>
    public class UserInfo
    {
        /// <summary>
        /// User ID
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Username
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Display name
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// User role
        /// </summary>
        public UserRole Role { get; set; }
    }
}