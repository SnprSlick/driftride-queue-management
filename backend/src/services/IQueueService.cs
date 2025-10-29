using DriftRide.Models;

namespace DriftRide.Services;

/// <summary>
/// Service interface for queue management and operations.
/// Handles queue entry creation, position management, ride completion, and desktop synchronization.
/// </summary>
public interface IQueueService
{
    /// <summary>
    /// Adds a confirmed payment to the ride queue.
    /// Automatically assigns next available position and creates queue entry.
    /// </summary>
    /// <param name="paymentId">Confirmed payment to add to queue</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Created queue entry with assigned position</returns>
    /// <exception cref="InvalidOperationException">Thrown when payment is not confirmed or already in queue</exception>
    Task<QueueEntry> AddToQueueAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a ride as completed by the driver.
    /// Updates queue entry status and adjusts positions of remaining customers.
    /// </summary>
    /// <param name="queueEntryId">Queue entry to complete</param>
    /// <param name="driverUsername">Username of driver completing the ride</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Updated queue entry with completion details</returns>
    /// <exception cref="InvalidOperationException">Thrown when queue entry is not in progress or invalid state</exception>
    Task<QueueEntry> CompleteRideAsync(Guid queueEntryId, string driverUsername, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a customer from the queue (no-show handling).
    /// Updates queue positions and creates audit trail with reason.
    /// </summary>
    /// <param name="queueEntryId">Queue entry to remove</param>
    /// <param name="reason">Reason for removal (e.g., customer left, no-show)</param>
    /// <param name="staffUsername">Username of sales staff removing customer</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Updated queue entry marked as cancelled</returns>
    /// <exception cref="InvalidOperationException">Thrown when queue entry cannot be removed</exception>
    Task<QueueEntry> RemoveCustomerAsync(Guid queueEntryId, string reason, string staffUsername, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually reorders the queue based on sales staff decisions.
    /// Updates positions to match provided order and maintains queue integrity.
    /// </summary>
    /// <param name="queueOrder">Array of queue entry IDs in desired order</param>
    /// <param name="staffUsername">Username of sales staff performing reorder</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Updated queue entries with new positions</returns>
    /// <exception cref="ArgumentException">Thrown when queue order contains invalid or duplicate IDs</exception>
    Task<List<QueueEntry>> ReorderAsync(Guid[] queueOrder, string staffUsername, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes queue state from desktop application.
    /// Desktop is authoritative source for queue order during sync conflicts.
    /// </summary>
    /// <param name="desktopQueueState">Queue state from desktop application</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Synchronized queue state reflecting desktop changes</returns>
    /// <exception cref="ArgumentNullException">Thrown when desktop queue state is null</exception>
    Task<List<QueueEntry>> SyncFromDesktopAsync(List<QueueEntry> desktopQueueState, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves current queue with all active customers.
    /// Returns waiting and in-progress entries ordered by position.
    /// </summary>
    /// <param name="includeCompleted">Whether to include recently completed entries</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>List of queue entries with customer and payment information</returns>
    Task<List<QueueEntry>> GetCurrentQueueAsync(bool includeCompleted = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next customer in queue for driver interface.
    /// Returns the customer at position 1 (front of queue).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Next queue entry to serve, null if queue is empty</returns>
    Task<QueueEntry?> GetNextCustomerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a ride by moving customer from waiting to in-progress.
    /// Updates queue entry status and sets start timestamp.
    /// </summary>
    /// <param name="queueEntryId">Queue entry to start</param>
    /// <param name="driverUsername">Username of driver starting the ride</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Updated queue entry with in-progress status</returns>
    /// <exception cref="InvalidOperationException">Thrown when queue entry is not in waiting status</exception>
    Task<QueueEntry> StartRideAsync(Guid queueEntryId, string driverUsername, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves queue entry by unique identifier with related information.
    /// </summary>
    /// <param name="queueEntryId">Unique queue entry identifier</param>
    /// <param name="includeRelated">Whether to include customer and payment information</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Queue entry if found, null otherwise</returns>
    Task<QueueEntry?> GetByIdAsync(Guid queueEntryId, bool includeRelated = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalculates and fixes queue positions to ensure continuity.
    /// Removes gaps and ensures positions start from 1 and are continuous.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Number of positions updated</returns>
    Task<int> RecalculatePositionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates queue operations before execution.
    /// Ensures business rules and queue integrity are maintained.
    /// </summary>
    /// <param name="operation">Type of queue operation to validate</param>
    /// <param name="queueEntryId">Queue entry ID if applicable</param>
    /// <param name="additionalData">Additional data for validation (e.g., new positions)</param>
    /// <returns>Validation result with any error messages</returns>
    Task<(bool IsValid, string[] Errors)> ValidateQueueOperationAsync(string operation, Guid? queueEntryId = null, object? additionalData = null);
}