using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DriftRide.Api.Validation;

/// <summary>
/// Custom validation attribute for phone number format validation.
/// Validates common phone number formats including US and international formats.
/// </summary>
public class PhoneNumberValidationAttribute : ValidationAttribute
{
    private static readonly Regex PhoneRegex = new Regex(
        @"^[\+]?[1-9]?[\d\s\-\(\)\.]{7,20}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Initializes a new instance of the PhoneNumberValidationAttribute.
    /// </summary>
    public PhoneNumberValidationAttribute()
    {
        ErrorMessage = "Phone number format is invalid. Please enter a valid phone number.";
    }

    /// <summary>
    /// Validates the phone number format.
    /// </summary>
    /// <param name="value">The phone number to validate</param>
    /// <returns>True if valid or null/empty, false otherwise</returns>
    public override bool IsValid(object? value)
    {
        // Allow null or empty for optional fields
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return true;
        }

        var phoneNumber = value.ToString()!;

        // Remove common formatting characters for validation
        var cleanedPhone = phoneNumber.Replace(" ", "")
                                     .Replace("-", "")
                                     .Replace("(", "")
                                     .Replace(")", "")
                                     .Replace(".", "");

        // Must contain at least 7 digits
        var digitCount = cleanedPhone.Count(char.IsDigit);
        if (digitCount < 7 || digitCount > 15)
        {
            return false;
        }

        // Validate format
        return PhoneRegex.IsMatch(phoneNumber);
    }

    /// <summary>
    /// Formats the error message with the field name.
    /// </summary>
    /// <param name="name">The name of the field being validated</param>
    /// <returns>The formatted error message</returns>
    public override string FormatErrorMessage(string name)
    {
        return $"The {name} field contains an invalid phone number format.";
    }
}