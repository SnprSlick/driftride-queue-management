using DriftRide.Models;

namespace DriftRide.Services;

/// <summary>
/// Service interface for customer management operations.
/// Handles customer creation, retrieval, and manual additions for payment failure scenarios.
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// Creates a new customer record with the provided information.
    /// Allows duplicate names distinguished by timestamp.
    /// </summary>
    /// <param name="name">Customer display name (required, max 100 chars)</param>
    /// <param name="email">Customer email address (required, max 255 chars)</param>
    /// <param name="phoneNumber">Optional contact phone number (max 20 chars)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Created customer with generated ID and timestamp</returns>
    /// <exception cref="ArgumentNullException">Thrown when name or email is null or whitespace</exception>
    /// <exception cref="ArgumentException">Thrown when parameters exceed length limits</exception>
    Task<Customer> CreateAsync(string name, string email, string? phoneNumber = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually adds a customer directly to the queue without payment verification.
    /// Used as fallback when payment processing fails. Creates audit trail.
    /// </summary>
    /// <param name="name">Customer display name (required, max 100 chars)</param>
    /// <param name="email">Customer email address (required, max 255 chars)</param>
    /// <param name="phoneNumber">Optional contact phone number (max 20 chars)</param>
    /// <param name="reason">Reason for manual addition (e.g., payment app failure)</param>
    /// <param name="staffUsername">Username of sales staff performing manual addition</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Created queue entry with customer and payment records</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
    /// <exception cref="ArgumentException">Thrown when parameters exceed length limits</exception>
    Task<QueueEntry> AddManuallyAsync(string name, string email, string? phoneNumber, string reason, string staffUsername, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a customer by their unique identifier.
    /// </summary>
    /// <param name="customerId">Unique customer identifier</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Customer if found, null otherwise</returns>
    Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves customers by name with optional timestamp filtering.
    /// Used for duplicate name disambiguation in UI.
    /// </summary>
    /// <param name="name">Customer name to search for</param>
    /// <param name="fromDate">Optional start date filter</param>
    /// <param name="toDate">Optional end date filter</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>List of customers matching the criteria, ordered by creation date</returns>
    Task<List<Customer>> GetByNameAsync(string name, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates customer data before creation or update.
    /// Ensures business rules are enforced consistently.
    /// </summary>
    /// <param name="name">Customer name to validate</param>
    /// <param name="email">Email address to validate</param>
    /// <param name="phoneNumber">Phone number to validate (optional)</param>
    /// <returns>Validation result with any error messages</returns>
    Task<(bool IsValid, string[] Errors)> ValidateCustomerDataAsync(string name, string email, string? phoneNumber = null);
}