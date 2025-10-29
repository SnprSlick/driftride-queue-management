using System.Text;
using DriftRide.Api.Middleware;
using DriftRide.Data;
using DriftRide.Services;
using DriftRide.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with configuration from appsettings
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "DriftRide.Api"));

// Add services to the container.
// Configure Entity Framework
builder.Services.AddDbContext<DriftRideDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Configure SignalR authentication
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/queueHub", StringComparison.OrdinalIgnoreCase))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

// Configure Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SalesOnly", policy => policy.RequireRole("Sales"));
    options.AddPolicy("DriverOnly", policy => policy.RequireRole("Driver"));
    options.AddPolicy("SalesOrDriver", policy => policy.RequireRole("Sales", "Driver"));
});

// Register services with appropriate lifetimes
// Scoped services (per HTTP request lifecycle)
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IQueueService, QueueService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();

// Singleton for notification service (SignalR hub context is singleton)
builder.Services.AddSingleton<INotificationService, NotificationService>();

// Register database seeding service
builder.Services.AddDbSeeder();

// Configure SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    // 5-second real-time sync requirement - keep connection alive more frequently
    options.KeepAliveInterval = TimeSpan.FromSeconds(5);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(15);
    // Optimize for real-time performance
    options.MaximumReceiveMessageSize = 64 * 1024; // 64KB limit for notifications
    options.StreamBufferCapacity = 10;
});

// Configure Controllers with JSON options
builder.Services.AddControllers(options =>
{
    // Configure model binding and validation
    options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => "The field is required.");
    options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(_ => "The value '{0}' is invalid.");
    options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(_ => "The field must be a number.");

    // Suppress automatic model state validation to use our custom validation responses
    options.SuppressAsyncSuffixInActionNames = false;
})
.ConfigureApiBehaviorOptions(options =>
{
    // Customize automatic validation response to match our ApiResponse format
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
            );

        var response = new DriftRide.Models.ApiResponse<object>
        {
            Success = false,
            Message = "Validation failed",
            Data = null,
            Error = new DriftRide.Models.ErrorResponse
            {
                Code = "VALIDATION_FAILED",
                Message = "One or more validation errors occurred",
                Details = errors
            }
        };

        return new BadRequestObjectResult(response);
    };
})
.AddJsonOptions(options =>
{
    // Configure JSON serialization options
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

    // Handle enums as strings for better API usability
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Configure CORS
var corsSettings = builder.Configuration.GetSection("CorsSettings");
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = corsSettings.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        var allowedMethods = corsSettings.GetSection("AllowedMethods").Get<string[]>() ?? Array.Empty<string>();
        var allowedHeaders = corsSettings.GetSection("AllowedHeaders").Get<string[]>() ?? Array.Empty<string>();
        var allowCredentials = corsSettings.GetValue<bool>("AllowCredentials");

        policy.WithOrigins(allowedOrigins)
              .WithMethods(allowedMethods)
              .WithHeaders(allowedHeaders);

        if (allowCredentials)
        {
            policy.AllowCredentials();
        }
    });
});

// Configure Security Settings
// Temporarily disabled for minimal build
// builder.Services.Configure<SecuritySettings>(builder.Configuration.GetSection("SecuritySettings"));
// builder.Services.Configure<RateLimitSettings>(builder.Configuration.GetSection("RateLimitSettings"));
// builder.Services.Configure<InputValidationSettings>(builder.Configuration.GetSection("InputValidationSettings"));

// Configure Swagger/OpenAPI with enhanced JWT support and documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // API Information
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DriftRide API",
        Version = "v1.0.0",
        Description = @"
# DriftRide Queue Management System API

The DriftRide API provides comprehensive queue management functionality with real-time updates, authentication, and role-based access control.

## Features
- JWT Bearer Token authentication
- Real-time queue updates via SignalR
- Role-based authorization (Sales, Driver)
- Comprehensive error handling
- Standard response format across all endpoints

## Authentication
All endpoints (except login) require a valid JWT Bearer token. Include the token in the Authorization header:
```
Authorization: Bearer <your-jwt-token>
```

## Response Format
All API responses follow a consistent format:
```json
{
  ""success"": true|false,
  ""message"": ""Description of the operation"",
  ""data"": { /* Response data */ },
  ""error"": { /* Error details if success=false */ }
}
```
",
        Contact = new OpenApiContact
        {
            Name = "DriftRide Support",
            Email = "support@driftride.com"
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Enable XML documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    // Configure JWT Bearer authentication scheme
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = @"
JWT Authorization header using the Bearer scheme.

**How to use:**
1. Login via `/api/auth/login` to get your JWT token
2. Click the 'Authorize' button below
3. Enter: `Bearer <your-jwt-token>`
4. Click 'Authorize' to apply the token to all requests

**Example:**
```
Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```
",
        Name = "Authorization",
        In = ParameterLocation.Header
    });

    // Add security requirement (will be applied automatically to endpoints that need it)
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Configure schema generation
    options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
    options.SupportNonNullableReferenceTypes();
    options.UseInlineDefinitionsForEnums();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "swagger/{documentName}/swagger.json";
    });

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DriftRide API v1.0.0");
        options.RoutePrefix = "swagger"; // Serve Swagger UI at /swagger
        options.DocumentTitle = "DriftRide API Documentation";

        // Enhanced UI configuration
        options.DisplayRequestDuration();
        options.EnableTryItOutByDefault();
        options.ShowExtensions();
        options.EnableValidator();
        // Submit methods configured via default settings

        // Configure OAuth2/JWT authorization UI
        options.OAuthAppName("DriftRide API");
        options.EnablePersistAuthorization();

        // Custom CSS for better styling
        options.InjectStylesheet("/swagger-ui/custom.css");

        // Configure for better developer experience
        options.DefaultModelExpandDepth(2);
        options.DefaultModelsExpandDepth(1);
        // Doc expansion set to default
        options.EnableDeepLinking();
        options.ShowCommonExtensions();

        // Custom JavaScript for enhanced functionality
        options.InjectJavascript("/swagger-ui/custom.js");
    });
}

// Security middleware pipeline (order is critical)
// 1. Request logging (first to capture all requests)
app.UseRequestLogging();

// 2. Security headers (first to apply to all responses)
// Temporarily disabled for minimal build
// app.UseSecurityHeaders();

// 3. HTTPS redirection
app.UseHttpsRedirection();

// 4. Input validation (before rate limiting to block malicious requests early)
// Temporarily disabled for minimal build
// app.UseInputValidation();

// 5. Rate limiting (after input validation, before expensive operations)
// Temporarily disabled for minimal build
// app.UseRateLimiting();

// 6. CORS (must be before authentication)
app.UseCors();

// 7. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 8. Custom JWT middleware (after authentication)
// Temporarily disabled for minimal build
// app.UseJwtMiddleware();

// 8. Map controllers
app.MapControllers();

// 9. Map SignalR hubs
app.MapHub<QueueHub>("/queueHub");

// Seed database in development environment
// Temporarily disabled for minimal build
// if (app.Environment.IsDevelopment())
// {
//     try
//     {
//         await app.Services.SeedDatabaseAsync();
//     }
//     catch (Exception ex)
//     {
//         var logger = app.Services.GetRequiredService<ILogger<Program>>();
//         logger.LogError(ex, "An error occurred while seeding the database");
//         // Continue startup even if seeding fails in development
//     }
// }

try
{
    Log.Information("Starting DriftRide API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
