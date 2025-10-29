using DriftRide.Models;

namespace DriftRide.Services;

/// <summary>
/// Service interface for user authentication and management operations.
/// Handles user validation, password management, and role-based access control.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Authenticates a user with username and password.
    /// Validates credentials and updates last login timestamp.
    /// </summary>
    /// <param name="username">Username for authentication</param>
    /// <param name="password">Plain text password to verify</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Authenticated user if credentials are valid, null otherwise</returns>
    /// <exception cref="ArgumentNullException">Thrown when username or password is null</exception>
    Task<User?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user by unique identifier.
    /// </summary>
    /// <param name="userId">Unique user identifier</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>User if found, null otherwise</returns>
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user by username for authentication and authorization.
    /// </summary>
    /// <param name="username">Username to search for</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>User if found, null otherwise</returns>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user account with specified role.
    /// Hashes password and validates username uniqueness.
    /// </summary>
    /// <param name="username">Unique username (max 50 chars, alphanumeric)</param>
    /// <param name="password">Plain text password for hashing</param>
    /// <param name="displayName">Display name for user interface (max 100 chars)</param>
    /// <param name="role">User role (Sales or Driver)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Created user with hashed password</returns>
    /// <exception cref="ArgumentException">Thrown when username already exists or validation fails</exception>
    Task<User> CreateAsync(string username, string password, string displayName, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates user information (display name, role, active status).
    /// Password changes require separate method for security.
    /// </summary>
    /// <param name="userId">User to update</param>
    /// <param name="displayName">New display name</param>
    /// <param name="role">New user role</param>
    /// <param name="isActive">Account active status</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Updated user information</returns>
    /// <exception cref="ArgumentException">Thrown when user not found or validation fails</exception>
    Task<User> UpdateAsync(Guid userId, string displayName, UserRole role, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes user password with current password verification.
    /// Enforces password complexity requirements.
    /// </summary>
    /// <param name="userId">User changing password</param>
    /// <param name="currentPassword">Current password for verification</param>
    /// <param name="newPassword">New password to set</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if password changed successfully, false if current password invalid</returns>
    /// <exception cref="ArgumentException">Thrown when new password doesn't meet complexity requirements</exception>
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active users for administrative purposes.
    /// Excludes password hashes from results.
    /// </summary>
    /// <param name="role">Optional role filter (null for all roles)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>List of active users matching criteria</returns>
    Task<List<User>> GetActiveUsersAsync(UserRole? role = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates user data before creation or update.
    /// Ensures username uniqueness and business rule compliance.
    /// </summary>
    /// <param name="username">Username to validate</param>
    /// <param name="displayName">Display name to validate</param>
    /// <param name="password">Password to validate (null for updates without password change)</param>
    /// <param name="existingUserId">User ID for updates (null for new users)</param>
    /// <returns>Validation result with any error messages</returns>
    Task<(bool IsValid, string[] Errors)> ValidateUserDataAsync(string username, string displayName, string? password = null, Guid? existingUserId = null);

    /// <summary>
    /// Validates password complexity requirements.
    /// Enforces minimum length, character diversity, and security rules.
    /// </summary>
    /// <param name="password">Password to validate</param>
    /// <returns>Validation result with specific password requirement failures</returns>
    Task<(bool IsValid, string[] Errors)> ValidatePasswordComplexityAsync(string password);

    /// <summary>
    /// Checks if username is available for new account creation.
    /// </summary>
    /// <param name="username">Username to check availability</param>
    /// <param name="existingUserId">User ID to exclude from check (for updates)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if username is available, false if taken</returns>
    Task<bool> IsUsernameAvailableAsync(string username, Guid? existingUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates last login timestamp for authenticated user.
    /// Used for tracking user activity and session management.
    /// </summary>
    /// <param name="userId">User who logged in</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    Task UpdateLastLoginAsync(Guid userId, CancellationToken cancellationToken = default);
}