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
/// Integration tests for driver workflow and queue management.
/// Tests the complete driver experience from queue assignment to ride completion.
/// </summary>
public class DriverFlowIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public DriverFlowIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Integration test for complete driver workflow.
    /// Tests: Get current customer → Complete ride → Queue progresses → Get next customer.
    /// </summary>
    [Fact]
    public async Task DriverWorkflow_CompleteFlow_SuccessfullyProcessesQueueProgression()
    {
        // Arrange - Setup test data with multiple customers in queue
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DriftRideDbContext>();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var queueService = scope.ServiceProvider.GetRequiredService<IQueueService>();

        // Clear existing data
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        // Create multiple customers with confirmed payments
        var customer1 = await customerService.CreateAsync("John Driver Test", "555-0101");
        var customer2 = await customerService.CreateAsync("Jane Driver Test", "555-0102");
        var customer3 = await customerService.CreateAsync("Bob Driver Test", "555-0103");

        var payment1 = await paymentService.ProcessAsync(customer1.Id, 25.00m, PaymentMethod.CashApp);
        var payment2 = await paymentService.ProcessAsync(customer2.Id, 25.00m, PaymentMethod.PayPal);
        var payment3 = await paymentService.ProcessAsync(customer3.Id, 25.00m, PaymentMethod.CashInHand);

        // Confirm payments and add to queue
        await paymentService.ConfirmAsync(payment1.Id, true, "Test confirmation", "testsales");
        await paymentService.ConfirmAsync(payment2.Id, true, "Test confirmation", "testsales");
        await paymentService.ConfirmAsync(payment3.Id, true, "Test confirmation", "testsales");

        var queueEntry1 = await queueService.AddToQueueAsync(payment1.Id);
        var queueEntry2 = await queueService.AddToQueueAsync(payment2.Id);
        var queueEntry3 = await queueService.AddToQueueAsync(payment3.Id);

        // Setup driver authentication
        var driverToken = GenerateTestToken("testdriver", "Test Driver", UserRole.Driver);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", driverToken);

        _output.WriteLine($"Setup complete: 3 customers in queue");
        _output.WriteLine($"Customer 1: {customer1.Name} (Position should be 1)");
        _output.WriteLine($"Customer 2: {customer2.Name} (Position should be 2)");
        _output.WriteLine($"Customer 3: {customer3.Name} (Position should be 3)");

        // Act & Assert - Test driver workflow step by step

        // Step 1: Driver gets current customer (should be first in queue)
        var currentResponse = await _client.GetAsync("/api/queue/current");
        currentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var currentContent = await currentResponse.Content.ReadAsStringAsync();
        var currentCustomer = JsonSerializer.Deserialize<QueueEntryResponse>(currentContent, GetJsonOptions());

        currentCustomer.Should().NotBeNull();
        currentCustomer!.Customer.Name.Should().Be("John Driver Test");
        currentCustomer.Position.Should().Be(1);
        currentCustomer.Status.Should().Be(QueueStatus.Waiting);

        _output.WriteLine($"✓ Step 1: Current customer is {currentCustomer.Customer.Name} at position {currentCustomer.Position}");

        // Step 2: Driver completes the current ride
        var completeResponse = await _client.PostAsync($"/api/queue/{currentCustomer.Id}/complete", null);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var completedContent = await completeResponse.Content.ReadAsStringAsync();
        var completedEntry = JsonSerializer.Deserialize<QueueEntryResponse>(completedContent, GetJsonOptions());

        completedEntry.Should().NotBeNull();
        completedEntry!.Status.Should().Be(QueueStatus.Completed);
        completedEntry.CompletedAt.Should().NotBeNull();
        completedEntry.CompletedBy.Should().Be("testdriver");

        _output.WriteLine($"✓ Step 2: Completed ride for {completedEntry.Customer.Name}");

        // Step 3: Verify queue progressed - next customer should now be current
        var newCurrentResponse = await _client.GetAsync("/api/queue/current");
        newCurrentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var newCurrentContent = await newCurrentResponse.Content.ReadAsStringAsync();
        var newCurrentCustomer = JsonSerializer.Deserialize<QueueEntryResponse>(newCurrentContent, GetJsonOptions());

        newCurrentCustomer.Should().NotBeNull();
        newCurrentCustomer!.Customer.Name.Should().Be("Jane Driver Test");
        newCurrentCustomer.Position.Should().Be(1); // Position should update after completion
        newCurrentCustomer.Status.Should().Be(QueueStatus.Waiting);

        _output.WriteLine($"✓ Step 3: New current customer is {newCurrentCustomer.Customer.Name} at position {newCurrentCustomer.Position}");

        // Step 4: Verify full queue state
        var queueResponse = await _client.GetAsync("/api/queue");
        queueResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var queueContent = await queueResponse.Content.ReadAsStringAsync();
        var fullQueue = JsonSerializer.Deserialize<QueueEntryResponse[]>(queueContent, GetJsonOptions());

        fullQueue.Should().NotBeNull();
        fullQueue!.Length.Should().Be(3);

        // First entry should be completed
        var completedQueueEntry = fullQueue.First(q => q.Customer.Name == "John Driver Test");
        completedQueueEntry.Status.Should().Be(QueueStatus.Completed);
        completedQueueEntry.CompletedAt.Should().NotBeNull();

        // Second entry should be current (waiting at position 1)
        var currentQueueEntry = fullQueue.First(q => q.Customer.Name == "Jane Driver Test");
        currentQueueEntry.Status.Should().Be(QueueStatus.Waiting);
        currentQueueEntry.Position.Should().Be(1);

        // Third entry should be waiting at position 2
        var waitingQueueEntry = fullQueue.First(q => q.Customer.Name == "Bob Driver Test");
        waitingQueueEntry.Status.Should().Be(QueueStatus.Waiting);
        waitingQueueEntry.Position.Should().Be(2);

        _output.WriteLine($"✓ Step 4: Queue state verified - 1 completed, 2 waiting");

        // Step 5: Complete remaining rides to test full queue processing
        await _client.PostAsync($"/api/queue/{newCurrentCustomer.Id}/complete", null);

        var finalCurrentResponse = await _client.GetAsync("/api/queue/current");
        finalCurrentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var finalCurrentContent = await finalCurrentResponse.Content.ReadAsStringAsync();
        var finalCurrentCustomer = JsonSerializer.Deserialize<QueueEntryResponse>(finalCurrentContent, GetJsonOptions());

        finalCurrentCustomer!.Customer.Name.Should().Be("Bob Driver Test");
        finalCurrentCustomer.Position.Should().Be(1);

        _output.WriteLine($"✓ Step 5: Final current customer is {finalCurrentCustomer.Customer.Name}");

        // Step 6: Complete last customer and verify empty queue
        await _client.PostAsync($"/api/queue/{finalCurrentCustomer.Id}/complete", null);

        var emptyQueueResponse = await _client.GetAsync("/api/queue/current");
        emptyQueueResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _output.WriteLine($"✓ Step 6: Queue is now empty (204 No Content)");
    }

    /// <summary>
    /// Integration test for driver workflow with duplicate customer names.
    /// Validates that drivers can distinguish between customers with same names using timestamps.
    /// </summary>
    [Fact]
    public async Task DriverWorkflow_DuplicateNames_HandlesTimestampDisambiguation()
    {
        // Arrange - Setup customers with duplicate names
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DriftRideDbContext>();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var queueService = scope.ServiceProvider.GetRequiredService<IQueueService>();

        // Clear existing data
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        // Create customers with same name but different timestamps
        var customer1 = await customerService.CreateAsync("John Smith", "555-0101");
        await Task.Delay(100); // Ensure different timestamps
        var customer2 = await customerService.CreateAsync("John Smith", "555-0102");

        var payment1 = await paymentService.ProcessAsync(customer1.Id, 25.00m, PaymentMethod.CashApp);
        var payment2 = await paymentService.ProcessAsync(customer2.Id, 25.00m, PaymentMethod.PayPal);

        await paymentService.ConfirmAsync(payment1.Id, true, "Test confirmation", "testsales");
        await paymentService.ConfirmAsync(payment2.Id, true, "Test confirmation", "testsales");

        await queueService.AddToQueueAsync(payment1.Id);
        await queueService.AddToQueueAsync(payment2.Id);

        var driverToken = GenerateTestToken("testdriver", "Test Driver", UserRole.Driver);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", driverToken);

        // Act - Get current customer and full queue
        var currentResponse = await _client.GetAsync("/api/queue/current");
        var queueResponse = await _client.GetAsync("/api/queue");

        // Assert
        currentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        queueResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var currentContent = await currentResponse.Content.ReadAsStringAsync();
        var currentCustomer = JsonSerializer.Deserialize<QueueEntryResponse>(currentContent, GetJsonOptions());

        var queueContent = await queueResponse.Content.ReadAsStringAsync();
        var fullQueue = JsonSerializer.Deserialize<QueueEntryResponse[]>(queueContent, GetJsonOptions());

        // Both customers should have same name but different timestamps
        currentCustomer!.Customer.Name.Should().Be("John Smith");
        fullQueue!.Length.Should().Be(2);
        fullQueue.All(q => q.Customer.Name == "John Smith").Should().BeTrue();

        // Should be distinguishable by CreatedAt timestamps
        var timestamps = fullQueue.Select(q => q.Customer.CreatedAt).ToArray();
        timestamps[0].Should().NotBe(timestamps[1]);

        _output.WriteLine($"✓ Duplicate names handled: Both named 'John Smith' but different timestamps");
        _output.WriteLine($"  Customer 1: {timestamps[0]:HH:mm:ss.fff}");
        _output.WriteLine($"  Customer 2: {timestamps[1]:HH:mm:ss.fff}");
    }

    /// <summary>
    /// Integration test for driver workflow performance.
    /// Validates that ride completion meets the <15-second target.
    /// </summary>
    [Fact]
    public async Task DriverWorkflow_Performance_CompletesRideWithinTarget()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DriftRideDbContext>();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var queueService = scope.ServiceProvider.GetRequiredService<IQueueService>();

        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        var customer = await customerService.CreateAsync("Performance Test", "555-9999");
        var payment = await paymentService.ProcessAsync(customer.Id, 25.00m, PaymentMethod.CashApp);
        await paymentService.ConfirmAsync(payment.Id, true, "Test confirmation", "testsales");
        var queueEntry = await queueService.AddToQueueAsync(payment.Id);

        var driverToken = GenerateTestToken("testdriver", "Test Driver", UserRole.Driver);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", driverToken);

        // Act - Measure complete ride operation time
        var startTime = DateTime.UtcNow;

        var currentResponse = await _client.GetAsync("/api/queue/current");
        var currentContent = await currentResponse.Content.ReadAsStringAsync();
        var currentCustomer = JsonSerializer.Deserialize<QueueEntryResponse>(currentContent, GetJsonOptions());

        var completeResponse = await _client.PostAsync($"/api/queue/{currentCustomer!.Id}/complete", null);

        var endTime = DateTime.UtcNow;
        var operationTime = endTime - startTime;

        // Assert
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        operationTime.TotalSeconds.Should().BeLessThan(15, "Driver workflow should complete within 15-second target");

        _output.WriteLine($"✓ Performance test: Ride completion took {operationTime.TotalMilliseconds:F0}ms");
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
/// Response model for queue entry API endpoints.
/// Matches contract test model and OpenAPI specification.
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
/// Response model for customer information.
/// </summary>
public class CustomerResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Response model for payment information.
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