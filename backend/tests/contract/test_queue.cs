using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DriftRide.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DriftRide.Contract.Tests;

/// <summary>
/// Contract tests for Queue API endpoints.
/// Validates API contracts match OpenAPI specification for driver queue management.
/// </summary>
public class QueueContractTests : ContractTestBase
{
    private readonly string _driverToken;
    private readonly string _salesToken;

    public QueueContractTests()
    {
        // Setup test tokens for different roles
        _driverToken = GenerateTestToken("testdriver", "Test Driver", UserRole.Driver);
        _salesToken = GenerateTestToken("testsales", "Test Sales", UserRole.Sales);
    }

    /// <summary>
    /// Contract test for GET /queue/current endpoint.
    /// Validates driver can retrieve current customer or receive 204 when queue is empty.
    /// </summary>
    [Fact]
    public async Task GetCurrentCustomer_WithValidDriverAuth_ReturnsCurrentCustomerOrNoContent()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _driverToken);

        // Act
        var response = await Client.GetAsync("/api/queue/current");

        // Assert
        response.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty();

            var queueEntry = JsonSerializer.Deserialize<QueueEntryResponse>(content, JsonOptions);
            queueEntry.Should().NotBeNull();
            queueEntry!.Id.Should().NotBeEmpty();
            queueEntry.Customer.Should().NotBeNull();
            queueEntry.Customer.Name.Should().NotBeNullOrEmpty();
            queueEntry.Position.Should().BeGreaterThan(0);
            queueEntry.Status.Should().Be(QueueStatus.Waiting);
        }
    }

    /// <summary>
    /// Contract test for GET /queue/current endpoint without authentication.
    /// Validates endpoint requires authentication.
    /// </summary>
    [Fact]
    public async Task GetCurrentCustomer_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/api/queue/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Contract test for GET /queue/current endpoint with wrong role.
    /// Validates endpoint requires Driver role.
    /// </summary>
    [Fact]
    public async Task GetCurrentCustomer_WithSalesRole_ReturnsForbidden()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _salesToken);

        // Act
        var response = await Client.GetAsync("/api/queue/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Contract test for POST /queue/{id}/complete endpoint.
    /// Validates driver can complete rides and receive updated queue entry.
    /// </summary>
    [Fact]
    public async Task CompleteRide_WithValidDriverAuthAndQueueEntry_ReturnsCompletedEntry()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _driverToken);

        var testQueueEntryId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/api/queue/{testQueueEntryId}/complete", null);

        // Assert - Either success with completed entry or 404 if entry doesn't exist
        response.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty();

            var queueEntry = JsonSerializer.Deserialize<QueueEntryResponse>(content, JsonOptions);
            queueEntry.Should().NotBeNull();
            queueEntry!.Id.Should().Be(testQueueEntryId);
            queueEntry.Status.Should().Be(QueueStatus.Completed);
            queueEntry.CompletedAt.Should().NotBeNull();
            queueEntry.CompletedBy.Should().NotBeNullOrEmpty();
        }
    }

    /// <summary>
    /// Contract test for POST /queue/{id}/complete endpoint without authentication.
    /// Validates endpoint requires authentication.
    /// </summary>
    [Fact]
    public async Task CompleteRide_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var testQueueEntryId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/api/queue/{testQueueEntryId}/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Contract test for POST /queue/{id}/complete endpoint with wrong role.
    /// Validates endpoint requires Driver role.
    /// </summary>
    [Fact]
    public async Task CompleteRide_WithSalesRole_ReturnsForbidden()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _salesToken);

        var testQueueEntryId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/api/queue/{testQueueEntryId}/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Contract test for POST /queue/{id}/complete endpoint with invalid ID format.
    /// Validates endpoint handles malformed GUIDs.
    /// </summary>
    [Fact]
    public async Task CompleteRide_WithInvalidId_ReturnsBadRequest()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _driverToken);

        // Act
        var response = await Client.PostAsync("/api/queue/invalid-id/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Contract test for GET /queue endpoint.
    /// Validates sales staff can retrieve full queue state.
    /// </summary>
    [Fact]
    public async Task GetQueue_WithValidSalesAuth_ReturnsQueueArray()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _salesToken);

        // Act
        var response = await Client.GetAsync("/api/queue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();

        var queue = JsonSerializer.Deserialize<QueueEntryResponse[]>(content, JsonOptions);
        queue.Should().NotBeNull();

        // If queue has entries, validate structure
        if (queue!.Length > 0)
        {
            var firstEntry = queue[0];
            firstEntry.Id.Should().NotBeEmpty();
            firstEntry.Customer.Should().NotBeNull();
            firstEntry.Customer.Name.Should().NotBeNullOrEmpty();
            firstEntry.Position.Should().BeGreaterThan(0);
            firstEntry.Status.Should().BeOneOf(QueueStatus.Waiting, QueueStatus.InProgress, QueueStatus.Completed);
        }
    }

    /// <summary>
    /// Contract test for GET /queue endpoint without authentication.
    /// Validates endpoint requires authentication.
    /// </summary>
    [Fact]
    public async Task GetQueue_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/api/queue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Contract test for GET /queue endpoint with driver role.
    /// Validates drivers can also view queue (for context).
    /// </summary>
    [Fact]
    public async Task GetQueue_WithDriverRole_ReturnsQueueArray()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _driverToken);

        // Act
        var response = await Client.GetAsync("/api/queue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var queue = JsonSerializer.Deserialize<QueueEntryResponse[]>(content, JsonOptions);
        queue.Should().NotBeNull();
    }
}

/// <summary>
/// Response model for queue entry API endpoints.
/// Matches OpenAPI specification for QueueEntryResponse.
/// </summary>
public class QueueEntryResponse
{
    public Guid Id { get; set; }
    public CustomerResponse Customer { get; set; } = null!;
    public PaymentResponse Payment { get; set; } = null!;
    public int Position { get; set; }
    public QueueStatus Status { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
}

/// <summary>
/// Response model for customer information in queue entries.
/// Matches OpenAPI specification for CustomerResponse.
/// </summary>
public class CustomerResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Response model for payment information in queue entries.
/// Matches OpenAPI specification for PaymentResponse.
/// </summary>
public class PaymentResponse
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; }
}