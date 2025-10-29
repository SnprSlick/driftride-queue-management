using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DriftRide.Services;
using DriftRide.Models;
using DriftRide.Api.Shared;
using ApiModels = DriftRide.Api.Models;

namespace DriftRide.Api.Controllers;

/// <summary>
/// Controller for payment processing and verification operations.
/// Handles payment creation, confirmation, and retrieval following the OpenAPI specification.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PaymentsController : BaseApiController
{
    private readonly IPaymentService _paymentService;
    private readonly IQueueService _queueService;
    private readonly INotificationService _notificationService;

    /// <summary>
    /// Initializes a new instance of the PaymentsController.
    /// </summary>
    /// <param name="paymentService">Payment service for business operations</param>
    /// <param name="queueService">Queue service for queue management</param>
    /// <param name="notificationService">Notification service for real-time updates</param>
    /// <param name="logger">Logger instance</param>
    public PaymentsController(
        IPaymentService paymentService,
        IQueueService queueService,
        INotificationService notificationService,
        ILogger<PaymentsController> logger) : base(logger)
    {
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    /// <summary>
    /// Records a payment attempt for a customer.
    /// Creates payment record in Pending status awaiting verification.
    /// </summary>
    /// <param name="request">Payment creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created payment response</returns>
    /// <response code="201">Payment created successfully</response>
    /// <response code="400">Bad request - validation failed</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - insufficient permissions</response>
    /// <response code="404">Customer not found</response>
    /// <response code="409">Conflict - customer already has pending payment</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ApiModels.Payment>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<ApiModels.Payment>>> CreatePayment(
        [FromBody] ApiModels.CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate model state
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        // Check authorization - only Sales role can create payments
        if (!HasRole("Sales"))
        {
            return ForbiddenError("Only sales staff can create payments");
        }

        try
        {
            // Convert API ID to domain ID
            var domainCustomerId = IdMapper.MapCustomerToDomainId(request.CustomerId);
            if (domainCustomerId == Guid.Empty)
            {
                return NotFoundError("Customer", request.CustomerId);
            }

            // Validate payment data using service
            var (isValid, errors) = await _paymentService.ValidatePaymentDataAsync(
                domainCustomerId,
                request.Amount,
                request.PaymentMethod,
                request.ExternalTransactionId);

            if (!isValid)
            {
                var errorMessage = string.Join("; ", errors);
                Logger.LogWarning("Payment validation failed for customer {CustomerId}: {Errors}",
                    request.CustomerId, errorMessage);
                return BadRequestError(errorMessage);
            }

            // Check if customer has existing pending payment
            var hasPendingPayment = await _paymentService.HasActivePendingPaymentAsync(domainCustomerId, cancellationToken);
            if (hasPendingPayment)
            {
                return ConflictError("Customer already has a pending payment. Only one pending payment is allowed per customer.");
            }

            // Create payment using the service
            var domainPayment = await _paymentService.ProcessAsync(
                domainCustomerId,
                request.Amount,
                request.PaymentMethod,
                request.ExternalTransactionId,
                cancellationToken);

            // Map domain model to API model
            var apiPayment = await MapToApiModelAsync(domainPayment);

            Logger.LogInformation("Payment created successfully with ID {PaymentId} for customer {CustomerId} by user {UserId}",
                domainPayment.Id, domainPayment.CustomerId, CurrentUserId);

            // Send real-time notification
            await _notificationService.NotifyPaymentStatusAsync(domainPayment);

            var response = new ApiResponse<ApiModels.Payment>
            {
                Success = true,
                Message = "Payment created successfully",
                Data = apiPayment
            };

            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning("Payment creation failed due to validation: {Message}", ex.Message);
            return BadRequestError(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning("Payment creation failed due to business rule: {Message}", ex.Message);
            return ConflictError(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during payment creation");
            return InternalServerError(ex, "An error occurred while creating the payment");
        }
    }

    /// <summary>
    /// Sales staff confirms or denies a payment manually.
    /// Updates payment status and creates audit trail. Triggers queue entry creation if confirmed.
    /// </summary>
    /// <param name="id">Payment identifier</param>
    /// <param name="request">Payment confirmation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated payment response</returns>
    /// <response code="200">Payment confirmed successfully</response>
    /// <response code="400">Bad request - validation failed</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - insufficient permissions</response>
    /// <response code="404">Payment not found</response>
    /// <response code="409">Conflict - payment already confirmed/denied</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("{id:int}/confirm")]
    [ProducesResponseType(typeof(ApiResponse<ApiModels.Payment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<ApiModels.Payment>>> ConfirmPayment(
        int id,
        [FromBody] ApiModels.ConfirmPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate model state
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        // Check authorization - only Sales role can confirm payments
        if (!HasRole("Sales"))
        {
            return ForbiddenError("Only sales staff can confirm payments");
        }

        try
        {
            // Convert API ID to domain ID
            var domainPaymentId = IdMapper.MapPaymentToDomainId(id);
            if (domainPaymentId == Guid.Empty)
            {
                return NotFoundError("Payment", id);
            }

            // Confirm payment using the service
            var domainPayment = await _paymentService.ConfirmAsync(
                domainPaymentId,
                request.Confirmed,
                request.Notes,
                CurrentUsername,
                cancellationToken);

            // If payment was confirmed, add to queue
            if (request.Confirmed && domainPayment.Status == PaymentStatus.Confirmed)
            {
                try
                {
                    var queueEntry = await _queueService.AddToQueueAsync(domainPayment.Id, cancellationToken);
                    Logger.LogInformation("Customer {CustomerId} added to queue with entry ID {QueueEntryId}",
                        domainPayment.CustomerId, queueEntry.Id);
                }
                catch (Exception queueEx)
                {
                    Logger.LogError(queueEx, "Failed to add customer to queue after payment confirmation for payment {PaymentId}",
                        domainPayment.Id);
                    // Payment confirmation still succeeded, so we continue
                }
            }

            // Map domain model to API model
            var apiPayment = await MapToApiModelAsync(domainPayment);

            Logger.LogInformation("Payment {PaymentId} {Status} by user {UserId}",
                domainPayment.Id, request.Confirmed ? "confirmed" : "denied", CurrentUserId);

            // Send real-time notifications
            await _notificationService.NotifyPaymentStatusAsync(domainPayment);

            return Success(apiPayment, $"Payment {(request.Confirmed ? "confirmed" : "denied")} successfully");
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning("Payment confirmation failed due to validation: {Message}", ex.Message);
            return BadRequestError(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning("Payment confirmation failed due to business rule: {Message}", ex.Message);
            return ConflictError(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during payment confirmation for payment {PaymentId}", id);
            return InternalServerError(ex, "An error occurred while confirming the payment");
        }
    }

    /// <summary>
    /// Retrieves all payments pending staff confirmation.
    /// Used by sales dashboard to show payments awaiting verification.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending payments</returns>
    /// <response code="200">Pending payments retrieved successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - insufficient permissions</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(ApiResponse<ApiModels.Payment[]>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<ApiModels.Payment[]>>> GetPendingPayments(
        CancellationToken cancellationToken = default)
    {
        // Check authorization - only Sales role can view pending payments
        if (!HasRole("Sales"))
        {
            return ForbiddenError("Only sales staff can view pending payments");
        }

        try
        {
            // Get pending payments using the service
            var domainPayments = await _paymentService.GetPendingAsync(cancellationToken);

            // Map domain models to API models
            var apiPayments = new List<ApiModels.Payment>();
            foreach (var domainPayment in domainPayments)
            {
                var apiPayment = await MapToApiModelAsync(domainPayment);
                apiPayments.Add(apiPayment);
            }

            Logger.LogInformation("Retrieved {Count} pending payments for user {UserId}",
                apiPayments.Count, CurrentUserId);

            return Success(apiPayments.ToArray(), "Pending payments retrieved successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during pending payments retrieval");
            return InternalServerError(ex, "An error occurred while retrieving pending payments");
        }
    }

    /// <summary>
    /// Maps a domain Payment model to API Payment model.
    /// Handles the conversion between Guid and int IDs and includes customer information.
    /// </summary>
    /// <param name="domainPayment">Domain payment model</param>
    /// <returns>API payment model</returns>
    private async Task<ApiModels.Payment> MapToApiModelAsync(Payment domainPayment)
    {
        return new ApiModels.Payment
        {
            Id = IdMapper.MapPaymentToApiId(domainPayment.Id),
            CustomerId = IdMapper.MapCustomerToApiId(domainPayment.CustomerId),
            Amount = domainPayment.Amount,
            PaymentMethod = domainPayment.PaymentMethod,
            Status = domainPayment.Status,
            ExternalTransactionId = domainPayment.ExternalTransactionId,
            CreatedAt = domainPayment.CreatedAt,
            ConfirmedAt = domainPayment.ConfirmedAt,
            ConfirmedByUsername = domainPayment.ConfirmedBy,
            ConfirmationNotes = domainPayment.Notes,
            CustomerName = domainPayment.Customer?.Name
        };
    }

    // ID mapping is now handled by the shared IdMapper class
}