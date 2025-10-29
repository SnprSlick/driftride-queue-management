using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.SignalR;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Xunit;
using DriftRide.Data;
using DriftRide.Models;
using DriftRide.Services;
using DriftRide.Hubs;

namespace DriftRide.Integration.Tests
{
    /// <summary>
    /// Integration tests for User Story 1: Customer Payment and Queue Entry Flow
    /// Tests the complete end-to-end workflow from customer arrival to queue confirmation
    ///
    /// Test Coverage:
    /// - Customer registration and payment initiation
    /// - Payment method selection (CashApp, PayPal, Cash)
    /// - Sales staff payment confirmation/denial
    /// - Automatic queue entry upon payment confirmation
    /// - Real-time notifications via SignalR
    /// - Business rule enforcement (single pending payment, manual override)
    /// </summary>
    public class PaymentFlowIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly DriftRideDbContext _dbContext;
        private readonly MockSignalRService _mockSignalR;
        private readonly string _salesUserToken;

        public PaymentFlowIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Replace SQL Server with InMemory database
                    services.RemoveAll(typeof(DbContextOptions<DriftRideDbContext>));
                    services.AddDbContext<DriftRideDbContext>(options =>
                    {
                        options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
                    });

                    // Mock SignalR for notification testing
                    services.RemoveAll(typeof(INotificationService));
                    var mockSignalR = new MockSignalRService();
                    services.AddSingleton<INotificationService>(mockSignalR);
                    services.AddSingleton(mockSignalR); // For test access
                });
            });

            _client = _factory.CreateClient();

            // Get test database context
            using var scope = _factory.Services.CreateScope();
            _dbContext = scope.ServiceProvider.GetRequiredService<DriftRideDbContext>();
            _mockSignalR = scope.ServiceProvider.GetRequiredService<MockSignalRService>();

            // Ensure database is created and seeded
            _dbContext.Database.EnsureCreated();
            SeedTestData();

            // Generate JWT token for sales user authentication
            _salesUserToken = GenerateSalesUserToken();
        }

        #region Test Setup and Teardown

        private void SeedTestData()
        {
            // Create test sales user
            var salesUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "testsales",
                DisplayName = "Test Sales User",
                Role = UserRole.Sales,
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("testpassword"),
                CreatedAt = DateTime.UtcNow
            };

            // Create test driver user
            var driverUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "testdriver",
                DisplayName = "Test Driver",
                Role = UserRole.Driver,
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("testpassword"),
                CreatedAt = DateTime.UtcNow
            };

            // Create payment configurations
            var paymentConfigs = new[]
            {
                new PaymentConfiguration
                {
                    Id = Guid.NewGuid(),
                    PaymentMethod = PaymentMethod.CashApp,
                    DisplayName = "CashApp",
                    PaymentUrl = "https://cash.app/$testaccount",
                    IsEnabled = true,
                    PricePerRide = 25.00m,
                    ApiIntegrationEnabled = false
                },
                new PaymentConfiguration
                {
                    Id = Guid.NewGuid(),
                    PaymentMethod = PaymentMethod.PayPal,
                    DisplayName = "PayPal",
                    PaymentUrl = "https://paypal.me/testaccount",
                    IsEnabled = true,
                    PricePerRide = 25.00m,
                    ApiIntegrationEnabled = false
                },
                new PaymentConfiguration
                {
                    Id = Guid.NewGuid(),
                    PaymentMethod = PaymentMethod.CashInHand,
                    DisplayName = "Cash In Hand",
                    PaymentUrl = "",
                    IsEnabled = true,
                    PricePerRide = 25.00m,
                    ApiIntegrationEnabled = false
                }
            };

            _dbContext.Users.AddRange(salesUser, driverUser);
            _dbContext.PaymentConfigurations.AddRange(paymentConfigs);
            _dbContext.SaveChanges();
        }

        private string GenerateSalesUserToken()
        {
            // This would integrate with your JWT service implementation
            // For now, returning a placeholder that tests will expect to fail
            return "Bearer test-jwt-token-sales-user";
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
            _client?.Dispose();
        }

        #endregion

        #region Happy Path Tests

        /// <summary>
        /// Test Scenario 1: Complete successful customer payment flow
        /// SUCCESS CRITERIA: Customer completes payment in under 3 minutes (per plan.md)
        /// SALES CRITERIA: Sales confirmation in under 30 seconds (per plan.md)
        /// </summary>
        [Fact]
        public async Task CompletePaymentFlow_HappyPath_CustomerSuccessfullyAddedToQueue()
        {
            // ARRANGE: Customer arrival scenario
            var customerData = new
            {
                Name = "John Doe",
                PhoneNumber = "+1234567890"
            };

            var startTime = DateTime.UtcNow;

            // ACT 1: Customer creates account and initiates payment
            // POST /customers - Customer registration
            var customerResponse = await _client.PostAsJsonAsync("/api/customers", customerData);

            // Assert registration success (this will fail until controller is implemented)
            Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);
            var customerResult = await customerResponse.Content.ReadFromJsonAsync<CustomerCreationResponse>();
            Assert.NotNull(customerResult);
            var customerId = customerResult.CustomerId;

            // Verify customer can see payment options
            var paymentOptionsResponse = await _client.GetAsync("/api/configuration/payment-methods");
            Assert.Equal(HttpStatusCode.OK, paymentOptionsResponse.StatusCode);
            var paymentOptions = await paymentOptionsResponse.Content.ReadFromJsonAsync<PaymentMethodsResponse>();
            Assert.NotEmpty(paymentOptions.PaymentMethods);
            Assert.Contains(paymentOptions.PaymentMethods, pm => pm.PaymentMethod == PaymentMethod.CashApp);

            // ACT 2: Customer selects payment method and initiates payment
            var paymentData = new
            {
                CustomerId = customerId,
                Amount = 25.00m,
                PaymentMethod = PaymentMethod.CashApp,
                ExternalTransactionId = "cashapp_test_12345"
            };

            var paymentResponse = await _client.PostAsJsonAsync("/api/payments", paymentData);
            Assert.Equal(HttpStatusCode.Created, paymentResponse.StatusCode);
            var paymentResult = await paymentResponse.Content.ReadFromJsonAsync<PaymentCreationResponse>();
            var paymentId = paymentResult.PaymentId;

            // Verify customer can mark payment as completed
            var markCompleteResponse = await _client.PostAsync($"/api/payments/{paymentId}/mark-complete", null);
            Assert.Equal(HttpStatusCode.OK, markCompleteResponse.StatusCode);

            // Verify payment appears in sales pending list
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _salesUserToken);

            var pendingPaymentsResponse = await _client.GetAsync("/api/payments/pending");
            Assert.Equal(HttpStatusCode.OK, pendingPaymentsResponse.StatusCode);
            var pendingPayments = await pendingPaymentsResponse.Content.ReadFromJsonAsync<PendingPaymentsResponse>();
            Assert.Contains(pendingPayments.Payments, p => p.Id == paymentId);

            // ACT 3: Sales staff confirms payment (within 30-second target)
            var confirmationData = new
            {
                Confirmed = true,
                Notes = "Payment verified via CashApp"
            };

            var confirmResponse = await _client.PostAsJsonAsync($"/api/payments/{paymentId}/confirm", confirmationData);
            Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

            var confirmationTime = DateTime.UtcNow;
            var salesProcessingTime = confirmationTime - startTime;

            // Verify sales confirmation meets performance criteria (30 seconds)
            Assert.True(salesProcessingTime.TotalSeconds <= 30,
                $"Sales confirmation took {salesProcessingTime.TotalSeconds} seconds, exceeds 30-second target");

            // ACT 4: Verify customer automatically added to queue
            var queueResponse = await _client.GetAsync("/api/queue");
            Assert.Equal(HttpStatusCode.OK, queueResponse.StatusCode);
            var queueState = await queueResponse.Content.ReadFromJsonAsync<QueueStateResponse>();
            Assert.Contains(queueState.Entries, q => q.CustomerId == customerId && q.PaymentId == paymentId);

            // ASSERT: Verify complete flow timing meets customer experience criteria (3 minutes)
            var totalTime = DateTime.UtcNow - startTime;
            Assert.True(totalTime.TotalMinutes <= 3,
                $"Complete customer flow took {totalTime.TotalMinutes} minutes, exceeds 3-minute target");

            // ASSERT: Verify real-time notifications were sent
            Assert.Contains(_mockSignalR.SentNotifications, n =>
                n.Type == "PaymentStatusUpdate" && n.Data.Contains(paymentId.ToString()));
            Assert.Contains(_mockSignalR.SentNotifications, n =>
                n.Type == "QueueUpdate" && n.Data.Contains(customerId.ToString()));
        }

        /// <summary>
        /// Test Scenario 2: PayPal payment method selection and completion
        /// </summary>
        [Fact]
        public async Task PayPalPaymentFlow_CustomerSelectsPayPal_PaymentSuccessfullyProcessed()
        {
            // ARRANGE
            var customerData = new { Name = "Jane Smith", PhoneNumber = "+1987654321" };

            // ACT: Customer registration and PayPal payment
            var customerResponse = await _client.PostAsJsonAsync("/api/customers", customerData);
            var customerResult = await customerResponse.Content.ReadFromJsonAsync<CustomerCreationResponse>();
            var customerId = customerResult.CustomerId;

            var paymentData = new
            {
                CustomerId = customerId,
                Amount = 25.00m,
                PaymentMethod = PaymentMethod.PayPal,
                ExternalTransactionId = "paypal_test_67890"
            };

            var paymentResponse = await _client.PostAsJsonAsync("/api/payments", paymentData);
            var paymentResult = await paymentResponse.Content.ReadFromJsonAsync<PaymentCreationResponse>();

            // ASSERT: Payment created with correct method
            Assert.Equal(HttpStatusCode.Created, paymentResponse.StatusCode);
            Assert.NotNull(paymentResult);

            // Verify PayPal-specific handling
            var paymentDetailsResponse = await _client.GetAsync($"/api/payments/{paymentResult.PaymentId}");
            var paymentDetails = await paymentDetailsResponse.Content.ReadFromJsonAsync<PaymentDetailsResponse>();
            Assert.Equal(PaymentMethod.PayPal, paymentDetails.PaymentMethod);
            Assert.Equal("paypal_test_67890", paymentDetails.ExternalTransactionId);
        }

        /// <summary>
        /// Test Scenario 3: Cash in hand payment method
        /// </summary>
        [Fact]
        public async Task CashInHandPaymentFlow_CustomerSelectsCash_NoExternalTransactionRequired()
        {
            // ARRANGE
            var customerData = new { Name = "Bob Johnson" };

            // ACT: Cash payment (no external transaction ID required)
            var customerResponse = await _client.PostAsJsonAsync("/api/customers", customerData);
            var customerResult = await customerResponse.Content.ReadFromJsonAsync<CustomerCreationResponse>();

            var paymentData = new
            {
                CustomerId = customerResult.CustomerId,
                Amount = 25.00m,
                PaymentMethod = PaymentMethod.CashInHand
                // No ExternalTransactionId for cash
            };

            var paymentResponse = await _client.PostAsJsonAsync("/api/payments", paymentData);

            // ASSERT: Cash payment accepted without external transaction
            Assert.Equal(HttpStatusCode.Created, paymentResponse.StatusCode);
        }

        #endregion

        #region Business Scenario Tests

        /// <summary>
        /// Test Scenario 4: Sales staff denies payment
        /// </summary>
        [Fact]
        public async Task PaymentDenial_SalesStaffDeniesPayment_CustomerNotAddedToQueue()
        {
            // ARRANGE: Customer and payment setup
            var customerResponse = await _client.PostAsJsonAsync("/api/customers",
                new { Name = "Denied Customer" });
            var customerResult = await customerResponse.Content.ReadFromJsonAsync<CustomerCreationResponse>();

            var paymentResponse = await _client.PostAsJsonAsync("/api/payments", new
            {
                CustomerId = customerResult.CustomerId,
                Amount = 25.00m,
                PaymentMethod = PaymentMethod.CashApp,
                ExternalTransactionId = "failed_payment_123"
            });
            var paymentResult = await paymentResponse.Content.ReadFromJsonAsync<PaymentCreationResponse>();

            // ACT: Sales staff denies payment
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _salesUserToken);

            var denyResponse = await _client.PostAsJsonAsync($"/api/payments/{paymentResult.PaymentId}/confirm", new
            {
                Confirmed = false,
                Notes = "Payment not received"
            });

            // ASSERT: Payment denied, customer not in queue
            Assert.Equal(HttpStatusCode.OK, denyResponse.StatusCode);

            var queueResponse = await _client.GetAsync("/api/queue");
            var queueState = await queueResponse.Content.ReadFromJsonAsync<QueueStateResponse>();
            Assert.DoesNotContain(queueState.Entries, q => q.CustomerId == customerResult.CustomerId);

            // Verify denial notification sent
            Assert.Contains(_mockSignalR.SentNotifications, n =>
                n.Type == "PaymentDenied" && n.Data.Contains(customerResult.CustomerId.ToString()));
        }

        /// <summary>
        /// Test Scenario 5: Multiple payment attempts - only one pending allowed
        /// </summary>
        [Fact]
        public async Task MultiplePaymentAttempts_OnlyOnePendingAllowed_SecondAttemptRejected()
        {
            // ARRANGE: Customer with existing pending payment
            var customerResponse = await _client.PostAsJsonAsync("/api/customers",
                new { Name = "Multiple Payment Customer" });
            var customerResult = await customerResponse.Content.ReadFromJsonAsync<CustomerCreationResponse>();

            // Create first payment
            var firstPaymentResponse = await _client.PostAsJsonAsync("/api/payments", new
            {
                CustomerId = customerResult.CustomerId,
                Amount = 25.00m,
                PaymentMethod = PaymentMethod.CashApp
            });
            Assert.Equal(HttpStatusCode.Created, firstPaymentResponse.StatusCode);

            // ACT: Attempt second payment while first is pending
            var secondPaymentResponse = await _client.PostAsJsonAsync("/api/payments", new
            {
                CustomerId = customerResult.CustomerId,
                Amount = 25.00m,
                PaymentMethod = PaymentMethod.PayPal
            });

            // ASSERT: Second payment rejected due to existing pending payment
            Assert.Equal(HttpStatusCode.Conflict, secondPaymentResponse.StatusCode);
            var errorResponse = await secondPaymentResponse.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.Contains("pending payment", errorResponse.Message.ToLower());
        }

        /// <summary>
        /// Test Scenario 6: Manual customer addition for payment failure fallback
        /// </summary>
        [Fact]
        public async Task ManualCustomerAddition_PaymentFailureFallback_CustomerAddedWithReason()
        {
            // ARRANGE: Sales staff authentication
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _salesUserToken);

            // ACT: Sales manually adds customer (payment method failure scenario)
            var manualAddResponse = await _client.PostAsJsonAsync("/api/customers/manual", new
            {
                Name = "Manual Add Customer",
                PhoneNumber = "+1555123456",
                Reason = "CashApp payment method unavailable",
                StaffId = "testsales"
            });

            // ASSERT: Customer manually added and appears in queue
            Assert.Equal(HttpStatusCode.Created, manualAddResponse.StatusCode);
            var manualResult = await manualAddResponse.Content.ReadFromJsonAsync<ManualCustomerResponse>();
            Assert.NotNull(manualResult);

            // Verify customer in queue without payment
            var queueResponse = await _client.GetAsync("/api/queue");
            var queueState = await queueResponse.Content.ReadFromJsonAsync<QueueStateResponse>();
            Assert.Contains(queueState.Entries, q => q.CustomerId == manualResult.CustomerId);

            // Verify audit trail for manual addition
            Assert.Contains(_mockSignalR.SentNotifications, n =>
                n.Type == "ManualCustomerAdded" && n.Data.Contains("CashApp payment method unavailable"));
        }

        #endregion

        #region Real-time Notification Tests

        /// <summary>
        /// Test Scenario 7: Real-time SignalR notifications during payment flow
        /// </summary>
        [Fact]
        public async Task RealTimeNotifications_PaymentFlow_AllStakeholdersNotified()
        {
            // ARRANGE: Customer payment setup
            var customerResponse = await _client.PostAsJsonAsync("/api/customers",
                new { Name = "Notification Test Customer" });
            var customerResult = await customerResponse.Content.ReadFromJsonAsync<CustomerCreationResponse>();

            var paymentResponse = await _client.PostAsJsonAsync("/api/payments", new
            {
                CustomerId = customerResult.CustomerId,
                Amount = 25.00m,
                PaymentMethod = PaymentMethod.CashApp
            });
            var paymentResult = await paymentResponse.Content.ReadFromJsonAsync<PaymentCreationResponse>();

            // Clear previous notifications for clean test
            _mockSignalR.SentNotifications.Clear();

            // ACT: Sales confirms payment
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _salesUserToken);

            await _client.PostAsJsonAsync($"/api/payments/{paymentResult.PaymentId}/confirm", new
            {
                Confirmed = true,
                Notes = "Payment verified"
            });

            // ASSERT: Verify all required notifications sent
            var notifications = _mockSignalR.SentNotifications;

            // Customer notification: Payment confirmed
            Assert.Contains(notifications, n =>
                n.Type == "PaymentConfirmed" &&
                n.TargetGroup == "Customer" &&
                n.Data.Contains(customerResult.CustomerId.ToString()));

            // Sales notification: Queue updated
            Assert.Contains(notifications, n =>
                n.Type == "QueueUpdate" &&
                n.TargetGroup == "Sales" &&
                n.Data.Contains("CustomerAdded"));

            // Driver notification: New customer in queue
            Assert.Contains(notifications, n =>
                n.Type == "QueueUpdate" &&
                n.TargetGroup == "Driver" &&
                n.Data.Contains(customerResult.CustomerId.ToString()));

            // System notification: Payment flow completed
            Assert.Contains(notifications, n =>
                n.Type == "SystemMessage" &&
                n.Data.Contains("Payment flow completed"));
        }

        #endregion

        #region Test Support Classes

        /// <summary>
        /// Mock SignalR service for testing real-time notifications
        /// </summary>
        public class MockSignalRService : INotificationService
        {
            public List<NotificationRecord> SentNotifications { get; } = new List<NotificationRecord>();

            public async Task NotifyQueueUpdateAsync(IEnumerable<QueueEntry> queueEntries)
            {
                SentNotifications.Add(new NotificationRecord
                {
                    Type = "QueueUpdate",
                    TargetGroup = "All",
                    Data = JsonConvert.SerializeObject(queueEntries),
                    Timestamp = DateTime.UtcNow
                });
                await Task.CompletedTask;
            }

            public async Task NotifyPaymentStatusAsync(Payment payment)
            {
                var notificationType = payment.Status switch
                {
                    PaymentStatus.Confirmed => "PaymentConfirmed",
                    PaymentStatus.Denied => "PaymentDenied",
                    _ => "PaymentStatusUpdate"
                };

                SentNotifications.Add(new NotificationRecord
                {
                    Type = notificationType,
                    TargetGroup = "Customer",
                    Data = JsonConvert.SerializeObject(payment),
                    Timestamp = DateTime.UtcNow
                });
                await Task.CompletedTask;
            }

            public async Task NotifyRideStatusAsync(QueueEntry queueEntry)
            {
                SentNotifications.Add(new NotificationRecord
                {
                    Type = "RideStatusUpdate",
                    TargetGroup = "Driver",
                    Data = JsonConvert.SerializeObject(queueEntry),
                    Timestamp = DateTime.UtcNow
                });
                await Task.CompletedTask;
            }

            public async Task NotifySystemMessageAsync(string message, UserRole? targetRole = null, NotificationPriority priority = NotificationPriority.Info)
            {
                SentNotifications.Add(new NotificationRecord
                {
                    Type = "SystemMessage",
                    TargetGroup = targetRole?.ToString() ?? "All",
                    Data = message,
                    Priority = priority,
                    Timestamp = DateTime.UtcNow
                });
                await Task.CompletedTask;
            }

            public async Task NotifyCustomerAttentionAsync(Customer customer, string reason)
            {
                SentNotifications.Add(new NotificationRecord
                {
                    Type = "CustomerAttentionRequired",
                    TargetGroup = "Sales",
                    Data = $"Customer: {customer.Name}, Reason: {reason}",
                    Timestamp = DateTime.UtcNow
                });
                await Task.CompletedTask;
            }

            public async Task NotifyDriverQueueUpdateAsync(Customer? nextCustomer, int queueLength)
            {
                SentNotifications.Add(new NotificationRecord
                {
                    Type = "DriverQueueUpdate",
                    TargetGroup = "Driver",
                    Data = JsonConvert.SerializeObject(new { NextCustomer = nextCustomer, QueueLength = queueLength }),
                    Timestamp = DateTime.UtcNow
                });
                await Task.CompletedTask;
            }

            public async Task SendUserNotificationAsync(Guid userId, string message, string type, object? data = null)
            {
                SentNotifications.Add(new NotificationRecord
                {
                    Type = type,
                    TargetGroup = $"User-{userId}",
                    Data = JsonConvert.SerializeObject(new { Message = message, Data = data }),
                    Timestamp = DateTime.UtcNow
                });
                await Task.CompletedTask;
            }

            public async Task NotifyConfigurationChangeAsync(string configType, object configData)
            {
                SentNotifications.Add(new NotificationRecord
                {
                    Type = "ConfigurationChanged",
                    TargetGroup = "Sales",
                    Data = JsonConvert.SerializeObject(new { ConfigType = configType, Data = configData }),
                    Timestamp = DateTime.UtcNow
                });
                await Task.CompletedTask;
            }
        }

        public class NotificationRecord
        {
            public string Type { get; set; } = string.Empty;
            public string TargetGroup { get; set; } = string.Empty;
            public string Data { get; set; } = string.Empty;
            public NotificationPriority Priority { get; set; } = NotificationPriority.Info;
            public DateTime Timestamp { get; set; }
        }

        // Response DTOs for test assertions (these will need to match actual API responses)
        public class CustomerCreationResponse
        {
            public Guid CustomerId { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }

        public class PaymentCreationResponse
        {
            public Guid PaymentId { get; set; }
            public Guid CustomerId { get; set; }
            public decimal Amount { get; set; }
            public PaymentMethod PaymentMethod { get; set; }
            public PaymentStatus Status { get; set; }
        }

        public class PaymentMethodsResponse
        {
            public List<PaymentMethodConfig> PaymentMethods { get; set; } = new();
        }

        public class PaymentMethodConfig
        {
            public PaymentMethod PaymentMethod { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public string PaymentUrl { get; set; } = string.Empty;
            public decimal PricePerRide { get; set; }
            public bool IsEnabled { get; set; }
        }

        public class PendingPaymentsResponse
        {
            public List<PaymentSummary> Payments { get; set; } = new();
        }

        public class PaymentSummary
        {
            public Guid Id { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public PaymentMethod PaymentMethod { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public class PaymentDetailsResponse
        {
            public Guid Id { get; set; }
            public PaymentMethod PaymentMethod { get; set; }
            public string? ExternalTransactionId { get; set; }
            public PaymentStatus Status { get; set; }
        }

        public class QueueStateResponse
        {
            public List<QueueEntryInfo> Entries { get; set; } = new();
        }

        public class QueueEntryInfo
        {
            public Guid Id { get; set; }
            public Guid CustomerId { get; set; }
            public Guid? PaymentId { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public int Position { get; set; }
            public QueueEntryStatus Status { get; set; }
        }

        public class ManualCustomerResponse
        {
            public Guid CustomerId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public string AddedBy { get; set; } = string.Empty;
        }

        public class ErrorResponse
        {
            public string Message { get; set; } = string.Empty;
            public string? ErrorCode { get; set; }
            public Dictionary<string, string[]>? ValidationErrors { get; set; }
        }

        #endregion
    }
}