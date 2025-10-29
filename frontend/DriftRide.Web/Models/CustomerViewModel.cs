using System.ComponentModel.DataAnnotations;

namespace DriftRide.Web.Models;

/// <summary>
/// View model for customer registration and payment flow
/// </summary>
public class CustomerViewModel
{
    /// <summary>
    /// Customer's full name for registration
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    [Display(Name = "Full Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Customer's phone number for contact
    /// </summary>
    [Required(ErrorMessage = "Phone number is required")]
    [Phone(ErrorMessage = "Please enter a valid phone number")]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Selected payment method (CashApp, PayPal, Cash)
    /// </summary>
    [Required(ErrorMessage = "Please select a payment method")]
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>
    /// Current step in the workflow (1-4)
    /// </summary>
    public int CurrentStep { get; set; } = 1;

    /// <summary>
    /// API base URL for backend calls
    /// </summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// SignalR hub URL for real-time updates
    /// </summary>
    public string SignalRHubUrl { get; set; } = string.Empty;

    /// <summary>
    /// Available payment methods from configuration
    /// </summary>
    public List<PaymentMethodOption> PaymentMethods { get; set; } = new List<PaymentMethodOption>();

    /// <summary>
    /// Current ride price
    /// </summary>
    public decimal RidePrice { get; set; }

    /// <summary>
    /// Customer ID after registration
    /// </summary>
    public int? CustomerId { get; set; }

    /// <summary>
    /// Payment ID after payment initiation
    /// </summary>
    public int? PaymentId { get; set; }

    /// <summary>
    /// Current queue position (if confirmed)
    /// </summary>
    public int? QueuePosition { get; set; }

    /// <summary>
    /// Payment status
    /// </summary>
    public string PaymentStatus { get; set; } = "None";

    /// <summary>
    /// Error messages for display
    /// </summary>
    public List<string> ErrorMessages { get; set; } = new List<string>();
}

/// <summary>
/// Payment method configuration option for customer interface
/// </summary>
public class PaymentMethodOption
{
    public string Method { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PaymentUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool RequiresExternalApp { get; set; }
    public decimal PricePerRide { get; set; }
}

/// <summary>
/// Customer error view model
/// </summary>
public class CustomerErrorViewModel
{
    public string ErrorMessage { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "/Customer";
}