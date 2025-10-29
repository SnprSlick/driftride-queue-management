using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace DriftRide.Api.Services;

/// <summary>
/// Service for enhanced error logging and monitoring with structured logging,
/// performance tracking, and error aggregation capabilities.
/// </summary>
public interface IErrorMonitoringService
{
    /// <summary>
    /// Log an error with context and correlation information.
    /// </summary>
    Task LogErrorAsync(Exception exception, string operation, object? context = null, string? correlationId = null);

    /// <summary>
    /// Log a business rule violation.
    /// </summary>
    Task LogBusinessRuleViolationAsync(string rule, string message, object? context = null, string? userId = null);

    /// <summary>
    /// Log validation failures with detailed field information.
    /// </summary>
    Task LogValidationFailureAsync(string operation, Dictionary<string, string[]> validationErrors, object? context = null);

    /// <summary>
    /// Track performance metrics for operations.
    /// </summary>
    Task TrackPerformanceAsync(string operation, TimeSpan duration, bool success, object? metadata = null);

    /// <summary>
    /// Log user activity for audit trails.
    /// </summary>
    Task LogUserActivityAsync(string userId, string action, object? details = null, string? resourceId = null);

    /// <summary>
    /// Log security events.
    /// </summary>
    Task LogSecurityEventAsync(string eventType, string message, object? context = null, string? ipAddress = null);

    /// <summary>
    /// Get error statistics for monitoring dashboard.
    /// </summary>
    Task<ErrorStatistics> GetErrorStatisticsAsync(DateTime fromDate, DateTime toDate);
}

public class ErrorMonitoringService : IErrorMonitoringService
{
    private readonly ILogger<ErrorMonitoringService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ActivitySource _activitySource;

    public ErrorMonitoringService(
        ILogger<ErrorMonitoringService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _activitySource = new ActivitySource("DriftRide.ErrorMonitoring");
    }

    /// <summary>
    /// Log an error with context and correlation information.
    /// </summary>
    public async Task LogErrorAsync(Exception exception, string operation, object? context = null, string? correlationId = null)
    {
        using var activity = _activitySource.StartActivity("LogError");
        activity?.SetTag("operation", operation);
        activity?.SetTag("correlationId", correlationId);

        var errorDetails = new
        {
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            Operation = operation,
            ExceptionType = exception.GetType().Name,
            ExceptionMessage = exception.Message,
            StackTrace = exception.StackTrace,
            InnerException = exception.InnerException?.Message,
            Context = context,
            Timestamp = DateTimeOffset.UtcNow,
            MachineName = Environment.MachineName,
            ProcessId = Environment.ProcessId,
            ThreadId = Environment.CurrentManagedThreadId
        };

        using var scope = _logger.BeginScope(errorDetails);

        _logger.LogError(exception,
            "Error occurred in operation {Operation}. CorrelationId: {CorrelationId}",
            operation, errorDetails.CorrelationId);

        // Send to external monitoring service if configured
        await SendToExternalMonitoringAsync("error", errorDetails);

        // Track error metrics
        await TrackErrorMetricsAsync(exception, operation);
    }

    /// <summary>
    /// Log a business rule violation.
    /// </summary>
    public async Task LogBusinessRuleViolationAsync(string rule, string message, object? context = null, string? userId = null)
    {
        using var activity = _activitySource.StartActivity("LogBusinessRuleViolation");
        activity?.SetTag("rule", rule);
        activity?.SetTag("userId", userId);

        var violationDetails = new
        {
            Rule = rule,
            Message = message,
            UserId = userId,
            Context = context,
            Timestamp = DateTimeOffset.UtcNow,
            Severity = "Warning"
        };

        using var scope = _logger.BeginScope(violationDetails);

        _logger.LogWarning(
            "Business rule violation: {Rule}. Message: {Message}. UserId: {UserId}",
            rule, message, userId);

        // Send to external monitoring service
        await SendToExternalMonitoringAsync("business_rule_violation", violationDetails);
    }

