using System.Security.Claims;

namespace DriftRide.Models
{
    /// <summary>
    /// Result of JWT token validation
    /// </summary>
    public class TokenValidationResult
    {
        /// <summary>
        /// Whether the token is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// User ID extracted from token
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Username extracted from token
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// User role extracted from token
        /// </summary>
        public UserRole? Role { get; set; }

        /// <summary>
        /// Claims principal for the user
        /// </summary>
        public ClaimsPrincipal? Principal { get; set; }

        /// <summary>
        /// Error message if validation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Token expiration time
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="username">Username</param>
        /// <param name="role">User role</param>
        /// <param name="principal">Claims principal</param>
        /// <param name="expiresAt">Token expiration</param>
        /// <returns>Valid token result</returns>
        public static TokenValidationResult Success(Guid userId, string username, UserRole role, ClaimsPrincipal principal, DateTime expiresAt)
        {
            return new TokenValidationResult
            {
                IsValid = true,
                UserId = userId,
                Username = username,
                Role = role,
                Principal = principal,
                ExpiresAt = expiresAt
            };
        }

        /// <summary>
        /// Creates a failed validation result
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <returns>Invalid token result</returns>
        public static TokenValidationResult Failure(string errorMessage)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage
            };
        }
    }
}