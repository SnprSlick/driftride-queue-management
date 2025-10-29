using DriftRide.Models;

namespace DriftRide.Services;

/// <summary>
/// Service interface for payment configuration management.
/// Handles configuration of payment methods, pricing, and API integration settings.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets all payment method configurations.
    /// </summary>
    /// <param name="includeCredentials">Whether to include API credentials (for admin use only)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of payment method configurations</returns>
    Task<PaymentConfiguration[]> GetPaymentMethodsAsync(bool includeCredentials = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific payment method configuration.
    /// </summary>
    /// <param name="paymentMethod">Payment method to retrieve</param>
    /// <param name="includeCredentials">Whether to include API credentials (for admin use only)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment method configuration or null if not found</returns>
    Task<PaymentConfiguration?> GetPaymentMethodAsync(PaymentMethod paymentMethod, bool includeCredentials = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a payment method configuration.
    /// Creates new configuration if one doesn't exist for the payment method.
    /// </summary>
    /// <param name="paymentMethod">Payment method to update</param>
    /// <param name="displayName">Display name for the payment method</param>
    /// <param name="paymentUrl">URL for the payment method (e.g., CashApp link)</param>
    /// <param name="isEnabled">Whether the payment method is enabled for customers</param>
    /// <param name="pricePerRide">Price per ride for this payment method</param>
    /// <param name="apiIntegrationEnabled">Whether API integration is enabled for automatic verification</param>
    /// <param name="apiCredentials">API credentials for payment verification (encrypted storage)</param>
    /// <param name="updatedBy">Username of the staff member making the update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated payment method configuration</returns>
    Task<PaymentConfiguration> UpdatePaymentMethodAsync(
        PaymentMethod paymentMethod,
        string displayName,
        string paymentUrl,
        bool isEnabled,
        decimal pricePerRide,
        bool apiIntegrationEnabled,
        string? apiCredentials,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates payment method configuration data.
    /// </summary>
    /// <param name="paymentMethod">Payment method</param>
    /// <param name="displayName">Display name</param>
    /// <param name="paymentUrl">Payment URL</param>
    /// <param name="pricePerRide">Price per ride</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with any errors</returns>
    Task<ValidationResult> ValidatePaymentMethodConfigurationAsync(
        PaymentMethod paymentMethod,
        string displayName,
        string paymentUrl,
        decimal pricePerRide,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets enabled payment methods for customer display.
    /// Excludes sensitive information like API credentials.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of enabled payment methods for customer use</returns>
    Task<PaymentConfiguration[]> GetEnabledPaymentMethodsForCustomersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests API integration for a payment method.
    /// Verifies that API credentials are valid and connection is working.
    /// </summary>
    /// <param name="paymentMethod">Payment method to test</param>
    /// <param name="apiCredentials">API credentials to test</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Test result indicating success or failure with details</returns>
    Task<ApiTestResult> TestApiIntegrationAsync(
        PaymentMethod paymentMethod,
        string apiCredentials,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets configuration audit history for a payment method.
    /// Tracks who changed what and when for compliance and debugging.
    /// </summary>
    /// <param name="paymentMethod">Payment method to get history for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of configuration change records</returns>
    Task<ConfigurationAuditRecord[]> GetConfigurationHistoryAsync(
        PaymentMethod paymentMethod,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of payment method configuration validation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of API integration testing.
/// </summary>
public class ApiTestResult
{
    public bool IsSuccessful { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeSpan ResponseTime { get; set; }
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorDetails { get; set; }
}

/// <summary>
/// Record of configuration changes for audit purposes.
/// </summary>
public class ConfigurationAuditRecord
{
    public int Id { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string FieldChanged { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
}