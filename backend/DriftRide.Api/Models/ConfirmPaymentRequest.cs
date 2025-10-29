using System.ComponentModel.DataAnnotations;

namespace DriftRide.Api.Models;

/// <summary>
/// Request model for confirming or denying a payment.
/// Matches the OpenAPI specification for POST /api/payments/{id}/confirm endpoint.
/// </summary>
public class ConfirmPaymentRequest : IValidatableObject
{
    /// <summary>
    /// True to confirm payment, false to deny payment (required).
    /// </summary>
    [Required(ErrorMessage = "Confirmed status is required")]
    public bool Confirmed { get; set; }

    /// <summary>
    /// Optional notes about the confirmation decision (max 500 characters).
    /// Required when denying a payment for audit trail purposes.
    /// </summary>
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    [MinLength(3, ErrorMessage = "Notes must be at least 3 characters when provided")]
    [RegularExpression(@"^[a-zA-Z0-9\s\.\,\!\?\-\'\(\)]+$",
        ErrorMessage = "Notes contain invalid characters. Only letters, numbers, spaces, and basic punctuation are allowed.")]
    public string? Notes { get; set; }

    /// <summary>
    /// Validates that notes are provided when denying a payment.
    /// </summary>
    /// <param name="validationContext">The validation context</param>
    /// <returns>Validation results</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Require notes when denying a payment for audit trail
        if (!Confirmed && string.IsNullOrWhiteSpace(Notes))
        {
            yield return new ValidationResult(
                "Notes are required when denying a payment for audit trail purposes.",
                new[] { nameof(Notes) });
        }

        // Validate notes length when denying
        if (!Confirmed && Notes != null && Notes.Trim().Length < 10)
        {
            yield return new ValidationResult(
                "Notes must be at least 10 characters when denying a payment.",
                new[] { nameof(Notes) });
        }
    }
}