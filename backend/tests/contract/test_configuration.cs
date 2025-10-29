using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DriftRide.Api.Models;

namespace DriftRide.Contract.Tests;

/// <summary>
/// Contract tests for Configuration endpoints
/// - GET /api/configuration/payment-methods - Get payment configuration
/// These tests validate request/response schemas, HTTP status codes, authentication, and business rules
/// Tests are designed to FAIL initially until controllers are implemented (TDD approach)
/// </summary>
public class ConfigurationContractTests : ContractTestBase
{
    public ConfigurationContractTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    #region GET /api/configuration/payment-methods Tests

    [Fact]
    public async Task GetConfigurationPaymentMethods_ValidRequest_ReturnsPaymentMethods()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        // Act
        var response = await Client.GetAsync("/api/configuration/payment-methods");

        // Assert
        var paymentMethods = await ValidateSuccessResponse<PaymentConfiguration[]>(response);

        Assert.NotEmpty(paymentMethods);
        Assert.Equal(3, paymentMethods.Length); // Based on seeded data

        // Validate each payment method configuration
        Assert.All(paymentMethods, config =>
        {
            Assert.True(config.Id > 0);
            Assert.True(Enum.IsDefined(typeof(PaymentMethod), config.PaymentMethod));
            Assert.NotNull(config.DisplayName);
            Assert.NotEmpty(config.DisplayName);
            Assert.True(config.PricePerRide > 0);
            Assert.True(config.IsEnabled); // All seeded configs are enabled
        });

