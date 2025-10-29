using DriftRide.Web.Models;

namespace DriftRide.Web.Services;

/// <summary>
/// Service interface for communicating with DriftRide API backend
/// </summary>
public interface IDriftRideApiService
{
    /// <summary>
    /// Get available payment methods and configuration
    /// </summary>
    Task<ApiResponse<List<PaymentMethodOption>>> GetPaymentMethodsAsync();

    /// <summary>
    /// Create a new customer
    /// </summary>
    Task<ApiResponse<CustomerResponse>> CreateCustomerAsync(CreateCustomerRequest request);

    /// <summary>
    /// Process a payment for a customer
    /// </summary>
    Task<ApiResponse<PaymentResponse>> ProcessPaymentAsync(ProcessPaymentRequest request);

    /// <summary>
    /// Get current queue status
    /// </summary>
    Task<ApiResponse<QueueStatusResponse>> GetQueueStatusAsync();

    /// <summary>
    /// Get customer's queue position
    /// </summary>
    Task<ApiResponse<CustomerQueuePosition>> GetCustomerQueuePositionAsync(int customerId);

    // Sales-specific endpoints
    /// <summary>
    /// Sales staff login with authentication
    /// </summary>
    Task<ApiResponse<SalesLoginResponse>> LoginAsync(SalesLoginRequest request);

    /// <summary>
    /// Get pending payments awaiting confirmation (Sales role required)
    /// </summary>
    Task<ApiResponse<List<PendingPaymentResponse>>> GetPendingPaymentsAsync(string authToken);

    /// <summary>
    /// Confirm or deny a payment (Sales role required)
    /// </summary>
    Task<ApiResponse<PaymentResponse>> ConfirmPaymentAsync(int paymentId, PaymentConfirmationRequest request, string authToken);

    /// <summary>
    /// Manually add customer when payment fails (Sales role required)
    /// </summary>
    Task<ApiResponse<CustomerResponse>> AddCustomerManuallyAsync(ManualCustomerRequest request, string authToken);

    /// <summary>
    /// Search customers by name for lookup (Sales role required)
    /// </summary>
    Task<ApiResponse<List<CustomerResponse>>> SearchCustomersAsync(string searchTerm, string authToken);

    /// <summary>
    /// Get payment configuration for management (Sales role required)
    /// </summary>
    Task<ApiResponse<List<PaymentConfigurationModel>>> GetPaymentConfigurationAsync(string authToken);

    /// <summary>
    /// Update payment configuration (Sales role required)
    /// </summary>
    Task<ApiResponse<PaymentConfigurationModel>> UpdatePaymentConfigurationAsync(UpdatePaymentConfigurationRequest request, string authToken);

    /// <summary>
    /// Invalidate cached payment methods to force refresh
    /// </summary>
    void InvalidatePaymentMethodsCache();
}

/// <summary>
/// Generic API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
}

/// <summary>
/// Customer creation request
/// </summary>
public class CreateCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

/// <summary>
/// Customer creation response
/// </summary>
public class CustomerResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Payment processing request
/// </summary>
public class ProcessPaymentRequest
{
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
}

/// <summary>
/// Payment processing response
/// </summary>
public class PaymentResponse
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ExternalTransactionId { get; set; }
}

/// <summary>
/// Queue status response
/// </summary>
public class QueueStatusResponse
{
    public List<QueueEntry> QueueEntries { get; set; } = new List<QueueEntry>();
    public int TotalInQueue { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Queue entry information
/// </summary>
public class QueueEntry
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime QueuedAt { get; set; }
}

/// <summary>
/// Customer queue position information
/// </summary>
public class CustomerQueuePosition
{
    public int CustomerId { get; set; }
    public int Position { get; set; }
    public int TotalInQueue { get; set; }
    public int EstimatedWaitMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
}

// Sales-specific models
/// <summary>
/// Sales staff login request
/// </summary>
public class SalesLoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Sales staff login response
/// </summary>
public class SalesLoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public SalesUserInfo User { get; set; } = new SalesUserInfo();
}

/// <summary>
/// Sales user information
/// </summary>
public class SalesUserInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// Pending payment response with customer details
/// </summary>
public class PendingPaymentResponse
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? ExternalTransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MinutesWaiting { get; set; }
}

/// <summary>
/// Payment confirmation request
/// </summary>
public class PaymentConfirmationRequest
{
    public bool Confirmed { get; set; }
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Manual customer addition request
/// </summary>
public class ManualCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}