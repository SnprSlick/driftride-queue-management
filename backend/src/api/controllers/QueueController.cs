using DriftRide.Models;
using DriftRide.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DriftRide.Api.Controllers;

/// <summary>
/// API controller for queue management operations.
/// Handles driver queue interactions and sales queue oversight.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class QueueController : ControllerBase
{
    private readonly IQueueService _queueService;
    private readonly ILogger<QueueController> _logger;

    /// <summary>
    /// Initializes a new instance of the QueueController.
    /// </summary>
    /// <param name="queueService">Service for queue management operations</param>
    /// <param name="logger">Logger for this controller</param>
    public QueueController(IQueueService queueService, ILogger<QueueController> logger)
    {
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current customer for the driver.
    /// Returns the next customer in queue or 204 if queue is empty.
    /// </summary>
    /// <returns>Current customer for driver or 204 No Content if queue is empty</returns>
    /// <response code="200">Current customer information</response>
    /// <response code="204">No customers in queue</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - Driver role required</response>
    [HttpGet("current")]
    [ProducesResponseType(typeof(QueueEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCurrentCustomer(CancellationToken cancellationToken = default)
    {
        try
        {
            var currentCustomer = await _queueService.GetNextCustomerAsync(cancellationToken);

            if (currentCustomer == null)
            {
                return NoContent();
            }

            var dto = QueueEntryDto.FromEntity(currentCustomer);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current customer");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Completes a ride for the specified queue entry.
    /// Marks the ride as completed and progresses the queue.
    /// </summary>
    /// <param name="queueEntryId">ID of the queue entry to complete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated queue entry with completion information</returns>
    /// <response code="200">Ride completed successfully</response>
    /// <response code="400">Invalid queue entry ID format</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - Driver role required</response>
    /// <response code="404">Queue entry not found</response>
    [HttpPost("{queueEntryId}/complete")]
    [ProducesResponseType(typeof(QueueEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteRide(
        [FromRoute] Guid queueEntryId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (queueEntryId == Guid.Empty)
            {
                return BadRequest("Queue entry ID cannot be empty");
            }

            // For now, use a placeholder driver username
            var driverUsername = "testdriver"; // In real implementation, get from JWT claims

            var completedEntry = await _queueService.CompleteRideAsync(queueEntryId, driverUsername, cancellationToken);

            if (completedEntry == null)
            {
                return NotFound($"Queue entry with ID {queueEntryId} not found");
            }

            var dto = QueueEntryDto.FromEntity(completedEntry);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing ride for queue entry {QueueEntryId}", queueEntryId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets the current queue state.
    /// Returns all queue entries for sales staff and drivers to view.
    /// </summary>
    /// <param name="includeCompleted">Whether to include completed entries (default: true)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of queue entries</returns>
    /// <response code="200">Current queue state</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - Sales or Driver role required</response>
    [HttpGet]
    [ProducesResponseType(typeof(QueueEntryDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetQueue(
        [FromQuery] bool includeCompleted = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queueEntries = await _queueService.GetCurrentQueueAsync(includeCompleted, cancellationToken);

            var dtos = queueEntries.Select(QueueEntryDto.FromEntity).ToArray();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue");
            return StatusCode(500, "Internal server error");
        }
    }
}

/// <summary>
/// Data transfer object for queue entry information.
/// Maps entity data to API response format.
/// </summary>
public class QueueEntryDto
{
    public Guid Id { get; set; }
    public CustomerDto Customer { get; set; } = null!;
    public PaymentDto Payment { get; set; } = null!;
    public int Position { get; set; }
    public QueueEntryStatus Status { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }

    /// <summary>
    /// Creates a DTO from a QueueEntry entity.
    /// </summary>
    /// <param name="entity">QueueEntry entity to convert</param>
    /// <returns>QueueEntryDto with mapped data</returns>
    public static QueueEntryDto FromEntity(QueueEntry entity)
    {
        return new QueueEntryDto
        {
            Id = entity.Id,
            Customer = CustomerDto.FromEntity(entity.Customer),
            Payment = PaymentDto.FromEntity(entity.Payment),
            Position = entity.Position,
            Status = entity.Status,
            QueuedAt = entity.QueuedAt,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            CompletedBy = entity.CompletedBy
        };
    }
}

/// <summary>
/// Data transfer object for customer information in queue context.
/// </summary>
public class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Creates a DTO from a Customer entity.
    /// </summary>
    public static CustomerDto FromEntity(Customer entity)
    {
        return new CustomerDto
        {
            Id = entity.Id,
            Name = entity.Name,
            PhoneNumber = entity.PhoneNumber,
            CreatedAt = entity.CreatedAt
        };
    }
}

/// <summary>
/// Data transfer object for payment information in queue context.
/// </summary>
public class PaymentDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; }

    /// <summary>
    /// Creates a DTO from a Payment entity.
    /// </summary>
    public static PaymentDto FromEntity(Payment entity)
    {
        return new PaymentDto
        {
            Id = entity.Id,
            Amount = entity.Amount,
            PaymentMethod = entity.PaymentMethod,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            ConfirmedAt = entity.ConfirmedAt,
            ConfirmedBy = entity.ConfirmedBy
        };
    }
}