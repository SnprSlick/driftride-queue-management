using DriftRide.Data;
using DriftRide.Models;
using Microsoft.EntityFrameworkCore;

namespace DriftRide.Services;

/// <summary>
/// Service implementation for queue management and operations.
/// Handles queue entry creation, position management, ride operations, and desktop synchronization.
/// </summary>
public class QueueService : IQueueService
{
    private readonly DriftRideDbContext _context;
    private readonly INotificationService _notificationService;

    /// <summary>
    /// Initializes a new instance of the QueueService.
    /// </summary>
    /// <param name="context">Database context for Entity Framework operations</param>
    /// <param name="notificationService">Notification service for real-time updates</param>
    public QueueService(
        DriftRideDbContext context,
        INotificationService notificationService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    /// <inheritdoc />
    public async Task<QueueEntry> AddToQueueAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _context.Payments
            .Include(p => p.Customer)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

        if (payment == null)
        {
            throw new ArgumentException($"Payment with ID {paymentId} not found.");
        }

        if (payment.Status != PaymentStatus.Confirmed)
        {
            throw new InvalidOperationException($"Payment must be confirmed before adding to queue. Current status: {payment.Status}");
        }

        // Check if payment is already in queue
        var existingEntry = await _context.QueueEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.PaymentId == paymentId, cancellationToken);

        if (existingEntry != null)
        {
            throw new InvalidOperationException("Payment is already associated with a queue entry.");
        }

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Get next position (max position + 1)
            var nextPosition = await _context.QueueEntries
                .Where(q => q.Status == QueueEntryStatus.Waiting || q.Status == QueueEntryStatus.InProgress)
                .MaxAsync(q => (int?)q.Position, cancellationToken) ?? 0;

            nextPosition++;

            var queueEntry = new QueueEntry
            {
                Id = Guid.NewGuid(),
                CustomerId = payment.CustomerId,
                PaymentId = paymentId,
                Position = nextPosition,
                Status = QueueEntryStatus.Waiting,
                QueuedAt = DateTime.UtcNow
            };

            _context.QueueEntries.Add(queueEntry);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            // Load with related data for notification
            queueEntry = await GetByIdAsync(queueEntry.Id, true, cancellationToken) ?? queueEntry;

            // Notify about queue update
            var currentQueue = await GetCurrentQueueAsync(false, cancellationToken);
            await _notificationService.NotifyQueueUpdateAsync(currentQueue, cancellationToken);

            return queueEntry;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<QueueEntry> CompleteRideAsync(Guid queueEntryId, string driverUsername, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverUsername, nameof(driverUsername));

        var queueEntry = await _context.QueueEntries
            .Include(q => q.Customer)
            .Include(q => q.Payment)
            .FirstOrDefaultAsync(q => q.Id == queueEntryId, cancellationToken);

        if (queueEntry == null)
        {
            throw new ArgumentException($"Queue entry with ID {queueEntryId} not found.");
        }

        if (queueEntry.Status != QueueEntryStatus.InProgress)
        {
            throw new InvalidOperationException($"Queue entry must be in progress to complete. Current status: {queueEntry.Status}");
        }

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Update queue entry
            queueEntry.Status = QueueEntryStatus.Completed;
            queueEntry.CompletedAt = DateTime.UtcNow;
            queueEntry.CompletedBy = driverUsername;

            await _context.SaveChangesAsync(cancellationToken);

            // Recalculate positions for remaining queue entries
            await RecalculatePositionsAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            // Notify about ride completion and queue update
            await _notificationService.NotifyRideStatusAsync(queueEntry, cancellationToken);

            var currentQueue = await GetCurrentQueueAsync(false, cancellationToken);
            await _notificationService.NotifyQueueUpdateAsync(currentQueue, cancellationToken);

            var nextCustomer = await GetNextCustomerAsync(cancellationToken);
            await _notificationService.NotifyDriverQueueUpdateAsync(nextCustomer, currentQueue.Count, cancellationToken);