    /// <summary>
    /// Log validation failures with detailed field information.
    /// </summary>
    public async Task LogValidationFailureAsync(string operation, Dictionary<string, string[]> validationErrors, object? context = null)
    {
        using var activity = _activitySource.StartActivity("LogValidationFailure");
        activity?.SetTag("operation", operation);
        activity?.SetTag("errorCount", validationErrors.Count);

        var validationDetails = new
        {
            Operation = operation,
            ValidationErrors = validationErrors,
            ErrorCount = validationErrors.Count,
            FieldsWithErrors = validationErrors.Keys.ToArray(),
            Context = context,
            Timestamp = DateTimeOffset.UtcNow
        };

        using var scope = _logger.BeginScope(validationDetails);

        _logger.LogWarning(
            "Validation failure in operation {Operation}. {ErrorCount} validation errors occurred",
            operation, validationErrors.Count);

        // Log each validation error separately for better searchability
        foreach (var kvp in validationErrors)
        {
            foreach (var error in kvp.Value)
            {
                _logger.LogWarning(
                    "Validation error in field {Field}: {Error}",
                    kvp.Key, error);
            }
        }

        // Send to external monitoring service
        await SendToExternalMonitoringAsync("validation_failure", validationDetails);
    }

    /// <summary>
    /// Track performance metrics for operations.
    /// </summary>
    public async Task TrackPerformanceAsync(string operation, TimeSpan duration, bool success, object? metadata = null)
    {
        using var activity = _activitySource.StartActivity("TrackPerformance");
        activity?.SetTag("operation", operation);
        activity?.SetTag("duration", duration.TotalMilliseconds);
        activity?.SetTag("success", success);

        var performanceData = new
        {
            Operation = operation,
            DurationMs = duration.TotalMilliseconds,
            Success = success,
            Metadata = metadata,
            Timestamp = DateTimeOffset.UtcNow
        };

        var logLevel = duration.TotalSeconds > 5 ? LogLevel.Warning : LogLevel.Information;

        using var scope = _logger.BeginScope(performanceData);

        _logger.Log(logLevel,
            "Operation {Operation} completed in {DurationMs}ms. Success: {Success}",
            operation, duration.TotalMilliseconds, success);

        // Send to external monitoring service
        await SendToExternalMonitoringAsync("performance", performanceData);

        // Track slow operations
        if (duration.TotalSeconds > 5)
        {
            await LogSlowOperationAsync(operation, duration, metadata);
        }
    }

    /// <summary>
    /// Log user activity for audit trails.
    /// </summary>
    public async Task LogUserActivityAsync(string userId, string action, object? details = null, string? resourceId = null)
    {
        using var activity = _activitySource.StartActivity("LogUserActivity");
        activity?.SetTag("userId", userId);
        activity?.SetTag("action", action);
        activity?.SetTag("resourceId", resourceId);

        var activityDetails = new
        {
            UserId = userId,
            Action = action,
            ResourceId = resourceId,
            Details = details,
            Timestamp = DateTimeOffset.UtcNow,
            SessionId = Activity.Current?.Id
        };

        using var scope = _logger.BeginScope(activityDetails);

        _logger.LogInformation(
            "User activity: {UserId} performed {Action} on resource {ResourceId}",
            userId, action, resourceId);

        // Send to external monitoring service
        await SendToExternalMonitoringAsync("user_activity", activityDetails);
    }

    /// <summary>
    /// Log security events.
    /// </summary>
    public async Task LogSecurityEventAsync(string eventType, string message, object? context = null, string? ipAddress = null)
    {
        using var activity = _activitySource.StartActivity("LogSecurityEvent");
        activity?.SetTag("eventType", eventType);
        activity?.SetTag("ipAddress", ipAddress);

        var securityDetails = new
        {
            EventType = eventType,
            Message = message,
            IpAddress = ipAddress,
            Context = context,
            Timestamp = DateTimeOffset.UtcNow,
            Severity = GetSecurityEventSeverity(eventType)
        };

        using var scope = _logger.BeginScope(securityDetails);

        var logLevel = GetSecurityEventSeverity(eventType) == "Critical" ? LogLevel.Critical : LogLevel.Warning;

        _logger.Log(logLevel,
            "Security event: {EventType}. Message: {Message}. IP: {IpAddress}",
            eventType, message, ipAddress);

        // Send to external monitoring service with high priority
        await SendToExternalMonitoringAsync("security_event", securityDetails, priority: "high");
    }

