using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace DriftRide.Api.Middleware;

/// <summary>
/// Middleware for request/response logging, correlation IDs, and performance monitoring.
/// Enriches log context with correlation information and tracks request performance.
/// </summary>
public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the LoggingMiddleware.
    /// </summary>
    /// <param name="next">Next middleware delegate</param>
    /// <param name="logger">Logger instance</param>
    public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invokes the middleware to process the request.
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Generate correlation ID if not present
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("D");

        // Add correlation ID to response headers
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        // Enrich log context with correlation information
        using var correlationScope = LogContext.PushProperty("CorrelationId", correlationId);
        using var userScope = LogContext.PushProperty("UserId", GetUserId(context));
        using var userRoleScope = LogContext.PushProperty("UserRole", GetUserRole(context));
        using var requestIdScope = LogContext.PushProperty("RequestId", context.TraceIdentifier);
        using var requestPathScope = LogContext.PushProperty("RequestPath", context.Request.Path);
        using var requestMethodScope = LogContext.PushProperty("RequestMethod", context.Request.Method);

        var stopwatch = Stopwatch.StartNew();
        var originalBodyStream = context.Response.Body;

        try
        {
            // Log request start
            _logger.LogInformation("Request started: {Method} {Path} from {RemoteIpAddress}",
                context.Request.Method,
                context.Request.Path,
                GetClientIpAddress(context));

            // Log request details for API endpoints
            if (IsApiRequest(context.Request.Path))
            {
                await LogApiRequestAsync(context);
            }

            // Continue processing
            await _next(context);

            stopwatch.Stop();

            // Log request completion
            _logger.LogInformation("Request completed: {Method} {Path} - {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);

            // Log performance warnings for slow requests
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                _logger.LogWarning("Slow request detected: {Method} {Path} took {ElapsedMs}ms",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds);
            }

            // Log API response for debugging
            if (IsApiRequest(context.Request.Path) && ShouldLogApiResponse(context))
            {
                LogApiResponse(context, stopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Request failed: {Method} {Path} after {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    /// <summary>
    /// Logs API request details for debugging and audit purposes.
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Task representing the asynchronous operation</returns>
    private async Task LogApiRequestAsync(HttpContext context)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
            return;

        var request = context.Request;

        // Log query parameters
        if (request.Query.Any())
        {
            var queryParams = request.Query
                .Where(q => !IsSensitiveParameter(q.Key))
                .ToDictionary(q => q.Key, q => q.Value.ToString());

            _logger.LogDebug("Request query parameters: {@QueryParameters}", queryParams);
        }

        // Log request headers (excluding sensitive ones)
        var safeHeaders = request.Headers
            .Where(h => !IsSensitiveHeader(h.Key))
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        _logger.LogDebug("Request headers: {@Headers}", safeHeaders);

        // Log request body for POST/PUT/PATCH requests (if JSON and not too large)
        if (ShouldLogRequestBody(request))
        {
            await LogRequestBodyAsync(context);
        }
    }

    /// <summary>
    /// Logs request body for API debugging.
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Task representing the asynchronous operation</returns>
    private async Task LogRequestBodyAsync(HttpContext context)
    {
        var request = context.Request;

        if (request.ContentLength > 10240) // Skip bodies larger than 10KB
            return;

        try
        {
            request.EnableBuffering();
            var body = await new StreamReader(request.Body).ReadToEndAsync();
            request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body))
            {
                // Mask sensitive fields in the body
                var maskedBody = MaskSensitiveDataInJson(body);
                _logger.LogDebug("Request body: {RequestBody}", maskedBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log request body");
        }
    }

    /// <summary>
    /// Logs API response details for debugging.
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="elapsedMs">Request processing time in milliseconds</param>
    private void LogApiResponse(HttpContext context, long elapsedMs)
    {
        var response = context.Response;

        _logger.LogDebug("API Response: {StatusCode} {ContentType} ({ContentLength} bytes) in {ElapsedMs}ms",
            response.StatusCode,
            response.ContentType ?? "unknown",
            response.ContentLength ?? 0,
            elapsedMs);

        // Log response headers (excluding sensitive ones)
        var safeHeaders = response.Headers
            .Where(h => !IsSensitiveHeader(h.Key))
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        if (safeHeaders.Any())
        {
            _logger.LogDebug("Response headers: {@Headers}", safeHeaders);
        }
    }

    /// <summary>
    /// Extracts user ID from the HTTP context.
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>User ID or "Anonymous" if not authenticated</returns>
    private static string GetUserId(HttpContext context)
    {
        var user = context.User;
        return user?.Identity?.IsAuthenticated == true
            ? user.FindFirst("sub")?.Value ?? user.FindFirst("id")?.Value ?? "Unknown"
            : "Anonymous";
    }

    /// <summary>
    /// Extracts user role from the HTTP context.
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>User role or "Anonymous" if not authenticated</returns>
    private static string GetUserRole(HttpContext context)
    {
        var user = context.User;
        return user?.Identity?.IsAuthenticated == true
            ? user.FindFirst("role")?.Value ?? "User"
            : "Anonymous";
    }

    /// <summary>
    /// Gets the client IP address from the HTTP context.
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Client IP address</returns>
    private static string GetClientIpAddress(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Determines if the request is an API request.
    /// </summary>
    /// <param name="path">Request path</param>
    /// <returns>True if API request, false otherwise</returns>
    private static bool IsApiRequest(PathString path)
    {
        return path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if request body should be logged.
    /// </summary>
    /// <param name="request">HTTP request</param>
    /// <returns>True if should log body, false otherwise</returns>
    private static bool ShouldLogRequestBody(HttpRequest request)
    {
        return (request.Method == "POST" || request.Method == "PUT" || request.Method == "PATCH") &&
               request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Determines if API response should be logged.
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>True if should log response, false otherwise</returns>
    private static bool ShouldLogApiResponse(HttpContext context)
    {
        // Log responses for errors or when debug logging is enabled
        return context.Response.StatusCode >= 400;
    }

    /// <summary>
    /// Checks if a header is sensitive and should not be logged.
    /// </summary>
    /// <param name="headerName">Header name</param>
    /// <returns>True if header is sensitive, false otherwise</returns>
    private static bool IsSensitiveHeader(string headerName)
    {
        var sensitiveHeaders = new[]
        {
            "Authorization", "Cookie", "Set-Cookie", "X-API-Key", "X-Auth-Token"
        };

        return sensitiveHeaders.Contains(headerName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a query parameter is sensitive and should not be logged.
    /// </summary>
    /// <param name="paramName">Parameter name</param>
    /// <returns>True if parameter is sensitive, false otherwise</returns>
    private static bool IsSensitiveParameter(string paramName)
    {
        var sensitiveParams = new[]
        {
            "password", "token", "apikey", "secret", "authorization"
        };

        return sensitiveParams.Any(p => paramName.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Masks sensitive data in JSON content.
    /// </summary>
    /// <param name="json">JSON content</param>
    /// <returns>JSON with sensitive fields masked</returns>
    private static string MaskSensitiveDataInJson(string json)
    {
        // Simple regex-based masking for common sensitive fields
        var sensitiveFields = new[]
        {
            "password", "token", "apikey", "secret", "authorization", "externalTransactionId"
        };

        var maskedJson = json;
        foreach (var field in sensitiveFields)
        {
            var pattern = $@"""{field}"":\s*""[^""]*""";
            maskedJson = System.Text.RegularExpressions.Regex.Replace(
                maskedJson,
                pattern,
                $@"""{field}"": ""***MASKED***""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return maskedJson;
    }
}

/// <summary>
/// Extension methods for registering the logging middleware.
/// </summary>
public static class LoggingMiddlewareExtensions
{
    /// <summary>
    /// Adds the logging middleware to the application pipeline.
    /// </summary>
    /// <param name="builder">Application builder</param>
    /// <returns>Application builder for chaining</returns>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<LoggingMiddleware>();
    }
}