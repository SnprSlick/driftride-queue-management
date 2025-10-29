using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DriftRide.Models;
using DriftRide.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DriftRide.Integration.Tests;

/// <summary>
/// Integration tests for payment configuration workflow.
/// Tests the complete sales payment configuration experience from UI to database persistence.
/// </summary>
public class PaymentConfigurationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public PaymentConfigurationIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Integration test for complete payment configuration workflow.
    /// Tests: Sales staff configures payment method → Changes persist → Customer sees updated options.
    /// </summary>
    [Fact]
    public async Task PaymentConfigurationWorkflow_CompleteFlow_SuccessfullyUpdatesPersistsAndReflects()
    {
        // Arrange - Setup test environment
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DriftRideDbContext>();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

        // Clear existing data and seed initial configuration
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        // Setup sales staff authentication
        var salesToken = GenerateTestToken("testsales", "Test Sales", UserRole.Sales);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", salesToken);

        _output.WriteLine("Setup complete: Clean database with seeded payment configurations");

        // Step 1: Sales staff views current payment configuration
        var initialGetResponse = await _client.GetAsync("/api/configuration/payment-methods");
        initialGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initialConfig = await GetPaymentConfigFromResponse(initialGetResponse);
        initialConfig.Should().NotBeEmpty();

        var cashAppConfig = initialConfig.First(c => c.PaymentMethod == PaymentMethod.CashApp);
        _output.WriteLine($"✓ Step 1: Initial CashApp config - Price: ${cashAppConfig.PricePerRide}, Enabled: {cashAppConfig.IsEnabled}");

        // Step 2: Sales staff updates CashApp payment configuration
        var updateRequest = new PaymentConfigurationUpdateRequest
        {
            PaymentMethod = PaymentMethod.CashApp,
            DisplayName = "CashApp Express Payment",
            PaymentUrl = "https://cash.app/pay/updated-drift-rides",
            IsEnabled = true,
            PricePerRide = 35.00m, // Price increase
            ApiIntegrationEnabled = true,
            ApiCredentials = "cashapp-api-key-12345"
        };

        var updateResponse = await _client.PutAsJsonAsync("/api/configuration/payment-methods", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedConfigResponse = await GetPaymentConfigFromResponse(updateResponse);
        updatedConfigResponse.PaymentMethod.Should().Be(PaymentMethod.CashApp);
        updatedConfigResponse.DisplayName.Should().Be("CashApp Express Payment");
        updatedConfigResponse.PricePerRide.Should().Be(35.00m);
        updatedConfigResponse.ApiIntegrationEnabled.Should().BeTrue();

        _output.WriteLine($"✓ Step 2: Updated CashApp config - Price: ${updatedConfigResponse.PricePerRide}, API: {updatedConfigResponse.ApiIntegrationEnabled}");

        // Step 3: Verify configuration persisted to database
        var persistenceVerifyResponse = await _client.GetAsync("/api/configuration/payment-methods");
        persistenceVerifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var persistedConfigs = await GetPaymentConfigFromResponse(persistenceVerifyResponse);
        var persistedCashApp = persistedConfigs.First(c => c.PaymentMethod == PaymentMethod.CashApp);

        persistedCashApp.DisplayName.Should().Be("CashApp Express Payment");
        persistedCashApp.PaymentUrl.Should().Be("https://cash.app/pay/updated-drift-rides");
        persistedCashApp.PricePerRide.Should().Be(35.00m);
        persistedCashApp.ApiIntegrationEnabled.Should().BeTrue();
        persistedCashApp.ApiCredentials.Should().BeNullOrEmpty(); // Security: credentials not returned

        _output.WriteLine($"✓ Step 3: Configuration persisted correctly to database");

        // Step 4: Test customer interface reflects updated pricing
        // Switch to customer context (no authentication required for viewing payment methods)
        _client.DefaultRequestHeaders.Authorization = null;

        var customerViewResponse = await _client.GetAsync("/api/configuration/payment-methods");
        customerViewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var customerConfigs = await GetPaymentConfigFromResponse(customerViewResponse);
        var customerCashApp = customerConfigs.First(c => c.PaymentMethod == PaymentMethod.CashApp);

        customerCashApp.DisplayName.Should().Be("CashApp Express Payment");
        customerCashApp.PricePerRide.Should().Be(35.00m);
        customerCashApp.IsEnabled.Should().BeTrue();

        _output.WriteLine($"✓ Step 4: Customer sees updated pricing: ${customerCashApp.PricePerRide}");

        // Step 5: Test disabling a payment method
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", salesToken);

        var disableRequest = new PaymentConfigurationUpdateRequest
        {
            PaymentMethod = PaymentMethod.PayPal,
            DisplayName = "PayPal (Temporarily Disabled)",
            PaymentUrl = "https://paypal.com/disabled",
            IsEnabled = false, // Disable PayPal
            PricePerRide = 25.00m,
            ApiIntegrationEnabled = false
        };

        var disableResponse = await _client.PutAsJsonAsync("/api/configuration/payment-methods", disableRequest);
        disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        _output.WriteLine($"✓ Step 5: PayPal payment method disabled");

        // Step 6: Verify disabled payment method behavior
        _client.DefaultRequestHeaders.Authorization = null;

        var disabledCheckResponse = await _client.GetAsync("/api/configuration/payment-methods");
        var disabledConfigs = await GetPaymentConfigFromResponse(disabledCheckResponse);
        var disabledPayPal = disabledConfigs.First(c => c.PaymentMethod == PaymentMethod.PayPal);

        disabledPayPal.IsEnabled.Should().BeFalse();

        _output.WriteLine($"✓ Step 6: Customer interface shows PayPal as disabled");
    }

    /// <summary>
    /// Integration test for API integration toggle workflow.
    /// Tests: Manual verification → API integration → Back to manual verification.
    /// </summary>
    [Fact]
    public async Task ApiIntegrationToggle_CompleteFlow_SuccessfullyTogglesVerificationMethods()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DriftRideDbContext>();

        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        var salesToken = GenerateTestToken("testsales", "Test Sales", UserRole.Sales);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", salesToken);

        _output.WriteLine("Testing API integration toggle workflow");

        // Step 1: Enable API integration for CashApp
        var enableApiRequest = new PaymentConfigurationUpdateRequest
        {
            PaymentMethod = PaymentMethod.CashApp,
            DisplayName = "CashApp with API Integration",
            PaymentUrl = "https://cash.app/api/payments",
            IsEnabled = true,
            PricePerRide = 25.00m,
            ApiIntegrationEnabled = true,
            ApiCredentials = "cashapp-production-api-key"
        };

        var enableResponse = await _client.PutAsJsonAsync("/api/configuration/payment-methods", enableApiRequest);
        enableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var enabledConfig = await GetPaymentConfigFromResponse(enableResponse);
        enabledConfig.ApiIntegrationEnabled.Should().BeTrue();

        _output.WriteLine($"✓ Step 1: API integration enabled for CashApp");

        // Step 2: Verify API credentials are stored but not returned
        var verifyResponse = await _client.GetAsync("/api/configuration/payment-methods");
        var configs = await GetPaymentConfigFromResponse(verifyResponse);
        var cashAppWithApi = configs.First(c => c.PaymentMethod == PaymentMethod.CashApp);

        cashAppWithApi.ApiIntegrationEnabled.Should().BeTrue();
        cashAppWithApi.ApiCredentials.Should().BeNullOrEmpty(); // Security check

        _output.WriteLine($"✓ Step 2: API credentials secured (not returned in response)");

        // Step 3: Disable API integration (back to manual)
        var disableApiRequest = new PaymentConfigurationUpdateRequest
        {
            PaymentMethod = PaymentMethod.CashApp,
            DisplayName = "CashApp Manual Verification",
            PaymentUrl = "https://cash.app/manual",
            IsEnabled = true,
            PricePerRide = 25.00m,
            ApiIntegrationEnabled = false,
            ApiCredentials = "" // Clear credentials
        };

        var disableResponse = await _client.PutAsJsonAsync("/api/configuration/payment-methods", disableApiRequest);
        disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var disabledConfig = await GetPaymentConfigFromResponse(disableResponse);
        disabledConfig.ApiIntegrationEnabled.Should().BeFalse();

        _output.WriteLine($"✓ Step 3: API integration disabled, back to manual verification");
    }

    /// <summary>
    /// Integration test for multiple payment method configuration.
    /// Tests: Configure all payment methods with different settings simultaneously.
    /// </summary>
    [Fact]
    public async Task MultiplePaymentMethodConfiguration_DifferentSettings_AllPersistCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DriftRideDbContext>();

        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        var salesToken = GenerateTestToken("testsales", "Test Sales", UserRole.Sales);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", salesToken);

        _output.WriteLine("Testing multiple payment method configuration");

        // Configure CashApp with API integration
        var cashAppRequest = new PaymentConfigurationUpdateRequest
        {
            PaymentMethod = PaymentMethod.CashApp,
            DisplayName = "CashApp (Premium)",
            PaymentUrl = "https://cash.app/premium",
            IsEnabled = true,
            PricePerRide = 30.00m,
            ApiIntegrationEnabled = true,
            ApiCredentials = "cashapp-api-key"
        };

        // Configure PayPal with manual verification
        var payPalRequest = new PaymentConfigurationUpdateRequest
        {
            PaymentMethod = PaymentMethod.PayPal,
            DisplayName = "PayPal (Standard)",
            PaymentUrl = "https://paypal.com/standard",
            IsEnabled = true,
            PricePerRide = 25.00m,
            ApiIntegrationEnabled = false
        };

        // Configure Cash-in-Hand as disabled
        var cashRequest = new PaymentConfigurationUpdateRequest
        {
            PaymentMethod = PaymentMethod.CashInHand,
            DisplayName = "Cash Payment (Disabled)",
            PaymentUrl = "",
            IsEnabled = false,
            PricePerRide = 20.00m,
            ApiIntegrationEnabled = false
        };

        // Act - Update all payment methods
        var cashAppResponse = await _client.PutAsJsonAsync("/api/configuration/payment-methods", cashAppRequest);
        var payPalResponse = await _client.PutAsJsonAsync("/api/configuration/payment-methods", payPalRequest);
        var cashResponse = await _client.PutAsJsonAsync("/api/configuration/payment-methods", cashRequest);

        // Assert - All updates successful
        cashAppResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        payPalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        cashResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify final configuration state
        var finalResponse = await _client.GetAsync("/api/configuration/payment-methods");
        var finalConfigs = await GetPaymentConfigFromResponse(finalResponse);

        finalConfigs.Should().HaveCount(3);

        var finalCashApp = finalConfigs.First(c => c.PaymentMethod == PaymentMethod.CashApp);
        finalCashApp.PricePerRide.Should().Be(30.00m);
        finalCashApp.ApiIntegrationEnabled.Should().BeTrue();
        finalCashApp.IsEnabled.Should().BeTrue();

        var finalPayPal = finalConfigs.First(c => c.PaymentMethod == PaymentMethod.PayPal);
        finalPayPal.PricePerRide.Should().Be(25.00m);
        finalPayPal.ApiIntegrationEnabled.Should().BeFalse();
        finalPayPal.IsEnabled.Should().BeTrue();

        var finalCash = finalConfigs.First(c => c.PaymentMethod == PaymentMethod.CashInHand);
        finalCash.PricePerRide.Should().Be(20.00m);
        finalCash.IsEnabled.Should().BeFalse();

        _output.WriteLine($"✓ All payment methods configured with different settings:");
        _output.WriteLine($"  CashApp: ${finalCashApp.PricePerRide} (API enabled, enabled)");
        _output.WriteLine($"  PayPal: ${finalPayPal.PricePerRide} (Manual verification, enabled)");
        _output.WriteLine($"  Cash: ${finalCash.PricePerRide} (Disabled)");
    }

    /// <summary>
    /// Integration test for validation error handling.
    /// Tests: Invalid configuration requests are properly rejected with detailed errors.
    /// </summary>
    [Fact]
    public async Task PaymentConfigurationValidation_InvalidRequests_ReturnsDetailedErrors()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DriftRideDbContext>();

        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        var salesToken = GenerateTestToken("testsales", "Test Sales", UserRole.Sales);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", salesToken);

        // Test negative price
        var negativePriceRequest = new PaymentConfigurationUpdateRequest
        {
            PaymentMethod = PaymentMethod.CashApp,
            DisplayName = "Invalid Price",
            PaymentUrl = "https://example.com",
            IsEnabled = true,
            PricePerRide = -10.00m // Invalid
        };

        var negativePriceResponse = await _client.PutAsJsonAsync("/api/configuration/payment-methods", negativePriceRequest);
        negativePriceResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Test empty display name
        var emptyNameRequest = new PaymentConfigurationUpdateRequest
        {
            PaymentMethod = PaymentMethod.PayPal,
            DisplayName = "", // Invalid
            PaymentUrl = "https://example.com",
            IsEnabled = true,
            PricePerRide = 25.00m
        };

        var emptyNameResponse = await _client.PutAsJsonAsync("/api/configuration/payment-methods", emptyNameRequest);
        emptyNameResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _output.WriteLine($"✓ Validation correctly rejects invalid configurations");
    }

    /// <summary>
    /// Helper method to extract payment configuration from HTTP response.
    /// </summary>
    private async Task<PaymentConfigurationDto[]> GetPaymentConfigFromResponse(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<PaymentConfigurationDto[]>(content, GetJsonOptions()) ?? Array.Empty<PaymentConfigurationDto>();
    }

    /// <summary>
    /// Helper method to extract single payment configuration from HTTP response.
    /// </summary>
    private async Task<PaymentConfigurationDto> GetPaymentConfigFromResponse(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<PaymentConfigurationDto>(content, GetJsonOptions()) ?? new PaymentConfigurationDto();
    }

    /// <summary>
    /// Generates a test JWT token for authentication.
    /// </summary>
    private string GenerateTestToken(string username, string displayName, UserRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = displayName,
            Role = role,
            IsActive = true
        };

        return jwtService.GenerateToken(user);
    }

    /// <summary>
    /// Gets JSON serialization options for consistent deserialization.
    /// </summary>
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }
}

/// <summary>
/// Request model for payment configuration updates in integration tests.
/// </summary>
public class PaymentConfigurationUpdateRequest
{
    public PaymentMethod PaymentMethod { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string PaymentUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public decimal PricePerRide { get; set; }
    public bool ApiIntegrationEnabled { get; set; } = false;
    public string? ApiCredentials { get; set; }
}

/// <summary>
/// Response model for payment configuration in integration tests.
/// </summary>
public class PaymentConfigurationDto
{
    public int Id { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string PaymentUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public decimal PricePerRide { get; set; }
    public bool ApiIntegrationEnabled { get; set; }
    public string? ApiCredentials { get; set; }
}