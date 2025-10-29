using System.Text;
using System.Text.Json;
using DriftRide.Web.Models;
using Microsoft.Extensions.Caching.Memory;

namespace DriftRide.Web.Services;

/// <summary>
/// Service implementation for communicating with DriftRide API backend
/// </summary>
public class DriftRideApiService : IDriftRideApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DriftRideApiService> _logger;
    private readonly IMemoryCache _cache;
    private readonly JsonSerializerOptions _jsonOptions;

    public DriftRideApiService(
        IHttpClientFactory httpClientFactory,
        ILogger<DriftRideApiService> logger,
        IMemoryCache cache)
    {
        _httpClient = httpClientFactory.CreateClient("DriftRideApi");
        _logger = logger;
        _cache = cache;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Get available payment methods and configuration
    /// </summary>
    public async Task<ApiResponse<List<PaymentMethodOption>>> GetPaymentMethodsAsync()
    {
        try
        {
            // Check cache first
            const string cacheKey = "payment_methods";
            if (_cache.TryGetValue(cacheKey, out ApiResponse<List<PaymentMethodOption>>? cachedResult))
            {
                return cachedResult!;
            }

            _logger.LogInformation("Fetching payment methods from API");

            var response = await _httpClient.GetAsync("/api/configuration/payment-methods");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<PaymentMethodOption>>>(content, _jsonOptions);

                if (apiResponse != null && apiResponse.Success)
                {
                    // Cache for 5 minutes
                    _cache.Set(cacheKey, apiResponse, TimeSpan.FromMinutes(5));
                    return apiResponse;
                }
            }

            _logger.LogWarning("Failed to fetch payment methods, using defaults");
            return GetDefaultPaymentMethods();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching payment methods");
            return GetDefaultPaymentMethods();
        }
    }

    /// <summary>
    /// Create a new customer
    /// </summary>
    public async Task<ApiResponse<CustomerResponse>> CreateCustomerAsync(CreateCustomerRequest request)
    {
        try
        {
            _logger.LogInformation("Creating customer: {Name}", request.Name);

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/customers", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<CustomerResponse>>(responseContent, _jsonOptions);
                if (apiResponse != null)
                {
                    _logger.LogInformation("Customer created successfully: {CustomerId}", apiResponse.Data?.Id);
                    return apiResponse;
                }
            }

            _logger.LogWarning("Failed to create customer: {StatusCode} - {Content}", response.StatusCode, responseContent);
            return new ApiResponse<CustomerResponse>
            {
                Success = false,
                Message = "Failed to create customer account",
                Errors = new List<string> { $"API returned {response.StatusCode}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer");
            return new ApiResponse<CustomerResponse>
            {
                Success = false,
                Message = "Unable to create customer account at this time",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Process a payment for a customer
    /// </summary>
    public async Task<ApiResponse<PaymentResponse>> ProcessPaymentAsync(ProcessPaymentRequest request)
    {
        try
        {
            _logger.LogInformation("Processing payment for customer {CustomerId} - {Method}: ${Amount}",
                request.CustomerId, request.PaymentMethod, request.Amount);

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/payments", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<PaymentResponse>>(responseContent, _jsonOptions);
                if (apiResponse != null)
                {
                    _logger.LogInformation("Payment processed successfully: {PaymentId}", apiResponse.Data?.Id);
                    return apiResponse;
                }
            }

            _logger.LogWarning("Failed to process payment: {StatusCode} - {Content}", response.StatusCode, responseContent);
            return new ApiResponse<PaymentResponse>
            {
                Success = false,
                Message = "Payment processing failed",
                Errors = new List<string> { $"API returned {response.StatusCode}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment");
            return new ApiResponse<PaymentResponse>
            {
                Success = false,
                Message = "Unable to process payment at this time",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Get current queue status
    /// </summary>
    public async Task<ApiResponse<QueueStatusResponse>> GetQueueStatusAsync()
    {
        try
        {
            _logger.LogDebug("Fetching queue status");

            var response = await _httpClient.GetAsync("/api/queue");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<QueueStatusResponse>>(responseContent, _jsonOptions);
                if (apiResponse != null)
                {
                    return apiResponse;
                }
            }

            _logger.LogWarning("Failed to fetch queue status: {StatusCode}", response.StatusCode);
            return new ApiResponse<QueueStatusResponse>
            {
                Success = false,
                Message = "Unable to fetch queue status",
                Errors = new List<string> { $"API returned {response.StatusCode}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching queue status");
            return new ApiResponse<QueueStatusResponse>
            {
                Success = false,
                Message = "Unable to fetch queue status",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Get customer's queue position
    /// </summary>
    public async Task<ApiResponse<CustomerQueuePosition>> GetCustomerQueuePositionAsync(int customerId)
    {
        try
        {
            _logger.LogDebug("Fetching queue position for customer {CustomerId}", customerId);

            var response = await _httpClient.GetAsync($"/api/queue/position/{customerId}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<CustomerQueuePosition>>(responseContent, _jsonOptions);
                if (apiResponse != null)
                {
                    return apiResponse;
                }
            }

            _logger.LogWarning("Failed to fetch customer queue position: {StatusCode}", response.StatusCode);
            return new ApiResponse<CustomerQueuePosition>
            {
                Success = false,
                Message = "Unable to fetch queue position",
                Errors = new List<string> { $"API returned {response.StatusCode}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching customer queue position");
            return new ApiResponse<CustomerQueuePosition>
            {
                Success = false,
                Message = "Unable to fetch queue position",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    // Sales-specific API methods

    /// <summary>
    /// Sales staff login with authentication
    /// </summary>
    public async Task<ApiResponse<SalesLoginResponse>> LoginAsync(SalesLoginRequest request)
    {
        try
        {
            _logger.LogInformation("Authenticating sales user: {Username}", request.Username);

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/auth/login", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<SalesLoginResponse>>(responseContent, _jsonOptions);
                if (apiResponse != null && apiResponse.Success)
                {
                    _logger.LogInformation("Sales user authenticated successfully: {Username}", request.Username);
                    return apiResponse;
                }
            }

            _logger.LogWarning("Sales authentication failed: {StatusCode} - {Content}", response.StatusCode, responseContent);
            return new ApiResponse<SalesLoginResponse>
            {
                Success = false,
                Message = "Authentication failed",
                Errors = new List<string> { "Invalid username or password" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sales authentication");
            return new ApiResponse<SalesLoginResponse>
            {
                Success = false,
                Message = "Authentication service unavailable",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Get pending payments awaiting confirmation (Sales role required)
    /// </summary>
    public async Task<ApiResponse<List<PendingPaymentResponse>>> GetPendingPaymentsAsync(string authToken)
    {
        try
        {
            _logger.LogDebug("Fetching pending payments for sales dashboard");

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

            var response = await _httpClient.GetAsync("/api/payments/pending");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<PendingPaymentResponse>>>(responseContent, _jsonOptions);
                if (apiResponse != null)
                {
                    _logger.LogInformation("Retrieved {Count} pending payments", apiResponse.Data?.Count ?? 0);
                    return apiResponse;
                }
            }

            _logger.LogWarning("Failed to fetch pending payments: {StatusCode}", response.StatusCode);
            return new ApiResponse<List<PendingPaymentResponse>>
            {
                Success = false,
                Message = "Unable to fetch pending payments",
                Errors = new List<string> { $"API returned {response.StatusCode}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending payments");
            return new ApiResponse<List<PendingPaymentResponse>>
            {
                Success = false,
                Message = "Unable to fetch pending payments",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Confirm or deny a payment (Sales role required)
    /// </summary>
    public async Task<ApiResponse<PaymentResponse>> ConfirmPaymentAsync(int paymentId, PaymentConfirmationRequest request, string authToken)
    {
        try
        {
            _logger.LogInformation("Confirming payment {PaymentId}: {Status}", paymentId, request.Confirmed ? "Approved" : "Denied");

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/api/payments/{paymentId}/confirm", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<PaymentResponse>>(responseContent, _jsonOptions);
                if (apiResponse != null)
                {
                    _logger.LogInformation("Payment {PaymentId} {Status} successfully", paymentId, request.Confirmed ? "confirmed" : "denied");
                    return apiResponse;
                }
            }

            _logger.LogWarning("Failed to confirm payment {PaymentId}: {StatusCode} - {Content}", paymentId, response.StatusCode, responseContent);
            return new ApiResponse<PaymentResponse>
            {
                Success = false,
                Message = "Payment confirmation failed",
                Errors = new List<string> { $"API returned {response.StatusCode}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming payment {PaymentId}", paymentId);
            return new ApiResponse<PaymentResponse>
            {
                Success = false,
                Message = "Unable to process payment confirmation",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Manually add customer when payment fails (Sales role required)
    /// </summary>
    public async Task<ApiResponse<CustomerResponse>> AddCustomerManuallyAsync(ManualCustomerRequest request, string authToken)
    {
        try
        {
            _logger.LogInformation("Manually adding customer: {Name} - Reason: {Reason}", request.Name, request.Reason);

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/customers/manual", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<CustomerResponse>>(responseContent, _jsonOptions);
                if (apiResponse != null)
                {
                    _logger.LogInformation("Customer manually added successfully: {CustomerId}", apiResponse.Data?.Id);
                    return apiResponse;
                }
            }

            _logger.LogWarning("Failed to manually add customer: {StatusCode} - {Content}", response.StatusCode, responseContent);
            return new ApiResponse<CustomerResponse>
            {
                Success = false,
                Message = "Failed to add customer manually",
                Errors = new List<string> { $"API returned {response.StatusCode}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error manually adding customer");
            return new ApiResponse<CustomerResponse>
            {
                Success = false,
                Message = "Unable to add customer manually",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Search customers by name for lookup (Sales role required)
    /// </summary>
    public async Task<ApiResponse<List<CustomerResponse>>> SearchCustomersAsync(string searchTerm, string authToken)
    {
        try
        {
            _logger.LogDebug("Searching customers with term: {SearchTerm}", searchTerm);

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

            var encodedSearchTerm = Uri.EscapeDataString(searchTerm);
            var response = await _httpClient.GetAsync($"/api/customers/search?q={encodedSearchTerm}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<CustomerResponse>>>(responseContent, _jsonOptions);
                if (apiResponse != null)
                {
                    _logger.LogInformation("Found {Count} customers matching search term", apiResponse.Data?.Count ?? 0);
                    return apiResponse;
                }
            }

            _logger.LogWarning("Failed to search customers: {StatusCode}", response.StatusCode);
            return new ApiResponse<List<CustomerResponse>>
            {
                Success = false,
                Message = "Unable to search customers",
                Errors = new List<string> { $"API returned {response.StatusCode}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching customers");
            return new ApiResponse<List<CustomerResponse>>
            {
                Success = false,
                Message = "Unable to search customers",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Get payment configuration for management (Sales role required)
    /// </summary>
    public async Task<ApiResponse<List<PaymentConfigurationModel>>> GetPaymentConfigurationAsync(string authToken)
    {
        try
        {
            _logger.LogInformation("Fetching payment configuration for management");

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

            var response = await _httpClient.GetAsync("/api/configuration/payment-methods/management");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<PaymentConfigurationModel>>>(content, _jsonOptions);

                if (apiResponse != null && apiResponse.Success)
                {
                    _logger.LogInformation("Retrieved {Count} payment configurations", apiResponse.Data?.Count ?? 0);
                    return apiResponse;
                }
            }

            _logger.LogWarning("Failed to fetch payment configuration: {StatusCode}", response.StatusCode);
            return new ApiResponse<List<PaymentConfigurationModel>>
            {
                Success = false,
                Message = "Failed to retrieve payment configuration",
                Errors = new List<string> { $"API returned {response.StatusCode}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching payment configuration");
            return new ApiResponse<List<PaymentConfigurationModel>>
            {
                Success = false,
                Message = "Unable to retrieve payment configuration",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Update payment configuration (Sales role required)
    /// </summary>
    public async Task<ApiResponse<PaymentConfigurationModel>> UpdatePaymentConfigurationAsync(UpdatePaymentConfigurationRequest request, string authToken)
    {
        try
        {
            _logger.LogInformation("Updating payment configuration: {PaymentMethod}", request.DisplayName);

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"/api/configuration/payment-methods/{request.Id}", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<PaymentConfigurationModel>>(responseContent, _jsonOptions);
                if (apiResponse != null)
                {
                    _logger.LogInformation("Payment configuration updated successfully: {Id}", apiResponse.Data?.Id);

                    // Invalidate cache when configuration changes
                    InvalidatePaymentMethodsCache();

                    return apiResponse;
                }
            }

            _logger.LogWarning("Failed to update payment configuration: {StatusCode} - {Content}", response.StatusCode, responseContent);
            return new ApiResponse<PaymentConfigurationModel>
            {
                Success = false,
                Message = "Failed to update payment configuration",
                Errors = new List<string> { $"API returned {response.StatusCode}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment configuration");
            return new ApiResponse<PaymentConfigurationModel>
            {
                Success = false,
                Message = "Unable to update payment configuration",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Invalidate cached payment methods to force refresh
    /// </summary>
    public void InvalidatePaymentMethodsCache()
    {
        const string cacheKey = "payment_methods";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Payment methods cache invalidated");
    }

    /// <summary>
    /// Get default payment methods when API is unavailable
    /// </summary>
    private static ApiResponse<List<PaymentMethodOption>> GetDefaultPaymentMethods()
    {
        return new ApiResponse<List<PaymentMethodOption>>
        {
            Success = true,
            Message = "Using default payment methods",
            Data = new List<PaymentMethodOption>
            {
                new PaymentMethodOption
                {
                    Method = "CashApp",
                    DisplayName = "CashApp",
                    PaymentUrl = "https://cash.app/",
                    IsEnabled = true,
                    RequiresExternalApp = true,
                    PricePerRide = 25.00m
                },
                new PaymentMethodOption
                {
                    Method = "PayPal",
                    DisplayName = "PayPal",
                    PaymentUrl = "https://paypal.me/",
                    IsEnabled = true,
                    RequiresExternalApp = true,
                    PricePerRide = 25.00m
                },
                new PaymentMethodOption
                {
                    Method = "Cash",
                    DisplayName = "Cash in Hand",
                    PaymentUrl = "",
                    IsEnabled = true,
                    RequiresExternalApp = false,
                    PricePerRide = 25.00m
                }
            }
        };
    }
}