using DriftRide.Models;

namespace DriftRide.Models;

/// <summary>
/// Notification model for queue position changes and updates.
/// Sent to all connected clients when queue state changes.
/// </summary>
public class QueueUpdateNotification
{
    /// <summary>
    /// Gets or sets the type of queue update that occurred.
    /// </summary>
    public QueueUpdateType UpdateType { get; set; }

    /// <summary>
    /// Gets or sets the complete current queue state.
    /// </summary>
    public List<QueueEntry> QueueEntries { get; set; } = new List<QueueEntry>();

    /// <summary>
    /// Gets or sets the queue entry that was specifically affected by this update.
    /// Null if the update affects the entire queue.
    /// </summary>
    public QueueEntry? AffectedEntry { get; set; }

    /// <summary>
    /// Gets or sets the previous position for position changes.
    /// Null if not applicable to the update type.
    /// </summary>
    public int? PreviousPosition { get; set; }

    /// <summary>
    /// Gets or sets the new position for position changes.
    /// Null if not applicable to the update type.
    /// </summary>
    public int? NewPosition { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the update occurred.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets additional context information about the update.
    /// </summary>
    public string? UpdateReason { get; set; }

    /// <summary>
    /// Gets or sets the total number of customers currently in queue.
    /// </summary>
    public int TotalQueueLength { get; set; }

    /// <summary>
    /// Gets or sets the estimated wait time for new customers.
    /// </summary>
    public TimeSpan? EstimatedWaitTime { get; set; }
}