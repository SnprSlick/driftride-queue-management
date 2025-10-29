using DriftRide.Models;

namespace DriftRide.Services;

/// <summary>
/// Service interface for real-time notifications using SignalR.
/// Handles queue updates, payment confirmations, and system-wide notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Notifies all connected clients about queue changes.
    /// Broadcasts updated queue state to sales staff and driver interfaces.
    /// </summary>
    /// <param name="queueEntries">Current queue state to broadcast</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async notification operation</returns>
    Task NotifyQueueUpdateAsync(List<QueueEntry> queueEntries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies relevant clients about payment status changes.
    /// Informs sales staff and customer interfaces about payment confirmations.
    /// </summary>
    /// <param name="payment">Payment with updated status</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async notification operation</returns>
    Task NotifyPaymentStatusAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies driver interface about ride status changes.
    /// Updates current customer information and ride progression.
    /// </summary>
    /// <param name="queueEntry">Queue entry with ride status update</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async notification operation</returns>
    Task NotifyRideStatusAsync(QueueEntry queueEntry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies specific user groups about system-wide messages.
    /// Sends alerts, maintenance notices, or operational updates.
    /// </summary>
    /// <param name="message">Message content to broadcast</param>
    /// <param name="targetRole">Target user role (null for all users)</param>
    /// <param name="priority">Message priority level (Info, Warning, Error)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async notification operation</returns>
    Task NotifySystemMessageAsync(string message, UserRole? targetRole = null, NotificationPriority priority = NotificationPriority.Info, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies sales staff about new customer arrivals requiring attention.
    /// Alerts when customers need manual payment verification or assistance.
    /// </summary>
    /// <param name="customer">Customer requiring attention</param>
    /// <param name="reason">Reason for notification (e.g., payment failed, manual review needed)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async notification operation</returns>
    Task NotifyCustomerAttentionAsync(Customer customer, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies drivers about queue position changes and next customer updates.
    /// Provides real-time updates for driver workflow optimization.
    /// </summary>
    /// <param name="nextCustomer">Next customer in queue (null if queue empty)</param>
    /// <param name="queueLength">Current queue length</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async notification operation</returns>
    Task NotifyDriverQueueUpdateAsync(QueueEntry? nextCustomer, int queueLength, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends targeted notification to specific connected user.
    /// Used for personalized messages or user-specific alerts.
    /// </summary>
    /// <param name="userId">Target user identifier</param>
    /// <param name="message">Message content</param>
    /// <param name="notificationType">Type of notification for client handling</param>
    /// <param name="data">Additional data payload (optional)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async notification operation</returns>
    Task SendUserNotificationAsync(Guid userId, string message, string notificationType, object? data = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts configuration changes to all connected clients.
    /// Notifies about payment method updates, pricing changes, etc.
    /// </summary>
    /// <param name="configType">Type of configuration that changed</param>
    /// <param name="configData">Updated configuration data</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async notification operation</returns>
    Task NotifyConfigurationChangeAsync(string configType, object configData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves list of currently connected clients by role.
    /// Used for monitoring active users and targeted messaging.
    /// </summary>
    /// <param name="role">User role to filter by (null for all roles)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>List of connected client information</returns>
    Task<List<ConnectedClient>> GetConnectedClientsAsync(UserRole? role = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates notification data before sending.
    /// Ensures message content and targets are appropriate.
    /// </summary>
    /// <param name="notificationType">Type of notification to validate</param>
    /// <param name="content">Message content to validate</param>
    /// <param name="targetRole">Target role to validate</param>
    /// <returns>Validation result with any error messages</returns>
    Task<(bool IsValid, string[] Errors)> ValidateNotificationAsync(string notificationType, string content, UserRole? targetRole = null);
}

/// <summary>
/// Represents notification priority levels for client handling.
/// </summary>
public enum NotificationPriority
{
    /// <summary>
    /// Informational message, no urgent action required.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning message, attention recommended.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error message, immediate attention required.
    /// </summary>
    Error = 2,

    /// <summary>
    /// Critical system message, urgent action required.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Represents information about a connected SignalR client.
/// </summary>
public class ConnectedClient
{
    /// <summary>
    /// Gets or sets the SignalR connection identifier.
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authenticated user identifier.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Gets or sets the user role for the connection.
    /// </summary>
    public UserRole? UserRole { get; set; }

    /// <summary>
    /// Gets or sets the connection establishment timestamp.
    /// </summary>
    public DateTime ConnectedAt { get; set; }

    /// <summary>
    /// Gets or sets the last activity timestamp.
    /// </summary>
    public DateTime LastActivity { get; set; }
}