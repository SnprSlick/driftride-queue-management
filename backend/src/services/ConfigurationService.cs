using DriftRide.Data;
using DriftRide.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace DriftRide.Services;

/// <summary>
/// Service implementation for payment configuration management.
/// Handles configuration of payment methods, pricing, and API integration settings.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly DriftRideDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ConfigurationService> _logger;

    /// <summary>
    /// Initializes a new instance of the ConfigurationService.
    /// </summary>
    /// <param name="context">Database context for Entity Framework operations</param>
    /// <param name="notificationService">Service for real-time notifications</param>
    /// <param name="logger">Logger for this service</param>
    public ConfigurationService(
        DriftRideDbContext context,
        INotificationService notificationService,
        ILogger<ConfigurationService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PaymentConfiguration[]> GetPaymentMethodsAsync(bool includeCredentials = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Retrieving payment method configurations, includeCredentials: {IncludeCredentials}", includeCredentials);

            var configurations = await _context.PaymentConfigurations
                .OrderBy(c => c.PaymentMethod)
                .ToArrayAsync(cancellationToken);

            // Security: Never return API credentials unless explicitly requested for admin purposes
            if (!includeCredentials)
            {
                foreach (var config in configurations)
                {
                    config.ApiCredentials = null;
                }
            }

            _logger.LogInformation("Retrieved {Count} payment method configurations", configurations.Length);
            return configurations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment method configurations");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PaymentConfiguration?> GetPaymentMethodAsync(PaymentMethod paymentMethod, bool includeCredentials = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Retrieving payment method configuration for {PaymentMethod}", paymentMethod);

            var configuration = await _context.PaymentConfigurations
                .FirstOrDefaultAsync(c => c.PaymentMethod == paymentMethod, cancellationToken);

            if (configuration != null && !includeCredentials)
            {
                configuration.ApiCredentials = null;
            }

            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment method configuration for {PaymentMethod}", paymentMethod);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PaymentConfiguration> UpdatePaymentMethodAsync(
        PaymentMethod paymentMethod,
        string displayName,
        string paymentUrl,
        bool isEnabled,
        decimal pricePerRide,
        bool apiIntegrationEnabled,
        string? apiCredentials,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating payment method configuration for {PaymentMethod} by {UpdatedBy}", paymentMethod, updatedBy);

            // Validate configuration data
            var validation = await ValidatePaymentMethodConfigurationAsync(paymentMethod, displayName, paymentUrl, pricePerRide, cancellationToken);
            if (!validation.IsValid)
            {
                var errorMessage = string.Join(", ", validation.Errors);
                throw new ArgumentException($"Invalid payment method configuration: {errorMessage}");
            }

            // Find existing configuration or create new one
            var configuration = await _context.PaymentConfigurations
                .FirstOrDefaultAsync(c => c.PaymentMethod == paymentMethod, cancellationToken);

            var isNewConfiguration = configuration == null;

            if (configuration == null)
            {
                configuration = new PaymentConfiguration
                {
                    PaymentMethod = paymentMethod,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PaymentConfigurations.Add(configuration);
            }

            // Store old values for audit trail
            var oldDisplayName = configuration.DisplayName;
            var oldPaymentUrl = configuration.PaymentUrl;
            var oldIsEnabled = configuration.IsEnabled;
            var oldPricePerRide = configuration.PricePerRide;
            var oldApiIntegrationEnabled = configuration.ApiIntegrationEnabled;

            // Update configuration
            configuration.DisplayName = displayName;
            configuration.PaymentUrl = paymentUrl;
            configuration.IsEnabled = isEnabled;
            configuration.PricePerRide = pricePerRide;
            configuration.ApiIntegrationEnabled = apiIntegrationEnabled;
            configuration.UpdatedAt = DateTime.UtcNow;
            configuration.UpdatedBy = updatedBy;

            // Update API credentials if provided
            if (apiIntegrationEnabled && !string.IsNullOrEmpty(apiCredentials))
            {
                // In production, encrypt credentials before storing
                configuration.ApiCredentials = apiCredentials;
            }
            else if (!apiIntegrationEnabled)
            {
                configuration.ApiCredentials = null;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully updated payment method configuration for {PaymentMethod}", paymentMethod);

            // Create audit records for significant changes
            await CreateAuditRecordsAsync(paymentMethod, updatedBy,
                ("DisplayName", oldDisplayName, displayName),
                ("PaymentUrl", oldPaymentUrl, paymentUrl),
                ("IsEnabled", oldIsEnabled.ToString(), isEnabled.ToString()),
                ("PricePerRide", oldPricePerRide.ToString("F2"), pricePerRide.ToString("F2")),
                ("ApiIntegrationEnabled", oldApiIntegrationEnabled.ToString(), apiIntegrationEnabled.ToString()),
                cancellationToken);

            // Notify other systems of configuration changes
            await _notificationService.NotifyConfigurationChangeAsync("PaymentMethod", new
            {
                PaymentMethod = paymentMethod,
                IsEnabled = isEnabled,
                PricePerRide = pricePerRide,
                ApiIntegrationEnabled = apiIntegrationEnabled,
                UpdatedBy = updatedBy,
                UpdatedAt = configuration.UpdatedAt
            });

            // Return configuration without sensitive data
            var result = new PaymentConfiguration
            {
                Id = configuration.Id,
                PaymentMethod = configuration.PaymentMethod,
                DisplayName = configuration.DisplayName,
                PaymentUrl = configuration.PaymentUrl,
                IsEnabled = configuration.IsEnabled,
                PricePerRide = configuration.PricePerRide,
                ApiIntegrationEnabled = configuration.ApiIntegrationEnabled,
                CreatedAt = configuration.CreatedAt,
                UpdatedAt = configuration.UpdatedAt,
                UpdatedBy = configuration.UpdatedBy,
                ApiCredentials = null // Never return credentials
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment method configuration for {PaymentMethod}", paymentMethod);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ValidationResult> ValidatePaymentMethodConfigurationAsync(
        PaymentMethod paymentMethod,
        string displayName,
        string paymentUrl,
        decimal pricePerRide,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult { IsValid = true };

        try
        {
            // Validate payment method
            if (!Enum.IsDefined(typeof(PaymentMethod), paymentMethod))
            {
                result.Errors.Add("PaymentMethod: Invalid payment method specified");
                result.IsValid = false;
            }

            // Validate display name
            if (string.IsNullOrWhiteSpace(displayName))
            {
                result.Errors.Add("DisplayName: Display name is required");
                result.IsValid = false;
            }
            else if (displayName.Length > 100)
            {
                result.Errors.Add("DisplayName: Display name cannot exceed 100 characters");
                result.IsValid = false;
            }

            // Validate payment URL
            if (!string.IsNullOrWhiteSpace(paymentUrl))
            {
                if (!IsValidUrl(paymentUrl))
                {
                    result.Errors.Add("PaymentUrl: Invalid URL format");
                    result.IsValid = false;
                }
                else if (paymentUrl.Length > 500)
                {
                    result.Errors.Add("PaymentUrl: Payment URL cannot exceed 500 characters");
                    result.IsValid = false;
                }
            }

            // Validate price per ride
            if (pricePerRide < 0)
            {
                result.Errors.Add("PricePerRide: Price per ride cannot be negative");
                result.IsValid = false;
            }
            else if (pricePerRide > 1000)
            {
                result.Errors.Add("PricePerRide: Price per ride cannot exceed $1,000");
                result.IsValid = false;
            }
            else if (pricePerRide == 0)
            {
                result.Warnings.Add("PricePerRide: Price is set to $0.00 - rides will be free");
            }

            // Validate payment method specific requirements
            switch (paymentMethod)
            {
                case PaymentMethod.CashApp:
                case PaymentMethod.PayPal:
                    if (string.IsNullOrWhiteSpace(paymentUrl))
                    {
                        result.Warnings.Add($"PaymentUrl: {paymentMethod} typically requires a payment URL");
                    }
                    break;

                case PaymentMethod.CashInHand:
                    if (!string.IsNullOrWhiteSpace(paymentUrl))
                    {
                        result.Warnings.Add("PaymentUrl: Cash-in-hand payments typically don't require a payment URL");
                    }
                    break;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating payment method configuration");
            result.Errors.Add("Validation error: Unable to validate configuration");
            result.IsValid = false;
            return result;
        }
    }

    /// <inheritdoc />
    public async Task<PaymentConfiguration[]> GetEnabledPaymentMethodsForCustomersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Retrieving enabled payment methods for customers");

            var enabledConfigurations = await _context.PaymentConfigurations
                .Where(c => c.IsEnabled)
                .OrderBy(c => c.PaymentMethod)
                .ToArrayAsync(cancellationToken);

            // Security: Always exclude API credentials for customer-facing data
            foreach (var config in enabledConfigurations)
            {
                config.ApiCredentials = null;
            }

            _logger.LogInformation("Retrieved {Count} enabled payment methods for customers", enabledConfigurations.Length);
            return enabledConfigurations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving enabled payment methods for customers");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ApiTestResult> TestApiIntegrationAsync(
        PaymentMethod paymentMethod,
        string apiCredentials,
        CancellationToken cancellationToken = default)
    {
        var result = new ApiTestResult();

        try
        {
            _logger.LogInformation("Testing API integration for {PaymentMethod}", paymentMethod);

            var startTime = DateTime.UtcNow;

            // Simulate API testing (in production, this would make actual API calls)
            switch (paymentMethod)
            {
                case PaymentMethod.CashApp:
                    result = await TestCashAppApiAsync(apiCredentials, cancellationToken);
                    break;

                case PaymentMethod.PayPal:
                    result = await TestPayPalApiAsync(apiCredentials, cancellationToken);
                    break;

                default:
                    result.IsSuccessful = false;
                    result.Message = $"API integration not supported for {paymentMethod}";
                    break;
            }

            result.ResponseTime = DateTime.UtcNow - startTime;
            result.TestedAt = DateTime.UtcNow;

            _logger.LogInformation("API integration test for {PaymentMethod} completed. Success: {Success}",
                paymentMethod, result.IsSuccessful);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing API integration for {PaymentMethod}", paymentMethod);

            result.IsSuccessful = false;
            result.Message = "API test failed due to unexpected error";
            result.ErrorDetails = ex.Message;
            result.TestedAt = DateTime.UtcNow;

            return result;
        }
    }

    /// <inheritdoc />
    public async Task<ConfigurationAuditRecord[]> GetConfigurationHistoryAsync(
        PaymentMethod paymentMethod,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Retrieving configuration history for {PaymentMethod}", paymentMethod);

            // In a full implementation, this would query a separate audit table
            // For now, return empty array as audit functionality would be implemented separately
            return Array.Empty<ConfigurationAuditRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration history for {PaymentMethod}", paymentMethod);
            throw;
        }
    }

    /// <summary>
    /// Validates if a string is a valid URL.
    /// </summary>
    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
               (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Creates audit records for configuration changes.
    /// </summary>
    private async Task CreateAuditRecordsAsync(
        PaymentMethod paymentMethod,
        string changedBy,
        params (string Field, string? OldValue, string? NewValue)[] changes)
    {
        try
        {
            foreach (var (field, oldValue, newValue) in changes)
            {
                if (oldValue != newValue)
                {
                    _logger.LogInformation("Configuration change: {PaymentMethod}.{Field} changed from '{OldValue}' to '{NewValue}' by {ChangedBy}",
                        paymentMethod, field, oldValue, newValue, changedBy);

                    // In a full implementation, this would create records in an audit table
                    // For now, we just log the changes
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create audit records for {PaymentMethod}", paymentMethod);
            // Don't throw - audit failure shouldn't prevent configuration updates
        }
    }

    /// <summary>
    /// Tests CashApp API integration.
    /// </summary>
    private async Task<ApiTestResult> TestCashAppApiAsync(string apiCredentials, CancellationToken cancellationToken)
    {
        // Simulate API test
        await Task.Delay(500, cancellationToken); // Simulate network call

        return new ApiTestResult
        {
            IsSuccessful = !string.IsNullOrEmpty(apiCredentials),
            Message = string.IsNullOrEmpty(apiCredentials)
                ? "Invalid API credentials"
                : "CashApp API connection successful"
        };
    }

    /// <summary>
    /// Tests PayPal API integration.
    /// </summary>
    private async Task<ApiTestResult> TestPayPalApiAsync(string apiCredentials, CancellationToken cancellationToken)
    {
        // Simulate API test
        await Task.Delay(750, cancellationToken); // Simulate network call

        return new ApiTestResult
        {
            IsSuccessful = !string.IsNullOrEmpty(apiCredentials),
            Message = string.IsNullOrEmpty(apiCredentials)
                ? "Invalid API credentials"
                : "PayPal API connection successful"
        };
    }
}