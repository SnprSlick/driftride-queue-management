using DriftRide.Data;
using DriftRide.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace DriftRide.Services;

/// <summary>
/// Service implementation for customer management operations.
/// Handles customer creation, retrieval, validation, and manual queue additions.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly DriftRideDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CustomerService> _logger;

    /// <summary>
    /// Initializes a new instance of the CustomerService.
    /// </summary>
    /// <param name="context">Database context for Entity Framework operations</param>
    /// <param name="notificationService">Notification service for real-time updates</param>
    /// <param name="logger">Logger instance for structured logging</param>
    public CustomerService(
        DriftRideDbContext context,
        INotificationService notificationService,
        ILogger<CustomerService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Customer> CreateAsync(string name, string email, string? phoneNumber = null, CancellationToken cancellationToken = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "CustomerCreate",
            ["CustomerName"] = name,
            ["CustomerEmail"] = email
        });

        _logger.LogInformation("Starting customer creation for {CustomerName} with email {CustomerEmail}",
            name, email);

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));

        var validationResult = await ValidateCustomerDataAsync(name, email, phoneNumber);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Customer validation failed for {CustomerName}: {ValidationErrors}",
                name, string.Join(", ", validationResult.Errors));
            throw new ArgumentException($"Customer data validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        var customerId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = customerId,
            Name = name.Trim(),
            Email = email.Trim(),
            PhoneNumber = phoneNumber?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _logger.LogDebug("Creating customer record with ID {CustomerId}", customerId);

        try
        {
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Customer {CustomerId} created successfully for {CustomerName}",
                customerId, name);

            // Log business event
            _logger.LogInformation("Business Event: Customer Registration - {CustomerId} {CustomerName} {CustomerEmail}",
                customerId, name, email);

            return customer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create customer {CustomerId} for {CustomerName}",
                customerId, name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<QueueEntry> AddManuallyAsync(string name, string email, string? phoneNumber, string reason, string staffUsername, CancellationToken cancellationToken = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "CustomerManualAdd",
            ["CustomerName"] = name,
            ["CustomerEmail"] = email,
            ["StaffUsername"] = staffUsername,
            ["Reason"] = reason
        });

        _logger.LogInformation("Starting manual customer addition for {CustomerName} by staff {StaffUsername} - Reason: {Reason}",
            name, staffUsername, reason);

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        ArgumentException.ThrowIfNullOrWhiteSpace(staffUsername, nameof(staffUsername));

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Starting transaction for manual customer addition");

            // Create customer
            var customer = await CreateAsync(name, email, phoneNumber, cancellationToken);
            _logger.LogDebug("Customer {CustomerId} created for manual addition", customer.Id);

            // Create manual payment record (CashInHand method for manual additions)
            var paymentId = Guid.NewGuid();
            var payment = new Payment
            {
                Id = paymentId,
                CustomerId = customer.Id,
                Amount = 0, // Manual additions don't require payment amount
                PaymentMethod = PaymentMethod.CashInHand,
                Status = PaymentStatus.Confirmed,
                ExternalTransactionId = null,
                CreatedAt = DateTime.UtcNow,
                ConfirmedAt = DateTime.UtcNow,
                ConfirmedBy = staffUsername,
                Notes = $"Manual addition - {reason}"
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Payment {PaymentId} created for manual addition", paymentId);

            // Get next position (max position + 1)
            var nextPosition = await _context.QueueEntries
                .Where(q => q.Status == QueueEntryStatus.Waiting || q.Status == QueueEntryStatus.InProgress)
                .MaxAsync(q => (int?)q.Position, cancellationToken) ?? 0;

            nextPosition++;
            _logger.LogDebug("Assigning queue position {Position} to customer {CustomerId}", nextPosition, customer.Id);

            // Create queue entry directly
            var queueEntryId = Guid.NewGuid();
            var queueEntry = new QueueEntry
            {
                Id = queueEntryId,
                CustomerId = customer.Id,
                PaymentId = payment.Id,
                Position = nextPosition,
                Status = QueueEntryStatus.Waiting,
                QueuedAt = DateTime.UtcNow
            };

            _context.QueueEntries.Add(queueEntry);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Queue entry {QueueEntryId} created at position {Position}", queueEntryId, nextPosition);

            await transaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Transaction committed for manual customer addition");

            // Load with related data for return
            queueEntry = await _context.QueueEntries
                .Include(q => q.Customer)
                .Include(q => q.Payment)
                .FirstAsync(q => q.Id == queueEntry.Id, cancellationToken);

            _logger.LogInformation("Manual customer addition completed: {CustomerId} added to queue at position {Position} by {StaffUsername}",
                customer.Id, nextPosition, staffUsername);

            // Log business event
            _logger.LogInformation("Business Event: Manual Queue Entry - {CustomerId} {CustomerName} Position:{Position} Staff:{StaffUsername} Reason:{Reason}",
                customer.Id, name, nextPosition, staffUsername, reason);

            // Notify about queue update
            await _notificationService.NotifyCustomerAttentionAsync(customer, $"Manually added to queue: {reason}", cancellationToken);

            return queueEntry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to manually add customer {CustomerName} to queue by staff {StaffUsername}",
                name, staffUsername);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<Customer>> GetByNameAsync(string name, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        var query = _context.Customers
            .AsNoTracking()
            .Where(c => c.Name.ToLower().Contains(name.ToLower()));

        if (fromDate.HasValue)
        {
            query = query.Where(c => c.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(c => c.CreatedAt <= toDate.Value);
        }

        return await query
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, string[] Errors)> ValidateCustomerDataAsync(string name, string email, string? phoneNumber = null)
    {
        await Task.CompletedTask; // Placeholder for async pattern consistency

        var errors = new List<string>();

        // Validate name
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Customer name is required");
        }
        else if (name.Length > 100)
        {
            errors.Add("Customer name cannot exceed 100 characters");
        }
        else if (name.Trim().Length == 0)
        {
            errors.Add("Customer name cannot be only whitespace");
        }

        // Validate email
        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add("Customer email is required");
        }
        else if (email.Length > 255)
        {
            errors.Add("Customer email cannot exceed 255 characters");
        }
        else if (!IsValidEmail(email))
        {
            errors.Add("Customer email format is invalid");
        }

        // Validate phone number if provided
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            if (phoneNumber.Length > 20)
            {
                errors.Add("Phone number cannot exceed 20 characters");
            }
            else if (!IsValidPhoneNumber(phoneNumber))
            {
                errors.Add("Phone number format is invalid");
            }
        }

        return (errors.Count == 0, errors.ToArray());
    }

    /// <summary>
    /// Validates phone number format using regex pattern.
    /// Accepts various international formats and common separators.
    /// </summary>
    /// <param name="phoneNumber">Phone number to validate</param>
    /// <returns>True if format is valid, false otherwise</returns>
    private static bool IsValidPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return false;
        }

        // Remove common separators and spaces
        var cleaned = Regex.Replace(phoneNumber, @"[\s\-\(\)\+\.]", "");

        // Check if remaining characters are all digits
        if (!Regex.IsMatch(cleaned, @"^\d+$"))
        {
            return false;
        }

        // Check length (7-15 digits is standard for international numbers)
        return cleaned.Length >= 7 && cleaned.Length <= 15;
    }

    /// <summary>
    /// Validates email address format using regex pattern.
    /// Follows standard email format validation.
    /// </summary>
    /// <param name="email">Email address to validate</param>
    /// <returns>True if format is valid, false otherwise</returns>
    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            // Use built-in .NET email validation approach
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}