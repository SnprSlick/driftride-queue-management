using DriftRide.Data;
using DriftRide.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DriftRide.Services;

/// <summary>
/// Service implementation for payment processing and verification operations.
/// Handles payment creation, manual confirmation, validation, and API integration.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly DriftRideDbContext _context;
    private readonly IQueueService _queueService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PaymentService> _logger;

    /// <summary>
    /// Initializes a new instance of the PaymentService.
    /// </summary>
    /// <param name="context">Database context for Entity Framework operations</param>
    /// <param name="queueService">Queue service for confirmed payments</param>
    /// <param name="notificationService">Notification service for real-time updates</param>
    /// <param name="logger">Logger instance for structured logging</param>
    public PaymentService(
        DriftRideDbContext context,
        IQueueService queueService,
        INotificationService notificationService,
        ILogger<PaymentService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Payment> ProcessAsync(Guid customerId, decimal amount, PaymentMethod paymentMethod, string? externalTransactionId = null, CancellationToken cancellationToken = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "PaymentProcess",
            ["CustomerId"] = customerId,
            ["Amount"] = amount,
            ["PaymentMethod"] = paymentMethod.ToString(),
            ["ExternalTransactionId"] = externalTransactionId ?? "None"
        });

        _logger.LogInformation("Starting payment processing for customer {CustomerId} - Amount: {Amount:C}, Method: {PaymentMethod}",
            customerId, amount, paymentMethod);

        var validationResult = await ValidatePaymentDataAsync(customerId, amount, paymentMethod, externalTransactionId);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Payment validation failed for customer {CustomerId}: {ValidationErrors}",
                customerId, string.Join(", ", validationResult.Errors));
            throw new ArgumentException($"Payment data validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        // Check if customer has active pending payment
        if (await HasActivePendingPaymentAsync(customerId, cancellationToken))
        {
            _logger.LogWarning("Payment creation blocked for customer {CustomerId}: Customer already has pending payment",
                customerId);
            throw new InvalidOperationException("Customer already has a pending payment. Only one active payment per customer is allowed.");
        }

        // Verify customer exists
        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer == null)
        {
            _logger.LogWarning("Payment creation failed: Customer {CustomerId} not found", customerId);
            throw new ArgumentException($"Customer with ID {customerId} not found.");
        }

        var paymentId = Guid.NewGuid();
        var payment = new Payment
        {
            Id = paymentId,
            CustomerId = customerId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            Status = PaymentStatus.Pending,
            ExternalTransactionId = externalTransactionId,
            CreatedAt = DateTime.UtcNow
        };

        _logger.LogDebug("Creating payment record {PaymentId} for customer {CustomerId}", paymentId, customerId);

        try
        {
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Payment {PaymentId} created successfully for customer {CustomerId} - Status: Pending",
                paymentId, customerId);

            // Log business event
            _logger.LogInformation("Business Event: Payment Creation - {PaymentId} {CustomerId} {Amount:C} {PaymentMethod} {ExternalTransactionId}",
                paymentId, customerId, amount, paymentMethod, externalTransactionId ?? "None");

            // Load payment with customer for notification
            payment = await GetByIdAsync(payment.Id, true, cancellationToken) ?? payment;

            // Notify about new pending payment
            await _notificationService.NotifyPaymentStatusAsync(payment, cancellationToken);

            return payment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create payment {PaymentId} for customer {CustomerId}",
                paymentId, customerId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Payment> ConfirmAsync(Guid paymentId, bool confirmed, string? notes, string staffUsername, CancellationToken cancellationToken = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "PaymentConfirm",
            ["PaymentId"] = paymentId,
            ["Confirmed"] = confirmed,
            ["StaffUsername"] = staffUsername
        });

        _logger.LogInformation("Starting payment {Action} for payment {PaymentId} by staff {StaffUsername}",
            confirmed ? "confirmation" : "denial", paymentId, staffUsername);

        ArgumentException.ThrowIfNullOrWhiteSpace(staffUsername, nameof(staffUsername));

        var payment = await _context.Payments
            .Include(p => p.Customer)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

        if (payment == null)
        {
            _logger.LogWarning("Payment confirmation failed: Payment {PaymentId} not found", paymentId);
            throw new ArgumentException($"Payment with ID {paymentId} not found.");
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            _logger.LogWarning("Payment confirmation failed: Payment {PaymentId} is in {PaymentStatus} status, expected Pending",
                paymentId, payment.Status);
            throw new InvalidOperationException($"Payment is in {payment.Status} status and cannot be confirmed or denied. Only pending payments can be processed.");
        }

        _logger.LogDebug("Payment {PaymentId} found - Customer: {CustomerId}, Amount: {Amount:C}, Method: {PaymentMethod}",
            paymentId, payment.CustomerId, payment.Amount, payment.PaymentMethod);

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Starting transaction for payment {Action}", confirmed ? "confirmation" : "denial");

            // Update payment status
            var previousStatus = payment.Status;
            payment.Status = confirmed ? PaymentStatus.Confirmed : PaymentStatus.Denied;
            payment.ConfirmedAt = DateTime.UtcNow;
            payment.ConfirmedBy = staffUsername;
            payment.Notes = notes?.Trim();

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Payment {PaymentId} status updated from {PreviousStatus} to {NewStatus}",
                paymentId, previousStatus, payment.Status);

            // If confirmed, add to queue
            if (confirmed)
            {
                var queueEntry = await _queueService.AddToQueueAsync(payment.Id, cancellationToken);
                _logger.LogInformation("Customer {CustomerId} added to queue at position {Position} after payment confirmation",
                    payment.CustomerId, queueEntry.Position);
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Transaction committed for payment {Action}", confirmed ? "confirmation" : "denial");

            _logger.LogInformation("Payment {PaymentId} {Action} completed by staff {StaffUsername} - Customer: {CustomerId}",
                paymentId, confirmed ? "confirmed" : "denied", staffUsername, payment.CustomerId);

            // Log business event
            _logger.LogInformation("Business Event: Payment {Action} - {PaymentId} {CustomerId} {Amount:C} Staff:{StaffUsername} Notes:{Notes}",
                confirmed ? "Confirmation" : "Denial", paymentId, payment.CustomerId, payment.Amount, staffUsername, notes ?? "None");

            // Notify about payment status change
            await _notificationService.NotifyPaymentStatusAsync(payment, cancellationToken);

            return payment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to {Action} payment {PaymentId} by staff {StaffUsername}",
                confirmed ? "confirm" : "deny", paymentId, staffUsername);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<(bool IsVerified, string? VerificationNotes)> VerifyAutomaticallyAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _context.Payments
            .Include(p => p.Customer)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

        if (payment == null)
        {
            throw new ArgumentException($"Payment with ID {paymentId} not found.");
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException($"Payment is not in pending status. Current status: {payment.Status}");
        }

        // Get payment method configuration to check API integration
        var paymentConfig = await _context.PaymentConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(pc => pc.PaymentMethod == payment.PaymentMethod, cancellationToken);

        if (paymentConfig == null || !paymentConfig.ApiIntegrationEnabled)
        {
            throw new NotSupportedException($"API verification is not enabled for payment method {payment.PaymentMethod}");
        }

        // TODO: Implement actual API verification logic here
        // This is a placeholder for future API integration
        // Different payment methods would have different verification APIs

        var isVerified = false;
        var verificationNotes = $"API verification not yet implemented for {payment.PaymentMethod}";

        switch (payment.PaymentMethod)
        {
            case PaymentMethod.CashApp:
                // TODO: Implement CashApp API verification
                verificationNotes = "CashApp API verification pending implementation";
                break;
            case PaymentMethod.PayPal:
                // TODO: Implement PayPal API verification
                verificationNotes = "PayPal API verification pending implementation";
                break;
            case PaymentMethod.CashInHand:
                // Cash in hand doesn't support API verification
                throw new NotSupportedException("Cash in hand payments cannot be verified automatically");
            default:
                throw new NotSupportedException($"Unknown payment method: {payment.PaymentMethod}");
        }

        return (isVerified, verificationNotes);
    }

    /// <inheritdoc />
    public async Task<List<Payment>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Include(p => p.Customer)
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Pending)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Payment?> GetByIdAsync(Guid paymentId, bool includeCustomer = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Payments.AsQueryable();

        if (includeCustomer)
        {
            query = query.Include(p => p.Customer);
        }

        return await query
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<Payment>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Include(p => p.Customer)
            .AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, string[] Errors)> ValidatePaymentDataAsync(Guid customerId, decimal amount, PaymentMethod paymentMethod, string? externalTransactionId = null)
    {
        var errors = new List<string>();

        // Validate amount
        if (amount <= 0)
        {
            errors.Add("Payment amount must be greater than zero");
        }

        // Validate external transaction ID requirements
        if (paymentMethod == PaymentMethod.CashApp || paymentMethod == PaymentMethod.PayPal)
        {
            if (string.IsNullOrWhiteSpace(externalTransactionId))
            {
                errors.Add($"External transaction ID is required for {paymentMethod} payments");
            }
            else if (externalTransactionId.Length > 255)
            {
                errors.Add("External transaction ID cannot exceed 255 characters");
            }
        }

        // Validate customer exists
        var customerExists = await _context.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Id == customerId);

        if (!customerExists)
        {
            errors.Add($"Customer with ID {customerId} does not exist");
        }

        // Check payment method configuration
        var paymentConfig = await _context.PaymentConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(pc => pc.PaymentMethod == paymentMethod);

        if (paymentConfig != null && !paymentConfig.IsEnabled)
        {
            errors.Add($"Payment method {paymentMethod} is currently disabled");
        }

        return (errors.Count == 0, errors.ToArray());
    }

    /// <inheritdoc />
    public async Task<bool> HasActivePendingPaymentAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .AsNoTracking()
            .AnyAsync(p => p.CustomerId == customerId && p.Status == PaymentStatus.Pending, cancellationToken);
    }
}