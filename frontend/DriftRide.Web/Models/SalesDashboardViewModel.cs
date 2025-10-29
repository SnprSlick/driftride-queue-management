using System.ComponentModel.DataAnnotations;
using DriftRide.Web.Services;

namespace DriftRide.Web.Models;

/// <summary>
/// View model for the sales dashboard interface optimized for payment verification and queue management
/// </summary>
public class SalesDashboardViewModel
{
    /// <summary>
    /// API base URL for backend calls
    /// </summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// SignalR hub URL for real-time updates
    /// </summary>
    public string SignalRHubUrl { get; set; } = string.Empty;

    /// <summary>
    /// Current authenticated user's username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Current authenticated user's display name
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Current authenticated user's role (should be "Sales")
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// List of payments pending confirmation
    /// </summary>
    public List<PendingPaymentResponse> PendingPayments { get; set; } = new List<PendingPaymentResponse>();

    /// <summary>
    /// Current queue entries
    /// </summary>
    public List<QueueEntry> QueueEntries { get; set; } = new List<QueueEntry>();

    /// <summary>
    /// Total number of pending payments
    /// </summary>
    public int TotalPendingCount { get; set; }

    /// <summary>
    /// Total number of customers in queue
    /// </summary>
    public int TotalInQueue { get; set; }

    /// <summary>
    /// Number of payments waiting more than 5 minutes (performance metric)
    /// </summary>
    public int PaymentsOver5Minutes { get; set; }

    /// <summary>
    /// Number of payments waiting more than 10 minutes (alert threshold)
    /// </summary>
    public int PaymentsOver10Minutes { get; set; }

    /// <summary>
    /// Error messages for display
    /// </summary>
    public List<string> ErrorMessages { get; set; } = new List<string>();

    /// <summary>
    /// Current dashboard view mode (pending, queue, search)
    /// </summary>
    public string CurrentView { get; set; } = "pending";

    /// <summary>
    /// Search term for customer lookup
    /// </summary>
    public string SearchTerm { get; set; } = string.Empty;

    /// <summary>
    /// Customer search results
    /// </summary>
    public List<CustomerResponse> SearchResults { get; set; } = new List<CustomerResponse>();

    /// <summary>
    /// Flag indicating if real-time updates are enabled
    /// </summary>
    public bool RealTimeUpdatesEnabled { get; set; } = true;

    /// <summary>
    /// Auto-refresh interval in seconds (default 30 seconds for real-time sync)
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Keyboard shortcuts enabled flag
    /// </summary>
    public bool KeyboardShortcutsEnabled { get; set; } = true;

    /// <summary>
    /// Sound alerts enabled flag
    /// </summary>
    public bool SoundAlertsEnabled { get; set; } = true;

    /// <summary>
    /// Gets priority payments (over 10 minutes waiting) for urgent attention
    /// </summary>
    public List<PendingPaymentResponse> PriorityPayments =>
        PendingPayments.Where(p => p.MinutesWaiting > 10).OrderByDescending(p => p.MinutesWaiting).ToList();

    /// <summary>
    /// Gets recent payments (under 5 minutes) for normal processing
    /// </summary>
    public List<PendingPaymentResponse> RecentPayments =>
        PendingPayments.Where(p => p.MinutesWaiting <= 5).OrderBy(p => p.CreatedAt).ToList();

    /// <summary>
    /// Gets moderate priority payments (5-10 minutes)
    /// </summary>
    public List<PendingPaymentResponse> ModeratePayments =>
        PendingPayments.Where(p => p.MinutesWaiting > 5 && p.MinutesWaiting <= 10).OrderBy(p => p.CreatedAt).ToList();

    /// <summary>
    /// Check if dashboard has any urgent payments requiring immediate attention
    /// </summary>
    public bool HasUrgentPayments => PaymentsOver10Minutes > 0;

    /// <summary>
    /// Check if dashboard is performing well (under 30-second average processing target)
    /// </summary>
    public bool IsPerformingWell => PendingPayments.Count <= 5 && PaymentsOver5Minutes <= 2;

    /// <summary>
    /// Get performance status indicator
    /// </summary>
    public string PerformanceStatus
    {
        get
        {
            if (HasUrgentPayments) return "critical";
            if (PaymentsOver5Minutes > 3) return "warning";
            if (IsPerformingWell) return "good";
            return "normal";
        }
    }

    /// <summary>
    /// Get next customer in queue for driver handoff
    /// </summary>
    public QueueEntry? NextCustomer =>
        QueueEntries.Where(q => q.Status == "Waiting").OrderBy(q => q.Position).FirstOrDefault();

    /// <summary>
    /// Count of customers currently being served
    /// </summary>
    public int CustomersInProgress =>
        QueueEntries.Count(q => q.Status == "InProgress");
}

/// <summary>
/// View model for manual customer addition form
/// </summary>
public class ManualCustomerAdditionViewModel
{
    [Required(ErrorMessage = "Customer name is required")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    [Display(Name = "Customer Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required")]
    [Phone(ErrorMessage = "Please enter a valid phone number")]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Reason for manual addition is required")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    [Display(Name = "Reason for Manual Addition")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Common reasons for manual customer addition
    /// </summary>
    public static readonly List<string> CommonReasons = new List<string>
    {
        "Payment method temporarily unavailable",
        "Customer preferred cash payment",
        "Payment app technical issues",
        "Customer requested assistance",
        "Payment gateway error",
        "Customer without smartphone",
        "Group booking adjustment"
    };
}

/// <summary>
/// View model for payment verification workflow
/// </summary>
public class PaymentVerificationViewModel
{
    public PendingPaymentResponse Payment { get; set; } = new PendingPaymentResponse();

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    [Display(Name = "Verification Notes")]
    public string VerificationNotes { get; set; } = string.Empty;

    public bool QuickConfirm { get; set; } = false;

    /// <summary>
    /// Pre-defined verification notes for quick selection
    /// </summary>
    public static readonly List<string> QuickNotes = new List<string>
    {
        "Payment verified successfully",
        "Transaction ID confirmed",
        "Payment amount matches",
        "Customer present and confirmed",
        "Manual verification completed"
    };

    /// <summary>
    /// Pre-defined denial reasons for quick selection
    /// </summary>
    public static readonly List<string> DenialReasons = new List<string>
    {
        "Payment amount insufficient",
        "Payment not received",
        "Invalid transaction ID",
        "Duplicate payment attempt",
        "Customer not present",
        "Payment method not accepted"
    };
}