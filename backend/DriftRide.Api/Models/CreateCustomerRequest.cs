using System.ComponentModel.DataAnnotations;
using DriftRide.Api.Validation;

namespace DriftRide.Api.Models;

/// <summary>
/// Request model for creating a new customer.
/// Matches the OpenAPI specification for POST /api/customers endpoint.
/// </summary>
public class CreateCustomerRequest
{
    /// <summary>
    /// Customer's full name (required, max 100 characters).
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [CustomerNameValidation]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    [MinLength(2, ErrorMessage = "Name must be at least 2 characters long")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Customer's email address (required, valid email format).
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        ErrorMessage = "Email format is invalid")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Customer's phone number (optional, valid phone format).
    /// </summary>
    [PhoneNumberValidation]
    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
    public string? Phone { get; set; }
}