using DriftRide.Models;

namespace DriftRide.Models;

/// <summary>
/// Notification model for ride status changes and driver updates.
/// Sent to drivers and relevant staff when ride states change.
/// </summary>
public class RideNotification
{
    /// <summary>
    /// Gets or sets the queue entry identifier for the ride.
    /// </summary>
    public Guid QueueEntryId { get; set; }

    /// <summary>
    /// Gets or sets the queue entry information.
    /// </summary>
    public QueueEntry? QueueEntry { get; set; }

    /// <summary>
    /// Gets or sets the customer information for the ride.
    /// </summary>
    public Customer? Customer { get; set; }

    /// <summary>
    /// Gets or sets the type of ride notification.
    /// </summary>
    public RideNotificationType NotificationType { get; set; }

    /// <summary>
    /// Gets or sets the current ride status.
    /// </summary>
    public QueueEntryStatus RideStatus { get; set; }

    /// <summary>
    /// Gets or sets the driver handling the ride.
    /// </summary>
    public string? DriverUsername { get; set; }

    /// <summary>
    /// Gets or sets when the ride was started.
    /// </summary>
    public DateTime? RideStartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the ride was completed.
    /// </summary>
    public DateTime? RideCompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the duration of the ride if completed.
    /// </summary>
    public TimeSpan? RideDuration { get; set; }

    /// <summary>
    /// Gets or sets the customer's position in queue when ride started.
    /// </summary>
    public int? StartingPosition { get; set; }

    /// <summary>
    /// Gets or sets the next customer in queue after this ride.
    /// </summary>
    public QueueEntry? NextCustomer { get; set; }

    /// <summary>
    /// Gets or sets the remaining queue length after this ride.
    /// </summary>
    public int RemainingQueueLength { get; set; }

    /// <summary>
    /// Gets or sets estimated time until next customer is ready.
    /// </summary>
    public TimeSpan? EstimatedNextCustomerTime { get; set; }

    /// <summary>
    /// Gets or sets any notes about the ride completion.
    /// </summary>
    public string? RideNotes { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the notification was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether this notification requires driver attention.
    /// </summary>
    public bool RequiresDriverAttention { get; set; }

    /// <summary>
    /// Gets or sets the notification priority level.
    /// </summary>
    public NotificationPriority Priority { get; set; } = NotificationPriority.Info;

    /// <summary>
    /// Gets or sets additional context data for the ride.
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();
}

