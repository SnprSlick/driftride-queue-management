using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DriftRide.Api.Validation;

/// <summary>
/// Custom validation attribute for customer name business rules.
/// Validates customer names against business constraints including prohibited characters,
/// minimum meaningful length, and format requirements.
/// </summary>
public class CustomerNameValidationAttribute : ValidationAttribute
{
    private static readonly Regex InvalidCharactersRegex = new Regex(
        @"[<>\""\'\&\;\(\)\*\%\$\#\@\!\[\]\{\}\\\/\|]",
        RegexOptions.Compiled);

    private static readonly Regex ValidNameRegex = new Regex(
        @"^[a-zA-Z\s\.\-\'\u00C0-\u017F]+$",
        RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the CustomerNameValidationAttribute.
    /// </summary>
    public CustomerNameValidationAttribute()
    {
        ErrorMessage = "Customer name contains invalid characters or format.";
    }

    /// <summary>
    /// Validates the customer name against business rules.
    /// </summary>
    /// <param name="value">The customer name to validate</param>
    /// <param name="validationContext">The validation context</param>
    /// <returns>Validation result</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return new ValidationResult("Customer name is required.");
        }

        var name = value.ToString()!.Trim();

        // Check minimum length for meaningful names
        if (name.Length < 2)
        {
            return new ValidationResult("Customer name must be at least 2 characters long.");
        }

        // Check maximum length
        if (name.Length > 100)
        {
            return new ValidationResult("Customer name cannot exceed 100 characters.");
        }

        // Check for prohibited characters that could indicate injection attacks
        if (InvalidCharactersRegex.IsMatch(name))
        {
            return new ValidationResult("Customer name contains prohibited characters. Only letters, spaces, hyphens, apostrophes, and periods are allowed.");
        }

        // Validate against allowed characters (including international characters)
        if (!ValidNameRegex.IsMatch(name))
        {
            return new ValidationResult("Customer name format is invalid. Please use only letters, spaces, hyphens, apostrophes, and periods.");
        }

        // Check for suspicious patterns
        if (name.Contains("..") || name.Contains("--") || name.Contains("  "))
        {
            return new ValidationResult("Customer name contains invalid character sequences.");
        }

        // Check for names that are too generic or potentially fake
        var lowerName = name.ToLowerInvariant();
        if (lowerName == "test" || lowerName == "admin" || lowerName == "user" ||
            lowerName == "customer" || lowerName == "guest" || lowerName == "unknown")
        {
            return new ValidationResult("Please enter a valid customer name.");
        }

        // Check for names that are just numbers or single characters repeated
        if (name.All(char.IsDigit))
        {
            return new ValidationResult("Customer name cannot consist only of numbers.");
        }

        if (name.Length > 1 && name.All(c => c == name[0]))
        {
            return new ValidationResult("Customer name cannot consist of the same character repeated.");
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
        return $"The {name} field contains an invalid customer name.";
    }
}