namespace DriftRide.Models;

/// <summary>
/// Represents a connected SignalR client with user information.
/// Used for tracking active connections and their associated user roles.
/// </summary>
public class ConnectedClient
{
    /// <summary>
    /// Gets or sets the SignalR connection identifier.
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user identifier associated with this connection.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the connected user.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's role for this connection.
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Gets or sets when this connection was established.
    /// </summary>
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last activity timestamp for this connection.
    /// </summary>
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether this connection is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the client's IP address.
    /// </summary>
    public string? IPAddress { get; set; }

    /// <summary>
    /// Gets or sets the client's user agent string.
    /// </summary>
    public string? UserAgent { get; set; }
}