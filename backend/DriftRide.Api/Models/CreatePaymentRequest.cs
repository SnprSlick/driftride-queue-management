using System.ComponentModel.DataAnnotations;
using DriftRide.Models;
using DriftRide.Api.Validation;

namespace DriftRide.Api.Models;

/// <summary>
/// Request model for creating a new payment record.
/// Matches the OpenAPI specification for POST /api/payments endpoint.
/// </summary>
public class CreatePaymentRequest
{
    /// <summary>
    /// Customer making the payment (required).
    /// </summary>
    [Required(ErrorMessage = "CustomerId is required")]
    [Range(1, int.MaxValue, ErrorMessage = "CustomerId must be a positive integer")]
    public int CustomerId { get; set; }

    /// <summary>
    /// Payment amount in USD (required, must be positive).
    /// </summary>
    [Required(ErrorMessage = "Amount is required")]
    [PaymentAmountValidation(0.01, 9999.99, 2)]
    [Range(0.01, 9999.99, ErrorMessage = "Amount must be between $0.01 and $9999.99")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Payment method used (required).
    /// </summary>
    [Required(ErrorMessage = "PaymentMethod is required")]
    [PaymentMethodValidation]
    [EnumDataType(typeof(PaymentMethod), ErrorMessage = "Invalid payment method")]
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>
    /// Transaction ID from payment provider (required for CashApp/PayPal, optional for CashInHand).
    /// </summary>
    [MaxLength(255, ErrorMessage = "ExternalTransactionId cannot exceed 255 characters")]
    [MinLength(3, ErrorMessage = "ExternalTransactionId must be at least 3 characters when provided")]
    public string? ExternalTransactionId { get; set; }
}