        // Verify specific payment methods exist
        Assert.Contains(paymentMethods, c => c.PaymentMethod == PaymentMethod.CashApp);
        Assert.Contains(paymentMethods, c => c.PaymentMethod == PaymentMethod.PayPal);
        Assert.Contains(paymentMethods, c => c.PaymentMethod == PaymentMethod.CashInHand);
    }

    [Fact]
    public async Task GetConfigurationPaymentMethods_ValidatesResponseSchema()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        // Act
        var response = await Client.GetAsync("/api/configuration/payment-methods");

        // Assert - Validate complete response schema
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = FromJson<ApiResponse<PaymentConfiguration[]>>(content);

        // Validate ApiResponse schema
        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Message);
        Assert.NotNull(apiResponse.Data);
        Assert.Null(apiResponse.Error);

        // Validate PaymentConfiguration schema for each item
        Assert.All(apiResponse.Data, config =>
        {
            Assert.True(config.Id > 0);
            Assert.True(Enum.IsDefined(typeof(PaymentMethod), config.PaymentMethod));
            Assert.NotNull(config.DisplayName);
            Assert.NotEmpty(config.DisplayName);
            Assert.True(config.PricePerRide >= 0);

            // PaymentUrl can be null for cash payments
            if (config.PaymentMethod != PaymentMethod.CashInHand)
            {
                Assert.NotNull(config.PaymentUrl);
                Assert.True(Uri.TryCreate(config.PaymentUrl, UriKind.Absolute, out _));
            }

            // API integration fields
            Assert.NotNull(config.ApiIntegrationEnabled);
            if (config.ApiIntegrationEnabled.Value)
            {
                Assert.NotNull(config.ApiCredentials);
            }
        });
    }

    [Fact]
    public async Task GetConfigurationPaymentMethods_OnlyEnabledMethods_ReturnsFilteredList()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        // Disable one payment method via database
        using var context = GetDbContext();
        var paypalConfig = context.PaymentConfigurations.First(c => c.PaymentMethod == PaymentMethod.PayPal);
        paypalConfig.IsEnabled = false;
        await context.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync("/api/configuration/payment-methods");

        // Assert
        var paymentMethods = await ValidateSuccessResponse<PaymentConfiguration[]>(response);

        Assert.Equal(2, paymentMethods.Length); // One should be filtered out
        Assert.All(paymentMethods, config => Assert.True(config.IsEnabled));
        Assert.DoesNotContain(paymentMethods, c => c.PaymentMethod == PaymentMethod.PayPal);
    }

    [Fact]
    public async Task GetConfigurationPaymentMethods_NoEnabledMethods_ReturnsEmptyArray()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        // Disable all payment methods
        using var context = GetDbContext();
        var allConfigs = context.PaymentConfigurations.ToList();
        foreach (var config in allConfigs)
        {
            config.IsEnabled = false;
        }
        await context.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync("/api/configuration/payment-methods");

        // Assert
        var paymentMethods = await ValidateSuccessResponse<PaymentConfiguration[]>(response);
        Assert.Empty(paymentMethods);
    }

    [Fact]
    public async Task GetConfigurationPaymentMethods_DriverRole_CanAccess()
    {
        // Arrange - Drivers should be able to view payment methods for customer interface
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Driver");

        // Act
        var response = await Client.GetAsync("/api/configuration/payment-methods");

        // Assert
        var paymentMethods = await ValidateSuccessResponse<PaymentConfiguration[]>(response);
        Assert.NotEmpty(paymentMethods);
    }

    [Fact]
    public async Task GetConfigurationPaymentMethods_SalesRole_CanAccess()
    {
        // Arrange - Sales should be able to view payment methods
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        // Act
        var response = await Client.GetAsync("/api/configuration/payment-methods");

        // Assert
        var paymentMethods = await ValidateSuccessResponse<PaymentConfiguration[]>(response);
        Assert.NotEmpty(paymentMethods);
    }

    [Fact]
    public async Task GetConfigurationPaymentMethods_NoAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await SeedTestDataAsync();
        ClearAuthorizationHeader();

        // Act
        var response = await Client.GetAsync("/api/configuration/payment-methods");

        // Assert
        var error = await ValidateErrorResponse(response, 401, "UNAUTHORIZED");
        Assert.Contains("authorization", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetConfigurationPaymentMethods_InvalidJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        await SeedTestDataAsync();
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await Client.GetAsync("/api/configuration/payment-methods");

        // Assert
        var error = await ValidateErrorResponse(response, 401, "UNAUTHORIZED");
    }

    [Fact]
    public async Task GetConfigurationPaymentMethods_ExpiredJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        await SeedTestDataAsync();

        // Generate an expired token
        var expiredToken = GenerateExpiredJwtToken();
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await Client.GetAsync("/api/configuration/payment-methods");

        // Assert
        var error = await ValidateErrorResponse(response, 401, "UNAUTHORIZED");
    }

    [Fact]
    public async Task GetConfigurationPaymentMethods_SensitiveDataExcluded_ReturnsFilteredResponse()
    {
        // Arrange - API credentials should not be exposed to clients
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        // Enable API integration on one payment method
        using var context = GetDbContext();
        var cashAppConfig = context.PaymentConfigurations.First(c => c.PaymentMethod == PaymentMethod.CashApp);
        cashAppConfig.ApiIntegrationEnabled = true;
        cashAppConfig.ApiCredentials = "sensitive-api-key-should-not-be-exposed";
        await context.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync("/api/configuration/payment-methods");

        // Assert
        var paymentMethods = await ValidateSuccessResponse<PaymentConfiguration[]>(response);

        var cashAppMethod = paymentMethods.First(c => c.PaymentMethod == PaymentMethod.CashApp);
        Assert.True(cashAppMethod.ApiIntegrationEnabled);

        // API credentials should be null or empty in the response
        Assert.True(string.IsNullOrEmpty(cashAppMethod.ApiCredentials));
    }

    [Fact]
    public async Task GetConfigurationPaymentMethods_SortedByDisplayOrder_ReturnsOrderedList()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        // Act
        var response = await Client.GetAsync("/api/configuration/payment-methods");

        // Assert
        var paymentMethods = await ValidateSuccessResponse<PaymentConfiguration[]>(response);

        // Should be sorted by PaymentMethod enum order or by a display order property
        Assert.NotEmpty(paymentMethods);

        // Verify consistent ordering (implementation detail, but important for UX)
        var previousId = 0;
        foreach (var method in paymentMethods)
        {
            Assert.True(method.Id > previousId, "Payment methods should be consistently ordered");
            previousId = method.Id;
        }
    }

    #endregion

    #region PUT /api/configuration/payment-methods Tests

    [Fact]
    public async Task UpdatePaymentMethodConfiguration_ValidRequest_ReturnsUpdatedConfiguration()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var updateRequest = new UpdatePaymentMethodConfigurationRequest
        {
            PaymentMethod = PaymentMethod.CashApp,
            DisplayName = "Updated CashApp Payment",
            PaymentUrl = "https://cashapp.com/updated-link",
            IsEnabled = true,
            PricePerRide = 30.00m,
            ApiIntegrationEnabled = true,
            ApiCredentials = "new-api-credentials"
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/configuration/payment-methods", updateRequest);

        // Assert
        var updatedConfig = await ValidateSuccessResponse<PaymentConfiguration>(response);

        Assert.Equal(updateRequest.PaymentMethod, updatedConfig.PaymentMethod);
        Assert.Equal(updateRequest.DisplayName, updatedConfig.DisplayName);
        Assert.Equal(updateRequest.PaymentUrl, updatedConfig.PaymentUrl);
        Assert.Equal(updateRequest.IsEnabled, updatedConfig.IsEnabled);
        Assert.Equal(updateRequest.PricePerRide, updatedConfig.PricePerRide);
        Assert.Equal(updateRequest.ApiIntegrationEnabled, updatedConfig.ApiIntegrationEnabled);

        // API credentials should not be returned in response
        Assert.True(string.IsNullOrEmpty(updatedConfig.ApiCredentials));
    }

    [Fact]
    public async Task UpdatePaymentMethodConfiguration_InvalidPaymentMethod_ReturnsBadRequest()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var updateRequest = new UpdatePaymentMethodConfigurationRequest
        {
            PaymentMethod = (PaymentMethod)999, // Invalid enum value
            DisplayName = "Invalid Payment Method",
            PaymentUrl = "https://example.com",
            IsEnabled = true,
            PricePerRide = 25.00m
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/configuration/payment-methods", updateRequest);

        // Assert
        var error = await ValidateErrorResponse(response, 400, "VALIDATION_ERROR");
        Assert.Contains("PaymentMethod", error.Details);
    }

    [Fact]
    public async Task UpdatePaymentMethodConfiguration_NegativePrice_ReturnsBadRequest()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var updateRequest = new UpdatePaymentMethodConfigurationRequest
        {
            PaymentMethod = PaymentMethod.CashApp,
            DisplayName = "CashApp Payment",
            PaymentUrl = "https://cashapp.com/link",
            IsEnabled = true,
            PricePerRide = -5.00m // Invalid negative price
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/configuration/payment-methods", updateRequest);

        // Assert
        var error = await ValidateErrorResponse(response, 400, "VALIDATION_ERROR");
        Assert.Contains("PricePerRide", error.Details);
    }

    [Fact]
    public async Task UpdatePaymentMethodConfiguration_EmptyDisplayName_ReturnsBadRequest()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var updateRequest = new UpdatePaymentMethodConfigurationRequest
        {
            PaymentMethod = PaymentMethod.PayPal,
            DisplayName = "", // Empty display name
            PaymentUrl = "https://paypal.com/link",
            IsEnabled = true,
            PricePerRide = 25.00m
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/configuration/payment-methods", updateRequest);

        // Assert
        var error = await ValidateErrorResponse(response, 400, "VALIDATION_ERROR");
        Assert.Contains("DisplayName", error.Details);
    }

    [Fact]
    public async Task UpdatePaymentMethodConfiguration_InvalidUrl_ReturnsBadRequest()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var updateRequest = new UpdatePaymentMethodConfigurationRequest
        {
            PaymentMethod = PaymentMethod.PayPal,
            DisplayName = "PayPal Payment",
            PaymentUrl = "not-a-valid-url", // Invalid URL format
            IsEnabled = true,
            PricePerRide = 25.00m
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/configuration/payment-methods", updateRequest);

        // Assert
        var error = await ValidateErrorResponse(response, 400, "VALIDATION_ERROR");
        Assert.Contains("PaymentUrl", error.Details);
    }

    [Fact]
    public async Task UpdatePaymentMethodConfiguration_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        await SeedTestDataAsync();
        // No authorization header set

        var updateRequest = new UpdatePaymentMethodConfigurationRequest
        {
            PaymentMethod = PaymentMethod.CashApp,
            DisplayName = "Unauthorized Update",
            PaymentUrl = "https://cashapp.com/link",
            IsEnabled = true,
            PricePerRide = 25.00m
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/configuration/payment-methods", updateRequest);

        // Assert
        var error = await ValidateErrorResponse(response, 401, "UNAUTHORIZED");
    }

    [Fact]
    public async Task UpdatePaymentMethodConfiguration_DriverRole_ReturnsForbidden()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Driver"); // Wrong role

        var updateRequest = new UpdatePaymentMethodConfigurationRequest
        {
            PaymentMethod = PaymentMethod.CashApp,
            DisplayName = "Driver Attempt",
            PaymentUrl = "https://cashapp.com/link",
            IsEnabled = true,
            PricePerRide = 25.00m
        };

        // Act
        var response = await Client.PutAsJsonAsync("/api/configuration/payment-methods", updateRequest);

        // Assert
        var error = await ValidateErrorResponse(response, 403, "FORBIDDEN");
    }

    [Fact]
    public async Task UpdatePaymentMethodConfiguration_ApiIntegrationToggle_UpdatesCorrectly()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        // First update: Enable API integration
        var enableApiRequest = new UpdatePaymentMethodConfigurationRequest
        {
            PaymentMethod = PaymentMethod.CashApp,
            DisplayName = "CashApp with API",
            PaymentUrl = "https://cashapp.com/api-link",
            IsEnabled = true,
            PricePerRide = 25.00m,
            ApiIntegrationEnabled = true,
            ApiCredentials = "api-key-12345"
        };

        // Act 1
        var enableResponse = await Client.PutAsJsonAsync("/api/configuration/payment-methods", enableApiRequest);

        // Assert 1
        var enabledConfig = await ValidateSuccessResponse<PaymentConfiguration>(enableResponse);
        Assert.True(enabledConfig.ApiIntegrationEnabled);

        // Second update: Disable API integration
        var disableApiRequest = new UpdatePaymentMethodConfigurationRequest
        {
            PaymentMethod = PaymentMethod.CashApp,
            DisplayName = "CashApp Manual Only",
            PaymentUrl = "https://cashapp.com/manual-link",
            IsEnabled = true,
            PricePerRide = 25.00m,
            ApiIntegrationEnabled = false,
            ApiCredentials = "" // Clear credentials
        };

        // Act 2
        var disableResponse = await Client.PutAsJsonAsync("/api/configuration/payment-methods", disableApiRequest);

        // Assert 2
        var disabledConfig = await ValidateSuccessResponse<PaymentConfiguration>(disableResponse);
        Assert.False(disabledConfig.ApiIntegrationEnabled);
    }

    [Fact]
    public async Task UpdatePaymentMethodConfiguration_PersistsToDatabase_ReflectsInSubsequentGets()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var updateRequest = new UpdatePaymentMethodConfigurationRequest
        {
            PaymentMethod = PaymentMethod.PayPal,
            DisplayName = "Updated PayPal Configuration",
            PaymentUrl = "https://paypal.com/updated-link",
            IsEnabled = false, // Disable this payment method
            PricePerRide = 35.00m,
            ApiIntegrationEnabled = true,
            ApiCredentials = "paypal-api-credentials"
        };

        // Act 1: Update configuration
        var updateResponse = await Client.PutAsJsonAsync("/api/configuration/payment-methods", updateRequest);
        await ValidateSuccessResponse<PaymentConfiguration>(updateResponse);

        // Act 2: Get all configurations to verify persistence
        var getResponse = await Client.GetAsync("/api/configuration/payment-methods");
        var allConfigs = await ValidateSuccessResponse<PaymentConfiguration[]>(getResponse);

        // Assert
        var updatedPayPalConfig = allConfigs.First(c => c.PaymentMethod == PaymentMethod.PayPal);

        Assert.Equal("Updated PayPal Configuration", updatedPayPalConfig.DisplayName);
        Assert.Equal("https://paypal.com/updated-link", updatedPayPalConfig.PaymentUrl);
        Assert.False(updatedPayPalConfig.IsEnabled);
        Assert.Equal(35.00m, updatedPayPalConfig.PricePerRide);
        Assert.True(updatedPayPalConfig.ApiIntegrationEnabled);
        // API credentials should not be returned
        Assert.True(string.IsNullOrEmpty(updatedPayPalConfig.ApiCredentials));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates an expired JWT token for testing
    /// </summary>
    private string GenerateExpiredJwtToken()
    {
        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var key = System.Text.Encoding.ASCII.GetBytes("test-secret-key-that-is-long-enough-for-jwt-signing-requirements");

        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, "1"),
            new(System.Security.Claims.ClaimTypes.Name, "testuser"),
            new(System.Security.Claims.ClaimTypes.Role, "Sales")
        };

        var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new System.Security.Claims.ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(-5), // Expired 5 minutes ago
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature),
            Issuer = "DriftRide.TestServer",
            Audience = "DriftRide.Api"
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    #endregion
}

/// <summary>
/// Request model for updating payment method configurations.
/// Maps to API request format for PUT operations.
/// </summary>
public class UpdatePaymentMethodConfigurationRequest
{
    public PaymentMethod PaymentMethod { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string PaymentUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public decimal PricePerRide { get; set; }
    public bool ApiIntegrationEnabled { get; set; } = false;
    public string? ApiCredentials { get; set; }
}