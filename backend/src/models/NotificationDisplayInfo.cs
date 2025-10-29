using DriftRide.Models;

namespace DriftRide.Models;

/// <summary>
/// Notification display information for UI rendering.
/// Provides consistent notification display across different interfaces.
/// </summary>
public class NotificationDisplayInfo
{
    /// <summary>
    /// Gets or sets the notification identifier.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the notification type for UI handling.
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Gets or sets the notification title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification message content.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification priority level.
    /// </summary>
    public NotificationPriority Priority { get; set; } = NotificationPriority.Info;

    /// <summary>
    /// Gets or sets whether this notification requires user action.
    /// </summary>
    public bool RequiresAction { get; set; }

    /// <summary>
    /// Gets or sets whether this notification should auto-dismiss.
    /// </summary>
    public bool AutoDismiss { get; set; } = true;

    /// <summary>
    /// Gets or sets the auto-dismiss timeout in milliseconds.
    /// </summary>
    public int AutoDismissTimeout { get; set; } = 5000;

    /// <summary>
    /// Gets or sets whether this notification should play a sound.
    /// </summary>
    public bool PlaySound { get; set; }

    /// <summary>
    /// Gets or sets the sound type to play.
    /// </summary>
    public SoundType SoundType { get; set; } = SoundType.Default;

    /// <summary>
    /// Gets or sets the timestamp when the notification was created.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets additional data associated with the notification.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Gets or sets the visual style for the notification.
    /// </summary>
    public NotificationStyle Style { get; set; } = NotificationStyle.Default;

    /// <summary>
    /// Gets or sets whether the notification should show with animation.
    /// </summary>
    public bool ShowAnimation { get; set; } = true;

    /// <summary>
    /// Gets or sets action buttons for the notification.
    /// </summary>
    public List<NotificationAction> Actions { get; set; } = new();
}

/// <summary>
/// Enumeration of notification types for UI handling.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Payment status update notification.
    /// </summary>
    PaymentStatusUpdate,

    /// <summary>
    /// New payment submission notification.
    /// </summary>
    NewPayment,

    /// <summary>
    /// Queue position update notification.
    /// </summary>
    QueuePositionUpdate,

    /// <summary>
    /// Customer alert notification.
    /// </summary>
    CustomerAlert,

    /// <summary>
    /// System message notification.
    /// </summary>
    SystemMessage,

    /// <summary>
    /// Configuration change notification.
    /// </summary>
    ConfigurationChange,

    /// <summary>
    /// Connection status notification.
    /// </summary>
    ConnectionStatus,

    /// <summary>
    /// Service issue notification.
    /// </summary>
    ServiceIssue
}

/// <summary>
/// Enumeration of sound types for notifications.
/// </summary>
public enum SoundType
{
    /// <summary>
    /// Default notification sound.
    /// </summary>
    Default,

    /// <summary>
    /// Success operation sound.
    /// </summary>
    Success,

    /// <summary>
    /// Warning alert sound.
    /// </summary>
    Warning,

    /// <summary>
    /// Error alert sound.
    /// </summary>
    Error,

    /// <summary>
    /// High priority alert sound.
    /// </summary>
    Critical,

    /// <summary>
    /// New item alert sound.
    /// </summary>
    NewItem,

    /// <summary>
    /// No sound.
    /// </summary>
    Silent
}

/// <summary>
/// Enumeration of notification visual styles.
/// </summary>
public enum NotificationStyle
{
    /// <summary>
    /// Default notification style.
    /// </summary>
    Default,

    /// <summary>
    /// Compact notification style.
    /// </summary>
    Compact,

    /// <summary>
    /// Expanded notification style with more details.
    /// </summary>
    Expanded,

    /// <summary>
    /// Banner style notification.
    /// </summary>
    Banner,

    /// <summary>
    /// Toast style notification.
    /// </summary>
    Toast,

    /// <summary>
    /// Modal style notification that requires interaction.
    /// </summary>
    Modal
}

/// <summary>
/// Action button for notifications.
/// </summary>
public class NotificationAction
{
    /// <summary>
    /// Gets or sets the action identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action label text.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action type.
    /// </summary>
    public ActionType Type { get; set; } = ActionType.Default;

    /// <summary>
    /// Gets or sets whether this action is the primary action.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Gets or sets additional data for the action.
    /// </summary>
    public object? Data { get; set; }
}

/// <summary>
/// Enumeration of action types.
/// </summary>
public enum ActionType
{
    /// <summary>
    /// Default action.
    /// </summary>
    Default,

    /// <summary>
    /// Approve action.
    /// </summary>
    Approve,

    /// <summary>
    /// Deny action.
    /// </summary>
    Deny,

    /// <summary>
    /// Dismiss action.
    /// </summary>
    Dismiss,

    /// <summary>
    /// View details action.
    /// </summary>
    ViewDetails,

    /// <summary>
    /// Retry action.
    /// </summary>
    Retry,

    /// <summary>
    /// Navigate action.
    /// </summary>
    Navigate
}