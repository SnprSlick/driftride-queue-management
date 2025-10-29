using DriftRide.Models;

namespace DriftRide.Api.Models;

/// <summary>
/// Payment response model for API endpoints.
/// Represents payment data in API responses, matching the OpenAPI specification.
/// </summary>
public class Payment
{
    /// <summary>
    /// Unique payment identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Customer who made the payment.
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// Payment amount in USD.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Payment method used.
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>
    /// Payment verification status.
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Transaction ID from payment provider (if applicable).
    /// </summary>
    public string? ExternalTransactionId { get; set; }

    /// <summary>
    /// When the payment was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the payment was confirmed or denied by sales staff (if applicable).
    /// </summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>
    /// Username of sales staff who confirmed or denied the payment (if applicable).
    /// </summary>
    public string? ConfirmedByUsername { get; set; }

    /// <summary>
    /// Optional notes from sales staff about the confirmation decision.
    /// </summary>
    public string? ConfirmationNotes { get; set; }

    /// <summary>
    /// Customer name for display purposes (populated from navigation property).
    /// </summary>
    public string? CustomerName { get; set; }
}