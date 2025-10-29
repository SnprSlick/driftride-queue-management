using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DriftRide.Web.Services;
using DriftRide.Web.Models;

namespace DriftRide.Web.Controllers;

/// <summary>
/// Controller for driver interface operations.
/// Handles driver queue management and ride completion functionality.
/// </summary>
public class DriverController : Controller
{
    private readonly ILogger<DriverController> _logger;
    private readonly IDriftRideApiService _apiService;

    /// <summary>
    /// Initializes a new instance of the DriverController.
    /// </summary>
    public DriverController(
        ILogger<DriverController> logger,
        IDriftRideApiService apiService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
    }

    /// <summary>
    /// Displays the main driver dashboard interface.
    /// Shows current customer and queue status.
    /// </summary>
    public IActionResult Dashboard()
    {
        var model = new DriverDashboardViewModel
        {
            ConnectionStatus = "Connecting...",
            QueueLength = 0,
            CurrentCustomer = null,
            LastUpdated = DateTime.Now
        };

        return View(model);
    }

    /// <summary>
    /// AJAX endpoint to get current customer for driver.
    /// Called by JavaScript for real-time updates.
    /// </summary>
    /// <returns>JSON response with current customer or null if queue empty</returns>
    [HttpGet]
    public async Task<IActionResult> GetCurrentCustomer()
    {
        try
        {
            // In a real implementation, we'd get the auth token from the session
            // For now, we'll use a placeholder
            var authToken = "placeholder-driver-token";

            var currentCustomer = await _apiService.GetCurrentCustomerAsync(authToken);

            return Json(new { success = true, customer = currentCustomer });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current customer");
            return Json(new { success = false, error = "Failed to get current customer" });
        }
    }

    /// <summary>
    /// AJAX endpoint to complete a ride.
    /// Called when driver clicks "Complete Ride" button.
    /// </summary>
    /// <param name="queueEntryId">ID of the queue entry to complete</param>
    /// <returns>JSON response indicating success or failure</returns>
    [HttpPost]
    public async Task<IActionResult> CompleteRide([FromBody] CompleteRideRequest request)
    {
        try
        {
            if (request?.QueueEntryId == null || request.QueueEntryId == Guid.Empty)
            {
                return Json(new { success = false, error = "Invalid queue entry ID" });
            }

            // In a real implementation, we'd get the auth token from the session
            var authToken = "placeholder-driver-token";

            var completedEntry = await _apiService.CompleteRideAsync(request.QueueEntryId, authToken);

            if (completedEntry != null)
            {
                return Json(new { success = true, completedEntry });
            }
            else
            {
                return Json(new { success = false, error = "Queue entry not found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing ride for queue entry {QueueEntryId}", request?.QueueEntryId);
            return Json(new { success = false, error = "Failed to complete ride" });
        }
    }

    /// <summary>
    /// AJAX endpoint to get full queue status.
    /// Used for context and next customer preview.
    /// </summary>
    /// <returns>JSON response with queue entries</returns>
    [HttpGet]
    public async Task<IActionResult> GetQueueStatus()
    {
        try
        {
            var authToken = "placeholder-driver-token";

            var queue = await _apiService.GetQueueAsync(authToken);

            return Json(new { success = true, queue });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue status");
            return Json(new { success = false, error = "Failed to get queue status" });
        }
    }
}

/// <summary>
/// Request model for completing rides.
/// </summary>
public class CompleteRideRequest
{
    public Guid QueueEntryId { get; set; }
}