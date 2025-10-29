using DriftRide.Models;
using DriftRide.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace DriftRide.Services;

/// <summary>
/// Service implementation for real-time notifications using SignalR.
/// Handles queue updates, payment confirmations, and system-wide notifications.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IHubContext<QueueHub> _hubContext;

    /// <summary>
    /// Initializes a new instance of the NotificationService.
    /// </summary>
    /// <param name="hubContext">SignalR hub context for sending notifications</param>
    public NotificationService(IHubContext<QueueHub> hubContext)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
    }

    /// <inheritdoc />
    public async Task NotifyQueueUpdateAsync(List<QueueEntry> queueEntries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueEntries, nameof(queueEntries));

        var notification = new QueueUpdateNotification
        {
            UpdateType = QueueUpdateType.QueueSynced,
            QueueEntries = queueEntries,
            TotalQueueLength = queueEntries.Count,
            EstimatedWaitTime = queueEntries.Count > 0 ? TimeSpan.FromMinutes(queueEntries.Count * 5) : null
        };

        // Notify all authenticated users (sales and drivers)
        await _hubContext.Clients.Groups("Role_Sales", "Role_Driver")
            .SendAsync("QueueUpdated", notification, cancellationToken);
    }

    /// <inheritdoc />
    public async Task NotifyPaymentStatusAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment, nameof(payment));

        var notification = new PaymentNotification
        {
            PaymentId = payment.Id,
            CustomerId = payment.CustomerId,
            Customer = payment.Customer,
            Status = payment.Status,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            ExternalTransactionId = payment.ExternalTransactionId,
            IsAutoVerified = false, // All payments require manual verification currently
            ConfirmedByStaff = payment.ConfirmedBy,
            ConfirmationNotes = payment.Notes,
            ProcessedAt = payment.CreatedAt,
            StatusChangedAt = DateTime.UtcNow,
            Priority = payment.Status == PaymentStatus.Denied ? NotificationPriority.Warning : NotificationPriority.Info,
            RequiresAttention = payment.Status == PaymentStatus.Pending
        };

        // Notify sales staff about payment status changes
        await _hubContext.Clients.Group("Role_Sales")
            .SendAsync("PaymentStatusUpdated", notification, cancellationToken);

        // Notify the specific customer about their payment status
        await _hubContext.Clients.Groups($"Customer_{payment.CustomerId}", $"Payment_{payment.Id}")
            .SendAsync("PaymentStatusChanged", notification, cancellationToken);

        // If payment is confirmed, notify drivers about potential queue changes
        if (payment.Status == PaymentStatus.Confirmed)
        {
            await _hubContext.Clients.Group("Role_Driver")
                .SendAsync("PaymentConfirmed", notification, cancellationToken);

            // Also notify all customers about queue updates
            await _hubContext.Clients.Group("Customers")
                .SendAsync("QueueMayHaveChanged", new { Timestamp = DateTime.UtcNow }, cancellationToken);
        }

        // If payment is denied, send targeted notification to customer
        if (payment.Status == PaymentStatus.Denied)
        {
            await _hubContext.Clients.Groups($"Customer_{payment.CustomerId}", $"Payment_{payment.Id}")
                .SendAsync("PaymentDenied", new {
                    PaymentId = payment.Id,
                    Reason = payment.Notes,
                    Timestamp = DateTime.UtcNow
                }, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task NotifyRideStatusAsync(QueueEntry queueEntry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueEntry, nameof(queueEntry));

        var notificationType = queueEntry.Status switch
        {
            QueueEntryStatus.InProgress => RideNotificationType.RideStarted,
            QueueEntryStatus.Completed => RideNotificationType.RideCompleted,
            QueueEntryStatus.Cancelled => RideNotificationType.RideCancelled,
            _ => RideNotificationType.QueuePositionUpdate
        };

        var notification = new RideNotification
        {
            QueueEntryId = queueEntry.Id,
            QueueEntry = queueEntry,
            Customer = queueEntry.Customer,
            NotificationType = notificationType,
            RideStatus = queueEntry.Status,
            DriverUsername = queueEntry.CompletedBy,
            RideStartedAt = queueEntry.StartedAt,
            RideCompletedAt = queueEntry.CompletedAt,
            RideDuration = queueEntry.StartedAt.HasValue && queueEntry.CompletedAt.HasValue
                ? queueEntry.CompletedAt.Value - queueEntry.StartedAt.Value
                : null,
            StartingPosition = queueEntry.Position,
            Priority = queueEntry.Status == QueueEntryStatus.Cancelled ? NotificationPriority.Warning : NotificationPriority.Info,
            RequiresDriverAttention = queueEntry.Status == QueueEntryStatus.Waiting && queueEntry.Position == 1
        };

        // Notify all authenticated users about ride status changes
        await _hubContext.Clients.Groups("Role_Sales", "Role_Driver")
            .SendAsync("RideStatusUpdated", notification, cancellationToken);
    }

    /// <inheritdoc />
    public async Task NotifySystemMessageAsync(string message, UserRole? targetRole = null, NotificationPriority priority = NotificationPriority.Info, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message, nameof(message));

        var messageData = new
        {
            Message = message,
            Priority = priority.ToString(),
            Timestamp = DateTime.UtcNow,
            Type = "SystemMessage"
        };

        if (targetRole.HasValue)
        {
            // Send to specific role
            await _hubContext.Clients.Group($"Role_{targetRole}")
                .SendAsync("SystemMessage", messageData, cancellationToken);
        }
        else
        {
            // Send to all authenticated users
            await _hubContext.Clients.Groups("Role_Sales", "Role_Driver")
                .SendAsync("SystemMessage", messageData, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task NotifyCustomerAttentionAsync(Customer customer, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer, nameof(customer));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        var notificationType = reason.ToLowerInvariant() switch
        {
            var r when r.Contains("payment") && r.Contains("fail") => CustomerNotificationType.PaymentVerificationRequired,
            var r when r.Contains("payment") && r.Contains("denied") => CustomerNotificationType.PaymentDenied,
            var r when r.Contains("manual") => CustomerNotificationType.ManuallyAdded,
            var r when r.Contains("assistance") => CustomerNotificationType.AssistanceRequested,
            var r when r.Contains("wait") => CustomerNotificationType.ExtendedWaitTime,
            _ => CustomerNotificationType.CustomerArrived
        };

        var notification = new CustomerNotification
        {
            CustomerId = customer.Id,
            Customer = customer,
            NotificationType = notificationType,
            Reason = reason,
            Message = $"Customer {customer.Name} requires attention: {reason}",
            Priority = notificationType switch
            {
                CustomerNotificationType.PaymentDenied => NotificationPriority.Warning,
                CustomerNotificationType.PaymentVerificationRequired => NotificationPriority.Warning,
                CustomerNotificationType.SystemError => NotificationPriority.Error,
                _ => NotificationPriority.Info
            },
            RequiresAction = true,
            SuggestedAction = notificationType switch
            {
                CustomerNotificationType.PaymentVerificationRequired => "Verify payment manually",
                CustomerNotificationType.PaymentDenied => "Assist with payment method",
                CustomerNotificationType.AssistanceRequested => "Provide customer assistance",
                _ => "Check customer status"
            }
        };

        // Notify sales staff about customers requiring attention
        await _hubContext.Clients.Group("Role_Sales")
            .SendAsync("CustomerAttention", notification, cancellationToken);
    }

    /// <inheritdoc />
    public async Task NotifyDriverQueueUpdateAsync(QueueEntry? nextCustomer, int queueLength, CancellationToken cancellationToken = default)
    {
        var driverData = new
        {
            NextCustomer = nextCustomer != null ? new
            {
                nextCustomer.Id,
                nextCustomer.Position,
                nextCustomer.Status,
                Customer = nextCustomer.Customer != null ? new
                {
                    nextCustomer.Customer.Id,
                    nextCustomer.Customer.Name,
                    nextCustomer.Customer.PhoneNumber
                } : null
            } : null,
            QueueLength = queueLength,
            UpdatedAt = DateTime.UtcNow
        };

        // Notify drivers about queue updates
        await _hubContext.Clients.Group("Role_Driver")
            .SendAsync("DriverQueueUpdated", driverData, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendUserNotificationAsync(Guid userId, string message, string notificationType, object? data = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message, nameof(message));
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationType, nameof(notificationType));

        var notificationData = new
        {
            Message = message,
            Type = notificationType,
            Data = data,
            Timestamp = DateTime.UtcNow
        };

        // Send to specific user
        await _hubContext.Clients.Group($"User_{userId}")
            .SendAsync("UserNotification", notificationData, cancellationToken);
    }

    /// <inheritdoc />
    public async Task NotifyConfigurationChangeAsync(string configType, object configData, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configType, nameof(configType));
        ArgumentNullException.ThrowIfNull(configData, nameof(configData));

        var changeData = new
        {
            ConfigType = configType,
            ConfigData = configData,
            UpdatedAt = DateTime.UtcNow,
            Type = "ConfigurationChange"
        };

        // Notify all authenticated users about configuration changes
        await _hubContext.Clients.Groups("Role_Sales", "Role_Driver")
            .SendAsync("ConfigurationChanged", changeData, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<ConnectedClient>> GetConnectedClientsAsync(UserRole? role = null, CancellationToken cancellationToken = default)
    {
        // Note: SignalR doesn't provide a built-in way to get connected clients with their user info
        // In a production system, you would need to maintain this information separately
        // For now, return empty list as this would require additional tracking infrastructure
        await Task.CompletedTask;
        return new List<ConnectedClient>();
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, string[] Errors)> ValidateNotificationAsync(string notificationType, string content, UserRole? targetRole = null)
    {
        await Task.CompletedTask; // Placeholder for async pattern consistency

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(notificationType))
        {
            errors.Add("Notification type is required");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add("Notification content is required");
        }
        else if (content.Length > 1000)
        {
            errors.Add("Notification content cannot exceed 1000 characters");
        }

        // Validate notification type
        var validTypes = new[]
        {
            "QueueUpdated",
            "PaymentStatusUpdated",
            "RideStatusUpdated",
            "SystemMessage",
            "CustomerAttention",
            "DriverQueueUpdated",
            "UserNotification",
            "ConfigurationChanged"
        };

        if (!string.IsNullOrEmpty(notificationType) && !validTypes.Contains(notificationType))
        {
            errors.Add($"Invalid notification type: {notificationType}");
        }

        return (errors.Count == 0, errors.ToArray());
    }

    /// <summary>
    /// Notifies sales staff about new payment submissions requiring verification.
    /// </summary>
    /// <param name="payment">The payment requiring verification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task NotifyNewPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment, nameof(payment));

        var notification = new
        {
            PaymentId = payment.Id,
            CustomerId = payment.CustomerId,
            CustomerName = payment.Customer?.Name,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            SubmittedAt = payment.CreatedAt,
            RequiresAttention = true,
            Priority = NotificationPriority.Info.ToString(),
            Type = "NewPayment"
        };

        // Notify sales staff with sound alert
        await _hubContext.Clients.Group("Role_Sales")
            .SendAsync("NewPayment", notification, cancellationToken);
    }

    /// <summary>
    /// Notifies customer about successful queue addition after payment confirmation.
    /// </summary>
    /// <param name="customerId">Customer ID</param>
    /// <param name="queuePosition">Position in queue</param>
    /// <param name="estimatedWaitTime">Estimated wait time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task NotifyCustomerQueuePositionAsync(Guid customerId, int queuePosition, TimeSpan? estimatedWaitTime = null, CancellationToken cancellationToken = default)
    {
        var notification = new
        {
            CustomerId = customerId,
            QueuePosition = queuePosition,
            EstimatedWaitTime = estimatedWaitTime,
            UpdatedAt = DateTime.UtcNow,
            Type = "QueuePositionUpdate"
        };

        // Notify the specific customer
        await _hubContext.Clients.Group($"Customer_{customerId}")
            .SendAsync("QueuePositionUpdated", notification, cancellationToken);
    }

    /// <summary>
    /// Notifies sales staff about customers requiring immediate attention.
    /// </summary>
    /// <param name="customerId">Customer ID</param>
    /// <param name="alertType">Type of alert</param>
    /// <param name="message">Alert message</param>
    /// <param name="priority">Alert priority</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task NotifyCustomerAlertAsync(Guid customerId, string alertType, string message, NotificationPriority priority = NotificationPriority.Warning, CancellationToken cancellationToken = default)
    {
        var notification = new
        {
            CustomerId = customerId,
            AlertType = alertType,
            Message = message,
            Priority = priority.ToString(),
            RequiresAction = true,
            Timestamp = DateTime.UtcNow,
            Type = "CustomerAlert"
        };

        // Notify sales staff with high priority sound
        await _hubContext.Clients.Group("Role_Sales")
            .SendAsync("CustomerAlert", notification, cancellationToken);
    }

    /// <summary>
    /// Broadcasts real-time queue statistics to all staff.
    /// </summary>
    /// <param name="totalInQueue">Total customers in queue</param>
    /// <param name="pendingPayments">Number of pending payments</param>
    /// <param name="averageWaitTime">Average wait time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task NotifyQueueStatisticsAsync(int totalInQueue, int pendingPayments, TimeSpan? averageWaitTime = null, CancellationToken cancellationToken = default)
    {
        var notification = new
        {
            TotalInQueue = totalInQueue,
            PendingPayments = pendingPayments,
            AverageWaitTime = averageWaitTime,
            UpdatedAt = DateTime.UtcNow,
            Type = "QueueStatistics"
        };

        // Notify all staff
        await _hubContext.Clients.Groups("Role_Sales", "Role_Driver")
            .SendAsync("QueueStatisticsUpdated", notification, cancellationToken);
    }

    /// <summary>
    /// Notifies about connection quality issues or service disruptions.
    /// </summary>
    /// <param name="issueType">Type of issue</param>
    /// <param name="message">Issue description</param>
    /// <param name="severity">Issue severity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task NotifyServiceIssueAsync(string issueType, string message, NotificationPriority severity = NotificationPriority.Warning, CancellationToken cancellationToken = default)
    {
        var notification = new
        {
            IssueType = issueType,
            Message = message,
            Severity = severity.ToString(),
            Timestamp = DateTime.UtcNow,
            Type = "ServiceIssue"
        };

        // Notify all connected clients
        await _hubContext.Clients.All
            .SendAsync("ServiceIssue", notification, cancellationToken);
    }
}