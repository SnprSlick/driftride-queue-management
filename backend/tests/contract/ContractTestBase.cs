using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DriftRide.Api.Data;
using DriftRide.Api.Models;

namespace DriftRide.Contract.Tests;

/// <summary>
/// Base class for contract tests providing WebApplicationFactory setup,
/// JWT token generation, and HTTP client configuration.
/// </summary>
public abstract class ContractTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions;

    protected ContractTestBase(WebApplicationFactory<Program> factory)
    {
        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Remove the app's DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<DriftRideDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add a database context using an in-memory database for testing
                services.AddDbContext<DriftRideDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting");
                });

                // Ensure the database is created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DriftRideDbContext>();
                context.Database.EnsureCreated();
            });
        });

        Client = Factory.CreateClient();
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Generates a JWT token for testing authentication
    /// </summary>
    protected string GenerateJwtToken(string username = "testuser", string role = "Sales", string userId = "00000000-0000-0000-0000-000000000001")
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes("test-secret-key-that-is-long-enough-for-jwt-signing-requirements");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, role),
            new("display_name", $"Test {role}"),
            new("jti", Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = "DriftRide.TestServer",
            Audience = "DriftRide.Api"
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Sets the Authorization header with a Bearer token
    /// </summary>
    protected void SetAuthorizationHeader(string? token = null, string role = "Sales")
    {
        token ??= GenerateJwtToken(role: role);
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Removes the Authorization header
    /// </summary>
    protected void ClearAuthorizationHeader()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Serializes an object to JSON
    /// </summary>
    protected string ToJson<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, JsonOptions);
    }

    /// <summary>
    /// Deserializes JSON to an object
    /// </summary>
    protected T? FromJson<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    /// <summary>
    /// Gets a fresh database context for testing
    /// </summary>
    protected DriftRideDbContext GetDbContext()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DriftRideDbContext>();
    }

    /// <summary>
    /// Creates test data in the database
    /// </summary>
    protected async Task SeedTestDataAsync()
    {
        using var context = GetDbContext();

        // Clear existing data
        context.QueueEntries.RemoveRange(context.QueueEntries);
        context.Payments.RemoveRange(context.Payments);
        context.Customers.RemoveRange(context.Customers);
        context.Users.RemoveRange(context.Users);
        context.PaymentConfigurations.RemoveRange(context.PaymentConfigurations);

        // Add test users
        var salesUser = new User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Username = "testuser",
            DisplayName = "Test Sales",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            Role = UserRole.Sales,
            Email = "sales@test.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var driverUser = new User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Username = "driver",
            DisplayName = "Test Driver",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            Role = UserRole.Driver,
            Email = "driver@test.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(salesUser, driverUser);

        // Add payment configurations
        var paymentConfigs = new[]
        {
            new PaymentConfiguration
            {
                Id = 1,
                PaymentMethod = PaymentMethod.CashApp,
                DisplayName = "CashApp",
                PaymentUrl = "https://cash.app/$driftride",
                IsEnabled = true,
                PricePerRide = 25.00m,
                ApiIntegrationEnabled = false
            },
            new PaymentConfiguration
            {
                Id = 2,
                PaymentMethod = PaymentMethod.PayPal,
                DisplayName = "PayPal",
                PaymentUrl = "https://paypal.me/driftride",
                IsEnabled = true,
                PricePerRide = 25.00m,
                ApiIntegrationEnabled = false
            },
            new PaymentConfiguration
            {
                Id = 3,
                PaymentMethod = PaymentMethod.CashInHand,
                DisplayName = "Cash in Hand",
                PaymentUrl = null,
                IsEnabled = true,
                PricePerRide = 25.00m,
                ApiIntegrationEnabled = false
            }
        };

        context.PaymentConfigurations.AddRange(paymentConfigs);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Standard test response validation helper
    /// </summary>
    protected async Task<T> ValidateSuccessResponse<T>(HttpResponseMessage response, int expectedStatusCode = 200)
    {
        Assert.Equal(expectedStatusCode, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);

        var apiResponse = FromJson<ApiResponse<T>>(content);
        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);

        return apiResponse.Data;
    }

    /// <summary>
    /// Standard error response validation helper
    /// </summary>
    protected async Task<ErrorResponse> ValidateErrorResponse(HttpResponseMessage response, int expectedStatusCode, string? expectedErrorCode = null)
    {
        Assert.Equal(expectedStatusCode, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);

        var apiResponse = FromJson<ApiResponse<object>>(content);
        Assert.NotNull(apiResponse);
        Assert.False(apiResponse.Success);
        Assert.NotNull(apiResponse.Error);

        if (expectedErrorCode != null)
        {
            Assert.Equal(expectedErrorCode, apiResponse.Error.Code);
        }

        return apiResponse.Error;
    }
}