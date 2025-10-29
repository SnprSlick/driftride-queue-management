using Microsoft.AspNetCore.Mvc;
using DriftRide.Web.Models;
using DriftRide.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace DriftRide.Web.Controllers;

/// <summary>
/// Controller for sales staff operations including payment verification, queue management, and customer lookup.
/// Implements role-based access control and optimizations for 30-second confirmation target.
/// </summary>
public class SalesController : Controller
{
    private readonly ILogger<SalesController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDriftRideApiService _apiService;

    public SalesController(
        ILogger<SalesController> logger,
        IConfiguration configuration,
        IDriftRideApiService apiService)
    {
        _logger = logger;
        _configuration = configuration;
        _apiService = apiService;
    }

    /// <summary>
    /// Sales login page - entry point for sales staff authentication
    /// </summary>
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (IsAuthenticated())
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var viewModel = new SalesLoginViewModel
        {
            ReturnUrl = returnUrl ?? Url.Action(nameof(Dashboard))
        };

        return View(viewModel);
    }

    /// <summary>
    /// Process sales staff login authentication
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(SalesLoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var loginRequest = new SalesLoginRequest
            {
                Username = model.Username,
                Password = model.Password
            };

            var response = await _apiService.LoginAsync(loginRequest);

            if (response.Success && response.Data != null)
            {
                // Store authentication token in session for security
                HttpContext.Session.SetString("AuthToken", response.Data.AccessToken);
                HttpContext.Session.SetString("Username", response.Data.User.Username);
                HttpContext.Session.SetString("DisplayName", response.Data.User.DisplayName);
                HttpContext.Session.SetString("Role", response.Data.User.Role);
                HttpContext.Session.SetString("TokenExpiry", response.Data.ExpiresAt.ToString("O"));

                _logger.LogInformation("Sales user {Username} logged in successfully", model.Username);

                // Redirect to intended page or dashboard
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                return RedirectToAction(nameof(Dashboard));
            }

            ModelState.AddModelError(string.Empty, "Invalid username or password");
            model.Password = string.Empty; // Clear password for security
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sales staff login for user {Username}", model.Username);
            ModelState.AddModelError(string.Empty, "Authentication service is currently unavailable. Please try again.");
            model.Password = string.Empty;
        }

        return View(model);
    }

    /// <summary>
    /// Sales staff logout
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        _logger.LogInformation("Sales user logged out");
        return RedirectToAction(nameof(Login));
    }

    /// <summary>
    /// Main sales dashboard - payment verification and queue management interface
    /// Optimized for 30-second confirmation target with keyboard shortcuts and real-time updates
    /// </summary>
    public async Task<IActionResult> Dashboard()
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction(nameof(Login));
        }

        var viewModel = new SalesDashboardViewModel
        {
            ApiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7000",
            SignalRHubUrl = _configuration["SignalR:HubUrl"] ?? "https://localhost:7000/queuehub",
            Username = HttpContext.Session.GetString("Username") ?? "",
            DisplayName = HttpContext.Session.GetString("DisplayName") ?? "",
            Role = HttpContext.Session.GetString("Role") ?? ""
        };

        try
        {
            // Load initial pending payments
            var authToken = HttpContext.Session.GetString("AuthToken") ?? "";
            var pendingPayments = await _apiService.GetPendingPaymentsAsync(authToken);

            if (pendingPayments.Success && pendingPayments.Data != null)
            {
                viewModel.PendingPayments = pendingPayments.Data;
                viewModel.TotalPendingCount = pendingPayments.Data.Count;

                // Calculate statistics for dashboard
                viewModel.PaymentsOver5Minutes = pendingPayments.Data.Count(p => p.MinutesWaiting > 5);
                viewModel.PaymentsOver10Minutes = pendingPayments.Data.Count(p => p.MinutesWaiting > 10);
            }

            // Load current queue status
            var queueStatus = await _apiService.GetQueueStatusAsync();
            if (queueStatus.Success && queueStatus.Data != null)
            {
                viewModel.QueueEntries = queueStatus.Data.QueueEntries;
                viewModel.TotalInQueue = queueStatus.Data.TotalInQueue;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sales dashboard data");
            viewModel.ErrorMessages.Add("Unable to load dashboard data. Please refresh the page.");
        }

        return View(viewModel);
    }

    /// <summary>
    /// AJAX endpoint to get pending payments with real-time updates
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPendingPayments()
    {
        if (!IsAuthenticated())
        {
            return Json(new { success = false, message = "Authentication required" });
        }

        try
        {
            var authToken = HttpContext.Session.GetString("AuthToken") ?? "";
            var response = await _apiService.GetPendingPaymentsAsync(authToken);

            if (response.Success)
            {
                return Json(new
                {
                    success = true,
                    data = response.Data,
                    count = response.Data?.Count ?? 0,
                    timestamp = DateTime.UtcNow
                });
            }

            return Json(new { success = false, message = response.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending payments via AJAX");
            return Json(new { success = false, message = "Unable to fetch pending payments" });
        }
    }

    /// <summary>
    /// AJAX endpoint to confirm or deny a payment
    /// Optimized for fast processing to meet 30-second target
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment([FromBody] PaymentConfirmationModel model)
    {
        if (!IsAuthenticated())
        {
            return Json(new { success = false, message = "Authentication required" });
        }

        if (!ModelState.IsValid)
        {
            return Json(new
            {
                success = false,
                message = "Invalid payment confirmation data",
                errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            });
        }

        try
        {
            var authToken = HttpContext.Session.GetString("AuthToken") ?? "";
            var request = new PaymentConfirmationRequest
            {
                Confirmed = model.Confirmed,
                Notes = model.Notes ?? ""
            };

            var response = await _apiService.ConfirmPaymentAsync(model.PaymentId, request, authToken);

            if (response.Success)
            {
                _logger.LogInformation("Payment {PaymentId} {Status} by user {Username}",
                    model.PaymentId, model.Confirmed ? "confirmed" : "denied",
                    HttpContext.Session.GetString("Username"));

                return Json(new
                {
                    success = true,
                    message = $"Payment {(model.Confirmed ? "confirmed" : "denied")} successfully",
                    data = response.Data,
                    timestamp = DateTime.UtcNow
                });
            }

            return Json(new { success = false, message = response.Message, errors = response.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming payment {PaymentId}", model.PaymentId);
            return Json(new { success = false, message = "Unable to process payment confirmation" });
        }
    }

    /// <summary>
    /// AJAX endpoint to manually add customer when payment methods fail
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomerManually([FromBody] ManualCustomerModel model)
    {
        if (!IsAuthenticated())
        {
            return Json(new { success = false, message = "Authentication required" });
        }

        if (!ModelState.IsValid)
        {
            return Json(new
            {
                success = false,
                message = "Invalid customer data",
                errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            });
        }

        try
        {
            var authToken = HttpContext.Session.GetString("AuthToken") ?? "";
            var request = new ManualCustomerRequest
            {
                Name = model.Name,
                PhoneNumber = model.PhoneNumber,
                Reason = model.Reason
            };

            var response = await _apiService.AddCustomerManuallyAsync(request, authToken);

            if (response.Success)
            {
                _logger.LogInformation("Customer {Name} manually added by user {Username} - Reason: {Reason}",
                    model.Name, HttpContext.Session.GetString("Username"), model.Reason);

                return Json(new
                {
                    success = true,
                    message = "Customer added successfully",
                    data = response.Data,
                    timestamp = DateTime.UtcNow
                });
            }

            return Json(new { success = false, message = response.Message, errors = response.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error manually adding customer {Name}", model.Name);
            return Json(new { success = false, message = "Unable to add customer manually" });
        }
    }

    /// <summary>
    /// AJAX endpoint to search customers by name for lookup
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchCustomers(string searchTerm)
    {
        if (!IsAuthenticated())
        {
            return Json(new { success = false, message = "Authentication required" });
        }

        if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
        {
            return Json(new { success = false, message = "Search term must be at least 2 characters" });
        }

        try
        {
            var authToken = HttpContext.Session.GetString("AuthToken") ?? "";
            var response = await _apiService.SearchCustomersAsync(searchTerm.Trim(), authToken);

            if (response.Success)
            {
                return Json(new
                {
                    success = true,
                    data = response.Data,
                    count = response.Data?.Count ?? 0,
                    searchTerm = searchTerm
                });
            }

            return Json(new { success = false, message = response.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching customers with term {SearchTerm}", searchTerm);
            return Json(new { success = false, message = "Unable to search customers" });
        }
    }

    /// <summary>
    /// AJAX endpoint to get current queue status
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetQueueStatus()
    {
        if (!IsAuthenticated())
        {
            return Json(new { success = false, message = "Authentication required" });
        }

        try
        {
            var response = await _apiService.GetQueueStatusAsync();

            if (response.Success)
            {
                return Json(new
                {
                    success = true,
                    data = response.Data,
                    timestamp = DateTime.UtcNow
                });
            }

            return Json(new { success = false, message = response.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching queue status");
            return Json(new { success = false, message = "Unable to fetch queue status" });
        }
    }

    /// <summary>
    /// Check if current user is authenticated with valid session
    /// </summary>
    private bool IsAuthenticated()
    {
        var authToken = HttpContext.Session.GetString("AuthToken");
        var expiryString = HttpContext.Session.GetString("TokenExpiry");
        var role = HttpContext.Session.GetString("Role");

        if (string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(expiryString) || role != "Sales")
        {
            return false;
        }

        if (DateTime.TryParse(expiryString, out DateTime expiry) && expiry <= DateTime.UtcNow)
        {
            HttpContext.Session.Clear(); // Clear expired session
            return false;
        }

        return true;
    }
}

/// <summary>
/// View model for sales login page
/// </summary>
public class SalesLoginViewModel
{
    [Required(ErrorMessage = "Username is required")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

/// <summary>
/// AJAX model for payment confirmation
/// </summary>
public class PaymentConfirmationModel
{
    [Required]
    public int PaymentId { get; set; }

    [Required]
    public bool Confirmed { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// AJAX model for manual customer addition
/// </summary>
public class ManualCustomerModel
{
    [Required(ErrorMessage = "Customer name is required")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required")]
    [Phone(ErrorMessage = "Please enter a valid phone number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Reason is required")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string Reason { get; set; } = string.Empty;
}