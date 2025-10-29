using System.ComponentModel.DataAnnotations;

namespace DriftRide.Api.Validation;

/// <summary>
/// Custom validation attribute for payment amount business rules.
/// Validates payment amounts against business constraints including minimum amounts,
/// maximum amounts, and proper decimal precision.
/// </summary>
public class PaymentAmountValidationAttribute : ValidationAttribute
{
    private readonly decimal _minimumAmount;
    private readonly decimal _maximumAmount;
    private readonly int _decimalPlaces;

    /// <summary>
    /// Initializes a new instance of the PaymentAmountValidationAttribute.
    /// </summary>
    /// <param name="minimumAmount">Minimum allowed payment amount</param>
    /// <param name="maximumAmount">Maximum allowed payment amount</param>
    /// <param name="decimalPlaces">Maximum number of decimal places allowed</param>
    public PaymentAmountValidationAttribute(double minimumAmount = 0.01, double maximumAmount = 9999.99, int decimalPlaces = 2)
    {
        _minimumAmount = (decimal)minimumAmount;
        _maximumAmount = (decimal)maximumAmount;
        _decimalPlaces = decimalPlaces;
        ErrorMessage = $"Payment amount must be between ${_minimumAmount:F2} and ${_maximumAmount:F2} with up to {_decimalPlaces} decimal places.";
    }

    /// <summary>
    /// Validates the payment amount against business rules.
    /// </summary>
    /// <param name="value">The payment amount to validate</param>
    /// <param name="validationContext">The validation context</param>
    /// <returns>Validation result</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return new ValidationResult("Payment amount is required.");
        }

        if (!decimal.TryParse(value.ToString(), out var amount))
        {
            return new ValidationResult("Payment amount must be a valid decimal number.");
        }

        // Check minimum amount
        if (amount < _minimumAmount)
        {
            return new ValidationResult($"Payment amount cannot be less than ${_minimumAmount:F2}.");
        }

        // Check maximum amount
        if (amount > _maximumAmount)
        {
            return new ValidationResult($"Payment amount cannot exceed ${_maximumAmount:F2}.");
        }

        // Check decimal places
        var decimalPlaces = GetDecimalPlaces(amount);
        if (decimalPlaces > _decimalPlaces)
        {
            return new ValidationResult($"Payment amount cannot have more than {_decimalPlaces} decimal places.");
        }

        // Business rule: Check for suspicious amounts (e.g., extremely precise amounts that might indicate testing)
        if (decimalPlaces > 2 && amount < 1.00m)
        {
            return new ValidationResult("Payment amounts under $1.00 cannot have more than 2 decimal places.");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Gets the number of decimal places in a decimal value.
    /// </summary>
    /// <param name="value">The decimal value to check</param>
    /// <returns>The number of decimal places</returns>
    private static int GetDecimalPlaces(decimal value)
    {
        var text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var decimalIndex = text.IndexOf('.');

        if (decimalIndex == -1)
        {
            return 0;
        }

        return text.Length - decimalIndex - 1;
    }

    /// <summary>
    /// Formats the error message with the field name.
    /// </summary>
    /// <param name="name">The name of the field being validated</param>
    /// <returns>The formatted error message</returns>
    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field has an invalid payment amount.";
    }
}