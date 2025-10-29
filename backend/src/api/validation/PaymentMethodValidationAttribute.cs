using System.ComponentModel.DataAnnotations;
using DriftRide.Models;

namespace DriftRide.Api.Validation;

/// <summary>
/// Custom validation attribute for payment method business rules.
/// Validates payment method specific requirements such as external transaction ID requirements.
/// </summary>
public class PaymentMethodValidationAttribute : ValidationAttribute
{
    /// <summary>
    /// Initializes a new instance of the PaymentMethodValidationAttribute.
    /// </summary>
    public PaymentMethodValidationAttribute()
    {
        ErrorMessage = "Payment method validation failed.";
    }

    /// <summary>
    /// Validates payment method against business rules.
    /// </summary>
    /// <param name="value">The payment method to validate</param>
    /// <param name="validationContext">The validation context containing the object being validated</param>
    /// <returns>Validation result</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return new ValidationResult("Payment method is required.");
        }

        if (!Enum.IsDefined(typeof(PaymentMethod), value))
        {
            return new ValidationResult("Invalid payment method specified.");
        }

        var paymentMethod = (PaymentMethod)value;

        // Get the external transaction ID from the same object
        var externalTransactionIdProperty = validationContext.ObjectType.GetProperty("ExternalTransactionId");
        var externalTransactionId = externalTransactionIdProperty?.GetValue(validationContext.ObjectInstance) as string;

        // Validate business rules for different payment methods
        switch (paymentMethod)
        {
            case PaymentMethod.CashApp:
            case PaymentMethod.PayPal:
                if (string.IsNullOrWhiteSpace(externalTransactionId))
                {
                    return new ValidationResult(
                        $"External transaction ID is required for {paymentMethod} payments.",
                        new[] { "ExternalTransactionId" });
                }

                // Validate transaction ID format for electronic payments
                if (externalTransactionId.Length < 3 || externalTransactionId.Length > 255)
                {
                    return new ValidationResult(
                        "External transaction ID must be between 3 and 255 characters.",
                        new[] { "ExternalTransactionId" });
                }
                break;

            case PaymentMethod.CashInHand:
                // Cash in hand doesn't require external transaction ID
                // This is acceptable for in-person cash payments
                break;

            default:
                return new ValidationResult($"Payment method {paymentMethod} is not supported.");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Formats the error message with the field name.
    /// </summary>
    /// <param name="name">The name of the field being validated</param>
    /// <returns>The formatted error message</returns>
    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field failed payment method validation.";
    }
}