    /// <summary>
    /// Get error statistics for monitoring dashboard.
    /// </summary>
    public async Task<ErrorStatistics> GetErrorStatisticsAsync(DateTime fromDate, DateTime toDate)
    {
        // In a real implementation, this would query a database or monitoring service
        // For now, return mock data structure
        await Task.Delay(1); // Simulate async operation

        return new ErrorStatistics
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalErrors = 0,
            ErrorsByType = new Dictionary<string, int>(),
            ErrorsByOperation = new Dictionary<string, int>(),
            AvgErrorsPerHour = 0,
            TopErrors = new List<ErrorSummary>()
        };
    }

    /// <summary>
    /// Send data to external monitoring service.
    /// </summary>
    private async Task SendToExternalMonitoringAsync(string eventType, object data, string priority = "normal")
    {
        try
        {
            var monitoringEnabled = _configuration.GetValue<bool>("Monitoring:ExternalService:Enabled", false);
            if (!monitoringEnabled) return;

            var endpoint = _configuration.GetValue<string>("Monitoring:ExternalService:Endpoint");
            var apiKey = _configuration.GetValue<string>("Monitoring:ExternalService:ApiKey");

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey)) return;

            var payload = new
            {
                EventType = eventType,
                Priority = priority,
                Data = data,
                Source = "DriftRide.Api",
                Environment = _configuration.GetValue<string>("Environment", "Unknown")
            };

            // Implementation would send to external service
            // For now, just log that we would send it
            _logger.LogDebug("Would send {EventType} event to external monitoring service", eventType);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            // Never let monitoring errors break the application
            _logger.LogWarning(ex, "Failed to send data to external monitoring service");
        }
    }

    /// <summary>
    /// Track error metrics for aggregation.
    /// </summary>
    private async Task TrackErrorMetricsAsync(Exception exception, string operation)
    {
        try
        {
            // Implementation would update metrics storage
            // For now, just log the metric
            _logger.LogDebug("Tracking error metric for {Operation}: {ExceptionType}", operation, exception.GetType().Name);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to track error metrics");
        }
    }

    /// <summary>
    /// Log slow operations for performance monitoring.
    /// </summary>
    private async Task LogSlowOperationAsync(string operation, TimeSpan duration, object? metadata)
    {
        var slowOpDetails = new
        {
            Operation = operation,
            DurationSeconds = duration.TotalSeconds,
            Metadata = metadata,
            Timestamp = DateTimeOffset.UtcNow,
            IsSlowOperation = true
        };

        _logger.LogWarning(
            "Slow operation detected: {Operation} took {DurationSeconds} seconds",
            operation, duration.TotalSeconds);

        await SendToExternalMonitoringAsync("slow_operation", slowOpDetails, priority: "high");
    }

    /// <summary>
    /// Get severity level for security events.
    /// </summary>
    private static string GetSecurityEventSeverity(string eventType)
    {
        return eventType.ToUpperInvariant() switch
        {
            "UNAUTHORIZED_ACCESS" => "Critical",
            "BRUTE_FORCE_ATTEMPT" => "Critical",
            "SUSPICIOUS_ACTIVITY" => "High",
            "RATE_LIMIT_EXCEEDED" => "Medium",
            "INVALID_TOKEN" => "Medium",
            _ => "Low"
        };
    }
}

/// <summary>
/// Error statistics for monitoring dashboard.
/// </summary>
public class ErrorStatistics
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalErrors { get; set; }
    public Dictionary<string, int> ErrorsByType { get; set; } = new();
    public Dictionary<string, int> ErrorsByOperation { get; set; } = new();
    public double AvgErrorsPerHour { get; set; }
    public List<ErrorSummary> TopErrors { get; set; } = new();
}

/// <summary>
/// Summary of specific error types.
/// </summary>
public class ErrorSummary
{
    public string ErrorType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTime LastOccurrence { get; set; }
}