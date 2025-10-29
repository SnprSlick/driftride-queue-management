namespace DriftRide.Api.Models;

/// <summary>
/// Customer response model for API endpoints.
/// Represents customer data in API responses, matching the OpenAPI specification.
/// </summary>
public class Customer
{
    /// <summary>
    /// Unique customer identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Customer's full name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Customer's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Customer's phone number (optional).
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Whether the customer account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Account creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}