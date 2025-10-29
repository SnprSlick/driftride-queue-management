using DriftRide.Data;
using DriftRide.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DriftRide.Services;

/// <summary>
/// Service implementation for user authentication and management operations.
/// Handles user validation, password management, authentication, and role-based access control.
/// </summary>
public class UserService : IUserService
{
    private readonly DriftRideDbContext _context;

    // Password complexity requirements
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 255;

    /// <summary>
    /// Initializes a new instance of the UserService.
    /// </summary>
    /// <param name="context">Database context for Entity Framework operations</param>
    public UserService(DriftRideDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<User?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username, nameof(username));
        ArgumentException.ThrowIfNullOrWhiteSpace(password, nameof(password));

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() && u.IsActive, cancellationToken);

        if (user == null)
        {
            return null;
        }

        // Verify password
        if (!VerifyPassword(password, user.PasswordHash))
        {
            return null;
        }

        // Update last login timestamp
        await UpdateLastLoginAsync(user.Id, cancellationToken);

        return user;
    }

    /// <inheritdoc />
    public async Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username, nameof(username));

        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User> CreateAsync(string username, string password, string displayName, UserRole role, CancellationToken cancellationToken = default)
    {
        var validationResult = await ValidateUserDataAsync(username, displayName, password);
        if (!validationResult.IsValid)
        {
            throw new ArgumentException($"User data validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        // Check username availability
        if (!await IsUsernameAvailableAsync(username, null, cancellationToken))
        {
            throw new ArgumentException($"Username '{username}' is already taken");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username.Trim().ToLower(),
            PasswordHash = HashPassword(password),
            DisplayName = displayName.Trim(),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return user;
    }

    /// <inheritdoc />
    public async Task<User> UpdateAsync(Guid userId, string displayName, UserRole role, bool isActive, CancellationToken cancellationToken = default)
    {
        var validationResult = await ValidateUserDataAsync(string.Empty, displayName, null, userId);
        if (!validationResult.IsValid)
        {
            throw new ArgumentException($"User data validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new ArgumentException($"User with ID {userId} not found");
        }

        user.DisplayName = displayName.Trim();
        user.Role = role;
        user.IsActive = isActive;

        await _context.SaveChangesAsync(cancellationToken);

        return user;
    }

    /// <inheritdoc />
    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPassword, nameof(currentPassword));

        var passwordValidation = await ValidatePasswordComplexityAsync(newPassword);
        if (!passwordValidation.IsValid)
        {
            throw new ArgumentException($"New password validation failed: {string.Join(", ", passwordValidation.Errors)}");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new ArgumentException($"User with ID {userId} not found");
        }

        // Verify current password
        if (!VerifyPassword(currentPassword, user.PasswordHash))
        {
            return false;
        }

        // Update password
        user.PasswordHash = HashPassword(newPassword);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<List<User>> GetActiveUsersAsync(UserRole? role = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Users
            .AsNoTracking()
            .Where(u => u.IsActive);

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        return await query
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, string[] Errors)> ValidateUserDataAsync(string username, string displayName, string? password = null, Guid? existingUserId = null)
    {
        var errors = new List<string>();

        // Validate username (skip for updates without username change)
        if (!string.IsNullOrEmpty(username))
        {
            if (username.Length > 50)
            {
                errors.Add("Username cannot exceed 50 characters");
            }
            else if (!IsValidUsername(username))
            {
                errors.Add("Username must contain only alphanumeric characters and underscores");
            }
            else if (!await IsUsernameAvailableAsync(username, existingUserId))
            {
                errors.Add($"Username '{username}' is already taken");
            }
        }

        // Validate display name
        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add("Display name is required");
        }
        else if (displayName.Length > 100)
        {
            errors.Add("Display name cannot exceed 100 characters");
        }

        // Validate password if provided
        if (!string.IsNullOrEmpty(password))
        {
            var passwordValidation = await ValidatePasswordComplexityAsync(password);
            if (!passwordValidation.IsValid)
            {
                errors.AddRange(passwordValidation.Errors);
            }
        }

        return (errors.Count == 0, errors.ToArray());
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, string[] Errors)> ValidatePasswordComplexityAsync(string password)
    {
        await Task.CompletedTask; // Placeholder for async pattern consistency

        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required");
            return (false, errors.ToArray());
        }

        if (password.Length < MinPasswordLength)
        {
            errors.Add($"Password must be at least {MinPasswordLength} characters long");
        }

        if (password.Length > MaxPasswordLength)
        {
            errors.Add($"Password cannot exceed {MaxPasswordLength} characters");
        }

        if (!Regex.IsMatch(password, @"[a-z]"))
        {
            errors.Add("Password must contain at least one lowercase letter");
        }

        if (!Regex.IsMatch(password, @"[A-Z]"))
        {
            errors.Add("Password must contain at least one uppercase letter");
        }

        if (!Regex.IsMatch(password, @"\d"))
        {
            errors.Add("Password must contain at least one number");
        }

        if (!Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
        {
            errors.Add("Password must contain at least one special character");
        }

        return (errors.Count == 0, errors.ToArray());
    }

    /// <inheritdoc />
    public async Task<bool> IsUsernameAvailableAsync(string username, Guid? existingUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username, nameof(username));

        var query = _context.Users
            .AsNoTracking()
            .Where(u => u.Username.ToLower() == username.ToLower());

        if (existingUserId.HasValue)
        {
            query = query.Where(u => u.Id != existingUserId.Value);
        }

        return !await query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateLastLoginAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Validates username format using regex pattern.
    /// Allows alphanumeric characters and underscores only.
    /// </summary>
    /// <param name="username">Username to validate</param>
    /// <returns>True if format is valid, false otherwise</returns>
    private static bool IsValidUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        // Username must contain only alphanumeric characters and underscores
        return Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$");
    }

    /// <summary>
    /// Hashes a password using BCrypt algorithm.
    /// Provides secure password storage with salt.
    /// </summary>
    /// <param name="password">Plain text password to hash</param>
    /// <returns>Hashed password string</returns>
    private static string HashPassword(string password)
    {
        // Use BCrypt for secure password hashing
        return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
    }

    /// <summary>
    /// Verifies a password against a stored hash.
    /// Uses BCrypt verification for security.
    /// </summary>
    /// <param name="password">Plain text password to verify</param>
    /// <param name="hash">Stored password hash</param>
    /// <returns>True if password matches hash, false otherwise</returns>
    private static bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            // If verification fails due to invalid hash format, return false
            return false;
        }
    }
}