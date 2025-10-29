namespace DriftRide.Web.Models;

/// <summary>
/// View model for the driver dashboard interface.
/// Contains current customer information and queue status.
/// </summary>
public class DriverDashboardViewModel
{
    /// <summary>
    /// Current connection status to the API and SignalR hub.
    /// </summary>
    public string ConnectionStatus { get; set; } = "Disconnected";

    /// <summary>
    /// Total number of customers in the queue.
    /// </summary>
    public int QueueLength { get; set; }

    /// <summary>
    /// Current customer for the driver to serve.
    /// Null if no customers in queue.
    /// </summary>
    public DriverCustomerViewModel? CurrentCustomer { get; set; }

    /// <summary>
    /// Preview of next 2-3 customers in queue.
    /// </summary>
    public List<DriverCustomerViewModel> UpcomingCustomers { get; set; } = new();

    /// <summary>
    /// Timestamp of last data update.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Whether the driver interface is in an active ride state.
    /// </summary>
    public bool IsRideInProgress { get; set; }

    /// <summary>
    /// Statistics for the current session.
    /// </summary>
    public DriverSessionStats SessionStats { get; set; } = new();
}

/// <summary>
/// Customer information optimized for driver interface display.
/// </summary>
public class DriverCustomerViewModel
{
    /// <summary>
    /// Queue entry ID for ride completion.
    /// </summary>
    public Guid QueueEntryId { get; set; }

    /// <summary>
    /// Customer ID for reference.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Customer name with duplicate disambiguation if needed.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Raw customer name without disambiguation.
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Customer phone number for contact if needed.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Position in queue (1 = next, 2 = after next, etc.).
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Payment method used by customer.
    /// </summary>
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>
    /// Payment amount for reference.
    /// </summary>
    public decimal PaymentAmount { get; set; }

    /// <summary>
    /// When the customer joined the queue.
    /// </summary>
    public DateTime QueuedAt { get; set; }

    /// <summary>
    /// How long the customer has been waiting.
    /// </summary>
    public TimeSpan WaitTime => DateTime.Now - QueuedAt;

    /// <summary>
    /// Formatted wait time for display (e.g., "15 min").
    /// </summary>
    public string WaitTimeDisplay
    {
        get
        {
            var wait = WaitTime;
            if (wait.TotalMinutes < 1)
                return "< 1 min";
            else if (wait.TotalMinutes < 60)
                return $"{(int)wait.TotalMinutes} min";
            else
                return $"{(int)wait.TotalHours}h {wait.Minutes}m";
        }
    }

    /// <summary>
    /// Customer arrival timestamp for duplicate name disambiguation.
    /// </summary>
    public DateTime CustomerArrivalTime { get; set; }

    /// <summary>
    /// Formatted arrival time for disambiguation (e.g., "2:15 PM").
    /// </summary>
    public string ArrivalTimeDisplay => CustomerArrivalTime.ToString("h:mm tt");

    /// <summary>
    /// Whether this customer has been waiting longer than normal (>10 min).
    /// </summary>
    public bool IsWaitingLong => WaitTime.TotalMinutes > 10;

    /// <summary>
    /// Whether this customer has been waiting an extended time (>20 min).
    /// </summary>
    public bool IsWaitingVeryLong => WaitTime.TotalMinutes > 20;
}

/// <summary>
/// Driver session statistics for performance tracking.
/// </summary>
public class DriverSessionStats
{
    /// <summary>
    /// Number of rides completed in current session.
    /// </summary>
    public int RidesCompleted { get; set; }

    /// <summary>
    /// Average time per ride completion in current session.
    /// </summary>
    public TimeSpan AverageRideTime { get; set; }

    /// <summary>
    /// Time when driver session started.
    /// </summary>
    public DateTime SessionStartTime { get; set; } = DateTime.Now;

    /// <summary>
    /// Total session duration.
    /// </summary>
    public TimeSpan SessionDuration => DateTime.Now - SessionStartTime;

    /// <summary>
    /// Rides per hour rate for current session.
    /// </summary>
    public double RidesPerHour
    {
        get
        {
            var hours = SessionDuration.TotalHours;
            return hours > 0 ? RidesCompleted / hours : 0;
        }
    }
}

/// <summary>
/// Response model for API calls from driver interface.
/// </summary>
public class DriverApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}