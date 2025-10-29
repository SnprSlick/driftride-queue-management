using Microsoft.AspNetCore.Mvc;
using DriftRide.Web.Models;
using DriftRide.Web.Services;

namespace DriftRide.Web.Controllers;

/// <summary>
/// Payment configuration management controller for sales staff
/// Handles payment method configuration display and updates
/// </summary>
public class PaymentConfigurationController : Controller
{
    private readonly IDriftRideApiService _apiService;
    private readonly ILogger<PaymentConfigurationController> _logger;

    public PaymentConfigurationController(
        IDriftRideApiService apiService,
        ILogger<PaymentConfigurationController> logger)
    {
        _apiService = apiService;
        _logger = logger;
    }

    /// <summary>
    /// Display payment configuration management page (Sales role required)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var authToken = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(authToken))
            {
                return RedirectToAction("Login", "Sales");
            }

            var viewModel = new PaymentConfigurationManagementViewModel();

            var response = await _apiService.GetPaymentConfigurationAsync(authToken);
            if (response.Success && response.Data != null)
            {
                viewModel.PaymentConfigurations = response.Data;
            }
            else
            {
                viewModel.ErrorMessages.AddRange(response.Errors ?? new List<string> { "Failed to load payment configuration" });
            }

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading payment configuration page");
            var errorModel = new PaymentConfigurationManagementViewModel();
            errorModel.ErrorMessages.Add("An unexpected error occurred while loading the configuration.");
            return View(errorModel);
        }
    }

    /// <summary>
    /// Get payment configuration data as JSON for AJAX requests
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetConfiguration()
    {
        try
        {
            var authToken = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(authToken))
            {
                return Json(new { success = false, message = "Authentication required" });
            }

            var response = await _apiService.GetPaymentConfigurationAsync(authToken);

            if (response.Success)
            {
                return Json(new
                {
                    success = true,
                    data = response.Data,
                    message = response.Message
                });
            }

            return Json(new
            {
                success = false,
                message = response.Message,
                errors = response.Errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching payment configuration via AJAX");
            return Json(new
            {
                success = false,
                message = "An unexpected error occurred while fetching configuration"
            });
        }
    }

    /// <summary>
    /// Update payment configuration via AJAX
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UpdateConfiguration([FromBody] UpdatePaymentConfigurationRequest request)
    {
        try
        {
            var authToken = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(authToken))
            {
                return Json(new { success = false, message = "Authentication required" });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return Json(new
                {
                    success = false,
                    message = "Validation failed",
                    errors = errors
                });
            }

            var response = await _apiService.UpdatePaymentConfigurationAsync(request, authToken);

            if (response.Success)
            {
                // Log configuration change
                _logger.LogInformation("Payment configuration updated: {DisplayName} by user {UserId}",
                    request.DisplayName, HttpContext.Session.GetString("UserId"));

                return Json(new
                {
                    success = true,
                    data = response.Data,
                    message = "Configuration updated successfully"
                });
            }

            return Json(new
            {
                success = false,
                message = response.Message,
                errors = response.Errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment configuration");
            return Json(new
            {
                success = false,
                message = "An unexpected error occurred while updating configuration"
            });
        }
    }

    /// <summary>
    /// Refresh customer payment methods cache
    /// </summary>
    [HttpPost]
    public IActionResult RefreshCache()
    {
        try
        {
            _apiService.InvalidatePaymentMethodsCache();
            _logger.LogInformation("Payment methods cache refreshed by user {UserId}",
                HttpContext.Session.GetString("UserId"));

            return Json(new
            {
                success = true,
                message = "Payment methods cache refreshed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing payment methods cache");
            return Json(new
            {
                success = false,
                message = "Failed to refresh cache"
            });
        }
    }

    /// <summary>
    /// Get available payment methods for customer interface (public endpoint)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCustomerPaymentMethods()
    {
        try
        {
            var response = await _apiService.GetPaymentMethodsAsync();

            if (response.Success)
            {
                // Filter to only enabled methods and exclude sensitive data
                var customerMethods = response.Data?
                    .Where(m => m.IsEnabled)
                    .Select(m => new
                    {
                        method = m.Method,
                        displayName = m.DisplayName,
                        paymentUrl = m.PaymentUrl,
                        requiresExternalApp = m.RequiresExternalApp,
                        pricePerRide = m.PricePerRide
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    data = customerMethods,
                    message = "Payment methods loaded successfully"
                });
            }

            return Json(new
            {
                success = false,
                message = response.Message,
                errors = response.Errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching customer payment methods");
            return Json(new
            {
                success = false,
                message = "Failed to load payment methods"
            });
        }
    }
}