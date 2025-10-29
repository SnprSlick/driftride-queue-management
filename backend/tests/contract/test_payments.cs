using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DriftRide.Api.Models;

namespace DriftRide.Contract.Tests;

/// <summary>
/// Contract tests for Payment endpoints
/// - POST /api/payments - Record payment attempt
/// - POST /api/payments/{id}/confirm - Sales confirms/denies payment
/// - GET /api/payments/pending - Sales view pending payments
/// These tests validate request/response schemas, HTTP status codes, authentication, and business rules
/// Tests are designed to FAIL initially until controllers are implemented (TDD approach)
/// </summary>
public class PaymentContractTests : ContractTestBase
{
    public PaymentContractTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    #region POST /api/payments Tests

    [Fact]
    public async Task PostPayments_ValidRequest_ReturnsCreatedPayment()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        // Create a customer first
        var customer = await CreateTestCustomerAsync();

        var request = new CreatePaymentRequest
        {
            CustomerId = customer.Id,
            Amount = 25.00m,
            PaymentMethod = PaymentMethod.CashApp,
            ExternalTransactionId = "test-transaction-123"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/payments", request, JsonOptions);

        // Assert
        var payment = await ValidateSuccessResponse<Payment>(response, 201);

        Assert.Equal(request.CustomerId, payment.CustomerId);
        Assert.Equal(request.Amount, payment.Amount);
        Assert.Equal(request.PaymentMethod, payment.PaymentMethod);
        Assert.Equal(request.ExternalTransactionId, payment.ExternalTransactionId);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.True(payment.Id > 0);
        Assert.True(payment.CreatedAt > DateTime.MinValue);
        Assert.Null(payment.ConfirmedAt);
        Assert.Null(payment.ConfirmedByUsername);
    }

