namespace DriftRide.Models
{
    /// <summary>
    /// Supported payment methods for ride payments
    /// </summary>
    public enum PaymentMethod
    {
        CashApp,
        PayPal,
        CashInHand
    }

    /// <summary>
    /// Payment verification status
    /// </summary>
    public enum PaymentStatus
    {
        Pending,
        Confirmed,
        Denied
    }

    /// <summary>
    /// Customer queue entry status
    /// </summary>
    public enum QueueEntryStatus
    {
        Waiting,
        InProgress,
        Completed,
        Cancelled
    }

    /// <summary>
    /// User role for access control
    /// </summary>
    public enum UserRole
    {
        Sales,
        Driver
    }


    /// <summary>
    /// Notification priority levels for system messages
    /// </summary>
    public enum NotificationPriority
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Types of ride-related notifications
    /// </summary>
    public enum RideNotificationType
    {
        RideStarted,
        RideCompleted,
        RideCancelled,
        QueuePositionUpdate
    }

    /// <summary>
    /// Types of customer-related notifications
    /// </summary>
    public enum CustomerNotificationType
    {
        PaymentVerificationRequired,
        PaymentDenied,
        ManuallyAdded,
        AssistanceRequested,
        ExtendedWaitTime,
        CustomerArrived,
        SystemError
    }

    /// <summary>
    /// Types of queue update notifications
    /// </summary>
    public enum QueueUpdateType
    {
        /// <summary>
        /// A new customer was added to the queue.
        /// </summary>
        CustomerAdded = 1,

        /// <summary>
        /// A customer was removed from the queue.
        /// </summary>
        CustomerRemoved = 2,

        /// <summary>
        /// Queue positions were manually reordered.
        /// </summary>
        QueueReordered = 3,

        /// <summary>
        /// A ride was started for a customer.
        /// </summary>
        RideStarted = 4,

        /// <summary>
        /// A ride was completed for a customer.
        /// </summary>
        RideCompleted = 5,

        /// <summary>
        /// Customer moved up in queue due to completion.
        /// </summary>
        PositionAdvanced = 6,

        /// <summary>
        /// Complete queue refresh from desktop sync.
        /// </summary>
        QueueSynced = 7
    }
}