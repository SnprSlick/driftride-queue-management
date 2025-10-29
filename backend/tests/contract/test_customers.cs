using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DriftRide.Api.Models;

namespace DriftRide.Contract.Tests;

/// <summary>
/// Contract tests for Customer endpoints (POST /api/customers)
/// These tests validate request/response schemas, HTTP status codes, authentication, and business rules
/// Tests are designed to FAIL initially until controllers are implemented (TDD approach)
/// </summary>
public class CustomerContractTests : ContractTestBase
{
    public CustomerContractTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task PostCustomers_ValidRequest_ReturnsCreatedCustomer()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var request = new CreateCustomerRequest
        {
            Name = "John Doe",
            Email = "john.doe@example.com",
            Phone = "+1-555-123-4567"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/customers", request, JsonOptions);

        // Assert
        var customer = await ValidateSuccessResponse<Customer>(response, 201);

        Assert.Equal(request.Name, customer.Name);
        Assert.Equal(request.Email, customer.Email);
        Assert.Equal(request.Phone, customer.Phone);
        Assert.True(customer.Id > 0);
        Assert.True(customer.IsActive);
        Assert.True(customer.CreatedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task PostCustomers_MissingName_ReturnsBadRequest()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var request = new CreateCustomerRequest
        {
            Name = "", // Invalid - required field
            Email = "john.doe@example.com",
            Phone = "+1-555-123-4567"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/customers", request, JsonOptions);

        // Assert
        var error = await ValidateErrorResponse(response, 400, "VALIDATION_FAILED");
        Assert.Contains("name", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostCustomers_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var request = new CreateCustomerRequest
        {
            Name = "John Doe",
            Email = "not-an-email", // Invalid email format
            Phone = "+1-555-123-4567"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/customers", request, JsonOptions);

        // Assert
        var error = await ValidateErrorResponse(response, 400, "VALIDATION_FAILED");
        Assert.Contains("email", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostCustomers_MissingEmail_ReturnsBadRequest()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var request = new CreateCustomerRequest
        {
            Name = "John Doe",
            Email = "", // Invalid - required field
            Phone = "+1-555-123-4567"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/customers", request, JsonOptions);

        // Assert
        var error = await ValidateErrorResponse(response, 400, "VALIDATION_FAILED");
        Assert.Contains("email", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostCustomers_DuplicateEmailAllowed_ReturnsCreated()
    {
        // Arrange - According to CLAUDE.md, duplicate customers are allowed (distinguished by timestamp)
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var request1 = new CreateCustomerRequest
        {
            Name = "John Doe",
            Email = "john.doe@example.com",
            Phone = "+1-555-123-4567"
        };

        var request2 = new CreateCustomerRequest
        {
            Name = "Jane Doe", // Different name, same email - should be allowed
            Email = "john.doe@example.com",
            Phone = "+1-555-987-6543"
        };

        // Act
        var response1 = await Client.PostAsJsonAsync("/api/customers", request1, JsonOptions);
        var response2 = await Client.PostAsJsonAsync("/api/customers", request2, JsonOptions);

        // Assert
        var customer1 = await ValidateSuccessResponse<Customer>(response1, 201);
        var customer2 = await ValidateSuccessResponse<Customer>(response2, 201);

        Assert.NotEqual(customer1.Id, customer2.Id);
        Assert.Equal(request1.Email, customer1.Email);
        Assert.Equal(request2.Email, customer2.Email);
        Assert.NotEqual(customer1.CreatedAt, customer2.CreatedAt); // Distinguished by timestamp
    }

    [Fact]
    public async Task PostCustomers_OptionalPhoneNumber_ReturnsCreated()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var request = new CreateCustomerRequest
        {
            Name = "John Doe",
            Email = "john.doe@example.com",
            Phone = null // Optional field
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/customers", request, JsonOptions);

        // Assert
        var customer = await ValidateSuccessResponse<Customer>(response, 201);

        Assert.Equal(request.Name, customer.Name);
        Assert.Equal(request.Email, customer.Email);
        Assert.Null(customer.Phone);
    }

    [Fact]
    public async Task PostCustomers_NoAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await SeedTestDataAsync();
        ClearAuthorizationHeader(); // No authentication

        var request = new CreateCustomerRequest
        {
            Name = "John Doe",
            Email = "john.doe@example.com",
            Phone = "+1-555-123-4567"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/customers", request, JsonOptions);

        // Assert
        var error = await ValidateErrorResponse(response, 401, "UNAUTHORIZED");
        Assert.Contains("authorization", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostCustomers_DriverRole_ReturnsForbidden()
    {
        // Arrange - Only Sales role should be able to create customers
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Driver");

        var request = new CreateCustomerRequest
        {
            Name = "John Doe",
            Email = "john.doe@example.com",
            Phone = "+1-555-123-4567"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/customers", request, JsonOptions);

        // Assert
        var error = await ValidateErrorResponse(response, 403, "FORBIDDEN");
        Assert.Contains("permission", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostCustomers_InvalidJwtToken_ReturnsUnauthorized()
    {
        // Arrange
        await SeedTestDataAsync();
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");

        var request = new CreateCustomerRequest
        {
            Name = "John Doe",
            Email = "john.doe@example.com",
            Phone = "+1-555-123-4567"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/customers", request, JsonOptions);

        // Assert
        var error = await ValidateErrorResponse(response, 401, "UNAUTHORIZED");
    }

    [Fact]
    public async Task PostCustomers_EmptyRequestBody_ReturnsBadRequest()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        // Act
        var response = await Client.PostAsync("/api/customers", new StringContent("", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        var error = await ValidateErrorResponse(response, 400, "BAD_REQUEST");
    }

    [Fact]
    public async Task PostCustomers_InvalidJsonContent_ReturnsBadRequest()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var invalidJson = "{ invalid json }";

        // Act
        var response = await Client.PostAsync("/api/customers", new StringContent(invalidJson, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        var error = await ValidateErrorResponse(response, 400, "BAD_REQUEST");
    }

    [Fact]
    public async Task PostCustomers_NameTooLong_ReturnsBadRequest()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var request = new CreateCustomerRequest
        {
            Name = new string('A', 256), // Assuming max length is 255
            Email = "john.doe@example.com",
            Phone = "+1-555-123-4567"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/customers", request, JsonOptions);

        // Assert
        var error = await ValidateErrorResponse(response, 400, "VALIDATION_FAILED");
        Assert.Contains("name", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostCustomers_ValidatesResponseSchema()
    {
        // Arrange
        await SeedTestDataAsync();
        SetAuthorizationHeader(role: "Sales");

        var request = new CreateCustomerRequest
        {
            Name = "John Doe",
            Email = "john.doe@example.com",
            Phone = "+1-555-123-4567"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/customers", request, JsonOptions);

        // Assert - Validate complete response schema
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        var apiResponse = FromJson<ApiResponse<Customer>>(content);

        // Validate ApiResponse schema
        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Message);
        Assert.NotNull(apiResponse.Data);
        Assert.Null(apiResponse.Error);

        // Validate Customer schema
        var customer = apiResponse.Data;
        Assert.True(customer.Id > 0);
        Assert.Equal(request.Name, customer.Name);
        Assert.Equal(request.Email, customer.Email);
        Assert.Equal(request.Phone, customer.Phone);
        Assert.True(customer.IsActive);
        Assert.True(customer.CreatedAt > DateTime.MinValue);
        Assert.True(customer.CreatedAt <= DateTime.UtcNow);
    }
}