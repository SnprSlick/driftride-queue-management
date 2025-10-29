using DriftRide.Models;

namespace DriftRide.Models;

/// <summary>
/// Notification model for customer-related events and alerts.
/// Sent to sales staff when customers require attention or assistance.
/// </summary>
public class CustomerNotification
{
    /// <summary>
    /// Gets or sets the customer identifier.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the customer information.
    /// </summary>
    public Customer? Customer { get; set; }

    /// <summary>
    /// Gets or sets the type of customer notification.
    /// </summary>
    public CustomerNotificationType NotificationType { get; set; }

    /// <summary>
    /// Gets or sets the reason for the notification.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets detailed message about the customer situation.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification priority level.
    /// </summary>
    public NotificationPriority Priority { get; set; } = NotificationPriority.Info;

    /// <summary>
    /// Gets or sets whether this notification requires immediate staff action.
    /// </summary>
    public bool RequiresAction { get; set; }

    /// <summary>
    /// Gets or sets the suggested action for staff to take.
    /// </summary>
    public string? SuggestedAction { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the notification was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the payment information if related to payment issues.
    /// </summary>
    public Payment? RelatedPayment { get; set; }

    /// <summary>
    /// Gets or sets the queue entry information if customer is in queue.
    /// </summary>
    public QueueEntry? RelatedQueueEntry { get; set; }

    /// <summary>
    /// Gets or sets additional context data for the notification.
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets whether the notification has been acknowledged by staff.
    /// </summary>
    public bool IsAcknowledged { get; set; }

    /// <summary>
    /// Gets or sets the staff member who acknowledged the notification.
    /// </summary>
    public string? AcknowledgedBy { get; set; }

    /// <summary>
    /// Gets or sets when the notification was acknowledged.
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }
}