            return queueEntry;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<QueueEntry> RemoveCustomerAsync(Guid queueEntryId, string reason, string staffUsername, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        ArgumentException.ThrowIfNullOrWhiteSpace(staffUsername, nameof(staffUsername));

        var queueEntry = await _context.QueueEntries
            .Include(q => q.Customer)
            .Include(q => q.Payment)
            .FirstOrDefaultAsync(q => q.Id == queueEntryId, cancellationToken);

        if (queueEntry == null)
        {
            throw new ArgumentException($"Queue entry with ID {queueEntryId} not found.");
        }

        if (queueEntry.Status == QueueEntryStatus.Completed || queueEntry.Status == QueueEntryStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot remove queue entry with status {queueEntry.Status}");
        }

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Update queue entry
            queueEntry.Status = QueueEntryStatus.Cancelled;
            queueEntry.CompletedAt = DateTime.UtcNow;
            queueEntry.CompletedBy = staffUsername;

            // Update payment with removal notes
            if (queueEntry.Payment != null)
            {
                queueEntry.Payment.Notes = $"{queueEntry.Payment.Notes} | Removed from queue: {reason}".Trim('|', ' ');
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Recalculate positions for remaining queue entries
            await RecalculatePositionsAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            // Notify about queue update
            var currentQueue = await GetCurrentQueueAsync(false, cancellationToken);
            await _notificationService.NotifyQueueUpdateAsync(currentQueue, cancellationToken);

            return queueEntry;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<QueueEntry>> ReorderAsync(Guid[] queueOrder, string staffUsername, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueOrder, nameof(queueOrder));
        ArgumentException.ThrowIfNullOrWhiteSpace(staffUsername, nameof(staffUsername));

        if (queueOrder.Length == 0)
        {
            throw new ArgumentException("Queue order cannot be empty", nameof(queueOrder));
        }

        // Check for duplicates
        if (queueOrder.Length != queueOrder.Distinct().Count())
        {
            throw new ArgumentException("Queue order contains duplicate entries", nameof(queueOrder));
        }

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Get current active queue entries
            var activeEntries = await _context.QueueEntries
                .Where(q => q.Status == QueueEntryStatus.Waiting || q.Status == QueueEntryStatus.InProgress)
                .ToListAsync(cancellationToken);

            // Validate that all provided IDs exist and are active
            var existingIds = activeEntries.Select(q => q.Id).ToHashSet();
            var missingIds = queueOrder.Where(id => !existingIds.Contains(id)).ToArray();

            if (missingIds.Length > 0)
            {
                throw new ArgumentException($"Queue entries not found or not active: {string.Join(", ", missingIds)}");
            }

            // Update positions based on provided order
            for (int i = 0; i < queueOrder.Length; i++)
            {
                var entry = activeEntries.First(q => q.Id == queueOrder[i]);
                entry.Position = i + 1;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Get updated queue with related data
            var updatedQueue = await GetCurrentQueueAsync(false, cancellationToken);

            // Notify about queue update
            await _notificationService.NotifyQueueUpdateAsync(updatedQueue, cancellationToken);

            return updatedQueue;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<QueueEntry>> SyncFromDesktopAsync(List<QueueEntry> desktopQueueState, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desktopQueueState, nameof(desktopQueueState));

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Get current cloud queue state
            var cloudQueue = await _context.QueueEntries
                .Where(q => q.Status == QueueEntryStatus.Waiting || q.Status == QueueEntryStatus.InProgress)
                .ToListAsync(cancellationToken);

            // Desktop is authoritative - update positions to match desktop state
            foreach (var desktopEntry in desktopQueueState)
            {
                var cloudEntry = cloudQueue.FirstOrDefault(c => c.Id == desktopEntry.Id);
                if (cloudEntry != null)
                {
                    cloudEntry.Position = desktopEntry.Position;
                    cloudEntry.Status = desktopEntry.Status;

                    // Update timestamps if changed
                    if (desktopEntry.StartedAt.HasValue && !cloudEntry.StartedAt.HasValue)
                    {
                        cloudEntry.StartedAt = desktopEntry.StartedAt;
                    }

                    if (desktopEntry.CompletedAt.HasValue && !cloudEntry.CompletedAt.HasValue)
                    {
                        cloudEntry.CompletedAt = desktopEntry.CompletedAt;
                        cloudEntry.CompletedBy = desktopEntry.CompletedBy;
                    }
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Get synchronized queue with related data
            var synchronizedQueue = await GetCurrentQueueAsync(false, cancellationToken);

            // Notify about queue update
            await _notificationService.NotifyQueueUpdateAsync(synchronizedQueue, cancellationToken);

            return synchronizedQueue;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<QueueEntry>> GetCurrentQueueAsync(bool includeCompleted = false, CancellationToken cancellationToken = default)
    {
        var query = _context.QueueEntries
            .Include(q => q.Customer)
            .Include(q => q.Payment)
            .AsNoTracking();

        if (includeCompleted)
        {
            query = query.Where(q => q.Status == QueueEntryStatus.Waiting ||
                                     q.Status == QueueEntryStatus.InProgress ||
                                     (q.Status == QueueEntryStatus.Completed && q.CompletedAt >= DateTime.UtcNow.AddHours(-24)));
        }
        else
        {
            query = query.Where(q => q.Status == QueueEntryStatus.Waiting || q.Status == QueueEntryStatus.InProgress);
        }

        return await query
            .OrderBy(q => q.Position)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueueEntry?> GetNextCustomerAsync(CancellationToken cancellationToken = default)
    {
        return await _context.QueueEntries
            .Include(q => q.Customer)
            .Include(q => q.Payment)
            .AsNoTracking()
            .Where(q => q.Status == QueueEntryStatus.Waiting)
            .OrderBy(q => q.Position)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueueEntry> StartRideAsync(Guid queueEntryId, string driverUsername, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverUsername, nameof(driverUsername));

        var queueEntry = await _context.QueueEntries
            .Include(q => q.Customer)
            .Include(q => q.Payment)
            .FirstOrDefaultAsync(q => q.Id == queueEntryId, cancellationToken);

        if (queueEntry == null)
        {
            throw new ArgumentException($"Queue entry with ID {queueEntryId} not found.");
        }

        if (queueEntry.Status != QueueEntryStatus.Waiting)
        {
            throw new InvalidOperationException($"Queue entry must be waiting to start. Current status: {queueEntry.Status}");
        }

        queueEntry.Status = QueueEntryStatus.InProgress;
        queueEntry.StartedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Notify about ride status change
        await _notificationService.NotifyRideStatusAsync(queueEntry, cancellationToken);

        return queueEntry;
    }

    /// <inheritdoc />
    public async Task<QueueEntry?> GetByIdAsync(Guid queueEntryId, bool includeRelated = true, CancellationToken cancellationToken = default)
    {
        var query = _context.QueueEntries.AsQueryable();

        if (includeRelated)
        {
            query = query.Include(q => q.Customer)
                         .Include(q => q.Payment);
        }

        return await query
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == queueEntryId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> RecalculatePositionsAsync(CancellationToken cancellationToken = default)
    {
        var activeEntries = await _context.QueueEntries
            .Where(q => q.Status == QueueEntryStatus.Waiting || q.Status == QueueEntryStatus.InProgress)
            .OrderBy(q => q.Position)
            .ToListAsync(cancellationToken);

        int updatedCount = 0;
        for (int i = 0; i < activeEntries.Count; i++)
        {
            var expectedPosition = i + 1;
            if (activeEntries[i].Position != expectedPosition)
            {
                activeEntries[i].Position = expectedPosition;
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return updatedCount;
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, string[] Errors)> ValidateQueueOperationAsync(string operation, Guid? queueEntryId = null, object? additionalData = null)
    {
        await Task.CompletedTask; // Placeholder for async pattern consistency

        var errors = new List<string>();

        ArgumentException.ThrowIfNullOrWhiteSpace(operation, nameof(operation));

        switch (operation.ToLower())
        {
            case "add":
                // Validation for adding to queue handled in AddToQueueAsync
                break;

            case "complete":
            case "start":
            case "remove":
                if (!queueEntryId.HasValue)
                {
                    errors.Add("Queue entry ID is required for this operation");
                }
                break;

            case "reorder":
                if (additionalData is not Guid[] queueOrder)
                {
                    errors.Add("Queue order array is required for reorder operation");
                }
                else if (queueOrder.Length == 0)
                {
                    errors.Add("Queue order cannot be empty");
                }
                else if (queueOrder.Length != queueOrder.Distinct().Count())
                {
                    errors.Add("Queue order contains duplicate entries");
                }
                break;

            default:
                errors.Add($"Unknown queue operation: {operation}");
                break;
        }

        return (errors.Count == 0, errors.ToArray());
    }
}