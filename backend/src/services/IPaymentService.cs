using DriftRide.Models;

namespace DriftRide.Services;

/// <summary>
/// Service interface for payment processing and verification operations.
/// Handles payment creation, manual confirmation, and optional API-based verification.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Records a payment attempt for a customer.
    /// Creates payment record in Pending status awaiting verification.
    /// </summary>
    /// <param name="customerId">Customer making the payment</param>
    /// <param name="amount">Payment amount in USD (must be positive)</param>
    /// <param name="paymentMethod">Payment method used (CashApp, PayPal, CashInHand)</param>
    /// <param name="externalTransactionId">Transaction ID from payment provider (required for CashApp/PayPal)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Created payment record with Pending status</returns>
    /// <exception cref="ArgumentException">Thrown when amount is not positive or required transaction ID is missing</exception>
    /// <exception cref="InvalidOperationException">Thrown when customer has active pending payment</exception>
    Task<Payment> ProcessAsync(Guid customerId, decimal amount, PaymentMethod paymentMethod, string? externalTransactionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sales staff confirms or denies a payment manually.
    /// Updates payment status and creates audit trail. Triggers queue entry creation if confirmed.
    /// </summary>
    /// <param name="paymentId">Payment to confirm or deny</param>
    /// <param name="confirmed">True to confirm payment, false to deny</param>
    /// <param name="notes">Optional notes about the decision (max 500 chars)</param>
    /// <param name="staffUsername">Username of sales staff making the decision</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Updated payment record with confirmation details</returns>
    /// <exception cref="InvalidOperationException">Thrown when payment is not in Pending status</exception>
    Task<Payment> ConfirmAsync(Guid paymentId, bool confirmed, string? notes, string staffUsername, CancellationToken cancellationToken = default);

    /// <summary>
    /// Automatically verifies payment through external API integration.
    /// Only available when payment method has API integration enabled.
    /// </summary>
    /// <param name="paymentId">Payment to verify automatically</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Verification result with updated payment status</returns>
    /// <exception cref="InvalidOperationException">Thrown when payment method doesn't support API verification</exception>
    /// <exception cref="NotSupportedException">Thrown when API integration is not configured</exception>
    Task<(bool IsVerified, string? VerificationNotes)> VerifyAutomaticallyAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all payments pending staff confirmation.
    /// Used by sales dashboard to show payments awaiting verification.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>List of pending payments with customer information, ordered by creation date</returns>
    Task<List<Payment>> GetPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves payment by unique identifier with related customer information.
    /// </summary>
    /// <param name="paymentId">Unique payment identifier</param>
    /// <param name="includeCustomer">Whether to include customer information in result</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Payment if found, null otherwise</returns>
    Task<Payment?> GetByIdAsync(Guid paymentId, bool includeCustomer = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves payment history for a specific customer.
    /// Includes all payment attempts (confirmed and denied).
    /// </summary>
    /// <param name="customerId">Customer to get payment history for</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>List of payments for the customer, ordered by creation date (newest first)</returns>
    Task<List<Payment>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates payment data before processing.
    /// Ensures business rules and payment method requirements are met.
    /// </summary>
    /// <param name="customerId">Customer making the payment</param>
    /// <param name="amount">Payment amount to validate</param>
    /// <param name="paymentMethod">Payment method to validate</param>
    /// <param name="externalTransactionId">External transaction ID to validate</param>
    /// <returns>Validation result with any error messages</returns>
    Task<(bool IsValid, string[] Errors)> ValidatePaymentDataAsync(Guid customerId, decimal amount, PaymentMethod paymentMethod, string? externalTransactionId = null);

    /// <summary>
    /// Checks if customer has any active (pending) payments.
    /// Used to enforce business rule of one active payment per customer.
    /// </summary>
    /// <param name="customerId">Customer to check</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if customer has pending payment, false otherwise</returns>
    Task<bool> HasActivePendingPaymentAsync(Guid customerId, CancellationToken cancellationToken = default);
}