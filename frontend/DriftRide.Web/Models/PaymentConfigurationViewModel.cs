using System.ComponentModel.DataAnnotations;

namespace DriftRide.Web.Models;

/// <summary>
/// Complete payment configuration model matching backend entity
/// </summary>
public class PaymentConfigurationModel
{
    public Guid Id { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PaymentUrl { get; set; }
    public bool IsEnabled { get; set; }
    public decimal PricePerRide { get; set; }
    public bool ApiIntegrationEnabled { get; set; }
    public string? ApiCredentials { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Payment configuration update request for sales staff
/// </summary>
public class UpdatePaymentConfigurationRequest
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(100, ErrorMessage = "Display name cannot exceed 100 characters")]
    public string DisplayName { get; set; } = string.Empty;

    [Url(ErrorMessage = "Please enter a valid URL")]
    [StringLength(500, ErrorMessage = "Payment URL cannot exceed 500 characters")]
    public string? PaymentUrl { get; set; }

    public bool IsEnabled { get; set; }

    [Required]
    [Range(0.01, 999999.99, ErrorMessage = "Price per ride must be between $0.01 and $999,999.99")]
    public decimal PricePerRide { get; set; }

    public bool ApiIntegrationEnabled { get; set; }

    [StringLength(1000, ErrorMessage = "API credentials cannot exceed 1000 characters")]
    public string? ApiCredentials { get; set; }
}

/// <summary>
/// Sales dashboard view model for payment configuration management
/// </summary>
public class PaymentConfigurationManagementViewModel
{
    public List<PaymentConfigurationModel> PaymentConfigurations { get; set; } = new List<PaymentConfigurationModel>();
    public UpdatePaymentConfigurationRequest? CurrentEdit { get; set; }
    public List<string> AvailablePaymentMethods { get; set; } = new List<string> { "CashApp", "PayPal", "CashInHand" };
    public bool HasUnsavedChanges { get; set; }
    public List<string> ErrorMessages { get; set; } = new List<string>();
    public string? SuccessMessage { get; set; }
}

/// <summary>
/// Configuration change notification for SignalR
/// </summary>
public class ConfigurationChangeNotification
{
    public string ChangeType { get; set; } = string.Empty; // "Updated", "Enabled", "Disabled"
    public string PaymentMethod { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public decimal PricePerRide { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
}

/// <summary>
/// Cache entry for payment configuration with expiration
/// </summary>
public class CachedPaymentConfiguration
{
    public List<PaymentConfigurationModel> Configurations { get; set; } = new List<PaymentConfigurationModel>();
    public DateTime CachedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}