using DriftRide.Models;

namespace DriftRide.Models;

/// <summary>
/// Notification model for payment status changes and confirmations.
/// Sent to relevant clients when payment verification occurs.
/// </summary>
public class PaymentNotification
{
    /// <summary>
    /// Gets or sets the payment identifier.
    /// </summary>
    public Guid PaymentId { get; set; }

    /// <summary>
    /// Gets or sets the customer identifier associated with the payment.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the customer information for display purposes.
    /// </summary>
    public Customer? Customer { get; set; }

    /// <summary>
    /// Gets or sets the payment status that triggered this notification.
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the payment amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the payment method used.
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets the external transaction identifier if available.
    /// </summary>
    public string? ExternalTransactionId { get; set; }

    /// <summary>
    /// Gets or sets whether the payment was automatically verified.
    /// </summary>
    public bool IsAutoVerified { get; set; }

    /// <summary>
    /// Gets or sets the staff member who confirmed the payment (if manual).
    /// </summary>
    public string? ConfirmedByStaff { get; set; }

    /// <summary>
    /// Gets or sets additional notes about the payment confirmation.
    /// </summary>
    public string? ConfirmationNotes { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the payment was processed.
    /// </summary>
    public DateTime ProcessedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the status changed.
    /// </summary>
    public DateTime StatusChangedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether the customer was automatically added to queue.
    /// </summary>
    public bool AddedToQueue { get; set; }

    /// <summary>
    /// Gets or sets the queue position if customer was added to queue.
    /// </summary>
    public int? QueuePosition { get; set; }

    /// <summary>
    /// Gets or sets any error message if payment verification failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the notification priority level.
    /// </summary>
    public NotificationPriority Priority { get; set; } = NotificationPriority.Info;

    /// <summary>
    /// Gets or sets whether this notification requires immediate attention.
    /// </summary>
    public bool RequiresAttention { get; set; }
}