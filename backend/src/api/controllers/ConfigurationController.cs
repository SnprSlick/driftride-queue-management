using DriftRide.Models;
using DriftRide.Services;
using Microsoft.AspNetCore.Mvc;

namespace DriftRide.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : BaseApiController
{
    private readonly IConfigurationService _configurationService;

    public ConfigurationController(IConfigurationService configurationService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    [HttpGet("payment-methods")]
    [DriftRideAuthorize("Sales")]
    public async Task<IActionResult> GetPaymentMethods(
        [FromQuery] bool includeCredentials = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var configurations = await _configurationService.GetPaymentMethodsAsync(includeCredentials, cancellationToken);
            return Success(configurations, "Payment method configurations retrieved successfully");
        });
    }

    [HttpGet("payment-methods/{paymentMethod}")]
    [DriftRideAuthorize("Sales")]
    public async Task<IActionResult> GetPaymentMethod(
        PaymentMethod paymentMethod,
        [FromQuery] bool includeCredentials = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var configuration = await _configurationService.GetPaymentMethodAsync(paymentMethod, includeCredentials, cancellationToken);

            if (configuration == null)
            {
                return NotFoundError($"Payment method configuration for {paymentMethod} not found");
            }

            return Success(configuration, $"Payment method configuration for {paymentMethod} retrieved successfully");
        });
    }

    [HttpPut("payment-methods")]
    [DriftRideAuthorize("Sales")]
    public async Task<IActionResult> UpdatePaymentMethod(
        [FromBody] UpdatePaymentMethodConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        return await ExecuteAsync(async () =>
        {
            var updatedConfiguration = await _configurationService.UpdatePaymentMethodAsync(
                request.PaymentMethod,
                request.DisplayName,
                request.PaymentUrl ?? string.Empty,
                request.IsEnabled,
                request.PricePerRide,
                request.ApiIntegrationEnabled,
                request.ApiCredentials,
                CurrentUsername,
                cancellationToken);

            return Success(updatedConfiguration, $"Payment method configuration for {request.PaymentMethod} updated successfully");
        });
    }

    [HttpPost("payment-methods/{paymentMethod}/test-api")]
    [DriftRideAuthorize("Sales")]
    public async Task<IActionResult> TestApiIntegration(
        PaymentMethod paymentMethod,
        [FromBody] TestApiIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        return await ExecuteAsync(async () =>
        {
            var testResult = await _configurationService.TestApiIntegrationAsync(
                paymentMethod,
                request.ApiCredentials,
                cancellationToken);

            return Success(testResult, $"API integration test for {paymentMethod} completed");
        });
    }

    [HttpGet("payment-methods/{paymentMethod}/history")]
    [DriftRideAuthorize("Sales")]
    public async Task<IActionResult> GetConfigurationHistory(
        PaymentMethod paymentMethod,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var history = await _configurationService.GetConfigurationHistoryAsync(paymentMethod, cancellationToken);
            return Success(history, $"Configuration history for {paymentMethod} retrieved successfully");
        });
    }

    [HttpPost("payment-methods/validate")]
    [DriftRideAuthorize("Sales")]
    public async Task<IActionResult> ValidateConfiguration(
        [FromBody] ValidatePaymentMethodConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        return await ExecuteAsync(async () =>
        {
            var validationResult = await _configurationService.ValidatePaymentMethodConfigurationAsync(
                request.PaymentMethod,
                request.DisplayName,
                request.PaymentUrl ?? string.Empty,
                request.PricePerRide,
                cancellationToken);

            return Success(validationResult, "Configuration validation completed");
        });
    }

    [HttpGet("payment-methods/enabled")]
    public async Task<IActionResult> GetEnabledPaymentMethods(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var enabledMethods = await _configurationService.GetEnabledPaymentMethodsForCustomersAsync(cancellationToken);
            return Success(enabledMethods, "Enabled payment methods retrieved successfully");
        });
    }
}

public class UpdatePaymentMethodConfigurationRequest
{
    public PaymentMethod PaymentMethod { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? PaymentUrl { get; set; }
    public bool IsEnabled { get; set; }
    public decimal PricePerRide { get; set; }
    public bool ApiIntegrationEnabled { get; set; }
    public string? ApiCredentials { get; set; }
}

public class TestApiIntegrationRequest
{
    public string ApiCredentials { get; set; } = string.Empty;
}

public class ValidatePaymentMethodConfigurationRequest
{
    public PaymentMethod PaymentMethod { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? PaymentUrl { get; set; }
    public decimal PricePerRide { get; set; }
}