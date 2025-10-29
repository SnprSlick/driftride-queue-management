using Microsoft.AspNetCore.Mvc;
using DriftRide.Web.Models;
using DriftRide.Web.Services;

namespace DriftRide.Web.Controllers;

public class CustomerController : Controller
{
    private readonly ILogger<CustomerController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDriftRideApiService _apiService;

    public CustomerController(
        ILogger<CustomerController> logger,
        IConfiguration configuration,
        IDriftRideApiService apiService)
    {
        _logger = logger;
        _configuration = configuration;
        _apiService = apiService;
    }

    /// <summary>
    /// Main customer entry point - mobile-responsive registration and payment flow
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var viewModel = new CustomerViewModel
        {
            ApiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7000",
            SignalRHubUrl = _configuration["SignalR:HubUrl"] ?? "https://localhost:7000/queuehub"
        };

        try
        {
            // Load payment methods from API
            var paymentMethodsResponse = await _apiService.GetPaymentMethodsAsync();
            if (paymentMethodsResponse.Success && paymentMethodsResponse.Data != null)
            {
                viewModel.PaymentMethods = paymentMethodsResponse.Data;

                // Set ride price from first available payment method
                var firstMethod = paymentMethodsResponse.Data.FirstOrDefault();
                if (firstMethod != null)
                {
                    viewModel.RidePrice = firstMethod.PricePerRide;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading payment methods for customer page");
            // Continue with default values - the JavaScript will handle API calls as fallback
        }

        return View(viewModel);
    }

    /// <summary>
    /// Success page after payment completion and queue entry
    /// </summary>
    public IActionResult Success()
    {
        return View();
    }

    /// <summary>
    /// Error handling for payment failures
    /// </summary>
    public IActionResult Error(string message = null)
    {
        var viewModel = new CustomerErrorViewModel
        {
            ErrorMessage = message ?? "An error occurred during your registration. Please try again or contact sales staff for assistance."
        };
        return View(viewModel);
    }

    /// <summary>
    /// API endpoint to get payment methods (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPaymentMethods()
    {
        try
        {
            var response = await _apiService.GetPaymentMethodsAsync();
            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching payment methods via AJAX");
            return Json(new
            {
                success = false,
                message = "Unable to load payment methods",
                errors = new[] { ex.Message }
            });
        }
    }

    /// <summary>
    /// API endpoint to create customer (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid customer data",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            var response = await _apiService.CreateCustomerAsync(request);
            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer via AJAX");
            return Json(new
            {
                success = false,
                message = "Unable to create customer account",
                errors = new[] { ex.Message }
            });
        }
    }

    /// <summary>
    /// API endpoint to process payment (AJAX)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessPayment([FromBody] ProcessPaymentRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid payment data",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            var response = await _apiService.ProcessPaymentAsync(request);
            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment via AJAX");
            return Json(new
            {
                success = false,
                message = "Unable to process payment",
                errors = new[] { ex.Message }
            });
        }
    }

    /// <summary>
    /// API endpoint to get queue status (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetQueueStatus()
    {
        try
        {
            var response = await _apiService.GetQueueStatusAsync();
            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching queue status via AJAX");
            return Json(new
            {
                success = false,
                message = "Unable to fetch queue status",
                errors = new[] { ex.Message }
            });
        }
    }

    /// <summary>
    /// API endpoint to get customer queue position (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCustomerQueuePosition(int customerId)
    {
        try
        {
            var response = await _apiService.GetCustomerQueuePositionAsync(customerId);
            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching customer queue position via AJAX");
            return Json(new
            {
                success = false,
                message = "Unable to fetch queue position",
                errors = new[] { ex.Message }
            });
        }
    }
}