    [Fact]
    public async Task PostPayments_NonExistentCustomer_ReturnsNotFound()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var request = new CreatePaymentRequest
        {
            CustomerId = 9999, // Non-existent customer
            Amount = 25.00m,
            PaymentMethod = PaymentMethod.CashApp,
            ExternalTransactionId = "test-transaction-123"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/payments", request, JsonOptions);

        // Assert
        var error = await ValidateErrorResponse(response, 404, "NOT_FOUND");
        Assert.Contains("customer", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostPayments_InvalidAmount_ReturnsBadRequest()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");
        var customer = await CreateTestCustomerAsync();

        var request = new CreatePaymentRequest
        {
            CustomerId = customer.Id,
            Amount = -5.00m, // Invalid negative amount
            PaymentMethod = PaymentMethod.CashApp,
            ExternalTransactionId = "test-transaction-123"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/payments", request, JsonOptions);

        // Assert
        var error = await ValidateErrorResponse(response, 400, "VALIDATION_FAILED");
        Assert.Contains("amount", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostPayments_InvalidPaymentMethod_ReturnsBadRequest()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");
        var customer = await CreateTestCustomerAsync();

        var requestJson = $@"{{
            ""customerId"": {customer.Id},
            ""amount"": 25.00,
            ""paymentMethod"": ""InvalidMethod"",
            ""externalTransactionId"": ""test-transaction-123""
        }}";

        // Act
        var response = await Client.PostAsync("/api/payments", new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        var error = await ValidateErrorResponse(response, 400, "VALIDATION_FAILED");
        Assert.Contains("paymentMethod", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostPayments_CustomerAlreadyHasPendingPayment_ReturnsConflict()
    {
        // Arrange - Business rule: customer can't have multiple pending payments
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");
        var customer = await CreateTestCustomerAsync();

        var request1 = new CreatePaymentRequest
        {
            CustomerId = customer.Id,
            Amount = 25.00m,
            PaymentMethod = PaymentMethod.CashApp,
            ExternalTransactionId = "test-transaction-123"
        };

        var request2 = new CreatePaymentRequest
        {
            CustomerId = customer.Id,
            Amount = 25.00m,
            PaymentMethod = PaymentMethod.PayPal,
            ExternalTransactionId = "test-transaction-456"
        };

        // Act
        var response1 = await Client.PostAsJsonAsync("/api/payments", request1, JsonOptions);
        var response2 = await Client.PostAsJsonAsync("/api/payments", request2, JsonOptions);

        // Assert
        await ValidateSuccessResponse<Payment>(response1, 201); // First payment should succeed
        var error = await ValidateErrorResponse(response2, 409, "CONFLICT");
        Assert.Contains("pending", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostPayments_DriverRole_ReturnsForbidden()
    {
        // Arrange - Only Sales role should create payments
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Driver");
        var customer = await CreateTestCustomerAsync();

        var request = new CreatePaymentRequest
        {
            CustomerId = customer.Id,
            Amount = 25.00m,
            PaymentMethod = PaymentMethod.CashApp,
            ExternalTransactionId = "test-transaction-123"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/payments", request, JsonOptions);

        // Assert
        var error = await ValidateErrorResponse(response, 403, "FORBIDDEN");
    }

    #endregion

    #region POST /api/payments/{id}/confirm Tests

    [Fact]
    public async Task PostPaymentsConfirm_ConfirmPayment_ReturnsUpdatedPayment()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");
        var customer = await CreateTestCustomerAsync();
        var payment = await CreateTestPaymentAsync(customer.Id);

        var request = new ConfirmPaymentRequest
        {
            Confirmed = true,
            Notes = "Payment verified manually"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/payments/{payment.Id}/confirm", request, JsonOptions);

        // Assert
        var confirmedPayment = await ValidateSuccessResponse<Payment>(response);

        Assert.Equal(PaymentStatus.Confirmed, confirmedPayment.Status);
        Assert.Equal(request.Notes, confirmedPayment.ConfirmationNotes);
        Assert.Equal("testuser", confirmedPayment.ConfirmedByUsername);
        Assert.True(confirmedPayment.ConfirmedAt.HasValue);
        Assert.True(confirmedPayment.ConfirmedAt.Value <= DateTime.UtcNow);
    }

    [Fact]
    public async Task PostPaymentsConfirm_DenyPayment_ReturnsUpdatedPayment()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");
        var customer = await CreateTestCustomerAsync();
        var payment = await CreateTestPaymentAsync(customer.Id);

        var request = new ConfirmPaymentRequest
        {
            Confirmed = false,
            Notes = "Payment not verified"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/payments/{payment.Id}/confirm", request, JsonOptions);

        // Assert
        var deniedPayment = await ValidateSuccessResponse<Payment>(response);

        Assert.Equal(PaymentStatus.Denied, deniedPayment.Status);
        Assert.Equal(request.Notes, deniedPayment.ConfirmationNotes);
        Assert.Equal("testuser", deniedPayment.ConfirmedByUsername);
        Assert.True(deniedPayment.ConfirmedAt.HasValue);
    }

    [Fact]
    public async Task PostPaymentsConfirm_NonExistentPayment_ReturnsNotFound()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var request = new ConfirmPaymentRequest
        {
            Confirmed = true,
            Notes = "Payment verified"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/payments/9999/confirm", request, JsonOptions);

        // Assert
        var error = await ValidateErrorResponse(response, 404, "NOT_FOUND");
        Assert.Contains("payment", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostPaymentsConfirm_AlreadyConfirmedPayment_ReturnsConflict()
    {
        // Arrange - Payment already confirmed
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");
        var customer = await CreateTestCustomerAsync();
        var payment = await CreateTestPaymentAsync(customer.Id);

        var confirmRequest = new ConfirmPaymentRequest
        {
            Confirmed = true,
            Notes = "Already confirmed"
        };

        // Confirm it first
        await Client.PostAsJsonAsync($"/api/payments/{payment.Id}/confirm", confirmRequest, JsonOptions);

        // Try to confirm again
        var secondRequest = new ConfirmPaymentRequest
        {
            Confirmed = false,
            Notes = "Trying to change"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/payments/{payment.Id}/confirm", secondRequest, JsonOptions);

        // Assert
        var error = await ValidateErrorResponse(response, 409, "CONFLICT");
        Assert.Contains("already", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostPaymentsConfirm_DriverRole_ReturnsForbidden()
    {
        // Arrange - Only Sales role should confirm payments
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Driver");
        var customer = await CreateTestCustomerAsync();
        var payment = await CreateTestPaymentAsync(customer.Id);

        var request = new ConfirmPaymentRequest
        {
            Confirmed = true,
            Notes = "Payment verified"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/payments/{payment.Id}/confirm", request, JsonOptions);

        // Assert
        var error = await ValidateErrorResponse(response, 403, "FORBIDDEN");
    }

    #endregion

    #region GET /api/payments/pending Tests

    [Fact]
    public async Task GetPaymentsPending_WithPendingPayments_ReturnsPaymentsList()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var customer1 = await CreateTestCustomerAsync();
        var customer2 = await CreateTestCustomerAsync();

        await CreateTestPaymentAsync(customer1.Id); // Pending
        await CreateTestPaymentAsync(customer2.Id); // Pending

        // Act
        var response = await Client.GetAsync("/api/payments/pending");

        // Assert
        var payments = await ValidateSuccessResponse<Payment[]>(response);

        Assert.Equal(2, payments.Length);
        Assert.All(payments, p => Assert.Equal(PaymentStatus.Pending, p.Status));

        // Validate each payment has required fields
        Assert.All(payments, p =>
        {
            Assert.True(p.Id > 0);
            Assert.True(p.CustomerId > 0);
            Assert.True(p.Amount > 0);
            Assert.True(Enum.IsDefined(typeof(PaymentMethod), p.PaymentMethod));
            Assert.True(p.CreatedAt > DateTime.MinValue);
            Assert.Null(p.ConfirmedAt);
            Assert.Null(p.ConfirmedByUsername);
        });
    }

    [Fact]
    public async Task GetPaymentsPending_NoPendingPayments_ReturnsEmptyArray()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        // Act
        var response = await Client.GetAsync("/api/payments/pending");

        // Assert
        var payments = await ValidateSuccessResponse<Payment[]>(response);
        Assert.Empty(payments);
    }

    [Fact]
    public async Task GetPaymentsPending_OnlyConfirmedPayments_ReturnsEmptyArray()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var customer = await CreateTestCustomerAsync();
        var payment = await CreateTestPaymentAsync(customer.Id);

        // Confirm the payment
        var confirmRequest = new ConfirmPaymentRequest
        {
            Confirmed = true,
            Notes = "Confirmed"
        };
        await Client.PostAsJsonAsync($"/api/payments/{payment.Id}/confirm", confirmRequest, JsonOptions);

        // Act
        var response = await Client.GetAsync("/api/payments/pending");

        // Assert
        var payments = await ValidateSuccessResponse<Payment[]>(response);
        Assert.Empty(payments);
    }

    [Fact]
    public async Task GetPaymentsPending_DriverRole_ReturnsForbidden()
    {
        // Arrange - Only Sales role should view pending payments
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Driver");

        // Act
        var response = await Client.GetAsync("/api/payments/pending");

        // Assert
        var error = await ValidateErrorResponse(response, 403, "FORBIDDEN");
    }

    [Fact]
    public async Task GetPaymentsPending_NoAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await SeedTestDataAsync();
        ClearAuthorizationHeader();

        // Act
        var response = await Client.GetAsync("/api/payments/pending");

        // Assert
        var error = await ValidateErrorResponse(response, 401, "UNAUTHORIZED");
    }

    #endregion

    #region Helper Methods

    private async Task<Customer> CreateTestCustomerAsync()
    {
        var request = new CreateCustomerRequest
        {
            Name = $"Test Customer {Guid.NewGuid().ToString()[0..8]}",
            Email = $"customer{Guid.NewGuid().ToString()[0..8]}@example.com",
            Phone = "+1-555-123-4567"
        };

        var response = await Client.PostAsJsonAsync("/api/customers", request, JsonOptions);
        return await ValidateSuccessResponse<Customer>(response, 201);
    }

    private async Task<Payment> CreateTestPaymentAsync(int customerId)
    {
        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            Amount = 25.00m,
            PaymentMethod = PaymentMethod.CashApp,
            ExternalTransactionId = $"test-txn-{Guid.NewGuid().ToString()[0..8]}"
        };

        var response = await Client.PostAsJsonAsync("/api/payments", request, JsonOptions);
        return await ValidateSuccessResponse<Payment>(response, 201);
    }

    #endregion
}

/// <summary>
/// Request models for payment endpoints (should match API contract)
/// </summary>
public class CreatePaymentRequest
{
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ExternalTransactionId { get; set; }
}

public class ConfirmPaymentRequest
{
    public bool Confirmed { get; set; }
    public string? Notes { get; set; }
}