namespace DriftRide.Api.Exceptions;

/// <summary>
/// Exception thrown when authorization failures occur.
/// </summary>
public class AuthorizationException : BusinessException
{
    /// <summary>
    /// Gets the HTTP status code (403 Forbidden).
    /// </summary>
    public override int StatusCode => 403;

    /// <summary>
    /// Gets the error code for authorization failures.
    /// </summary>
    public override string ErrorCode => "AUTHORIZATION_FAILED";

    /// <summary>
    /// Gets the required role for the operation.
    /// </summary>
    public string? RequiredRole { get; }

    /// <summary>
    /// Gets the user's current role.
    /// </summary>
    public string? CurrentRole { get; }

    /// <summary>
    /// Initializes a new instance of the AuthorizationException class.
    /// </summary>
    /// <param name="message">The error message</param>
    public AuthorizationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the AuthorizationException class with role information.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="requiredRole">The required role for the operation</param>
    /// <param name="currentRole">The user's current role</param>
    public AuthorizationException(string message, string requiredRole, string? currentRole)
        : base(message)
    {
        RequiredRole = requiredRole;
        CurrentRole = currentRole;
    }
}

/// <summary>
/// Exception thrown when authentication failures occur.
/// </summary>
public class AuthenticationException : BusinessException
{
    /// <summary>
    /// Gets the HTTP status code (401 Unauthorized).
    /// </summary>
    public override int StatusCode => 401;

    /// <summary>
    /// Gets the error code for authentication failures.
    /// </summary>
    public override string ErrorCode => "AUTHENTICATION_FAILED";

    /// <summary>
    /// Initializes a new instance of the AuthenticationException class.
    /// </summary>
    /// <param name="message">The error message</param>
    public AuthenticationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the AuthenticationException class with an inner exception.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="innerException">The inner exception</param>
    public AuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when rate limiting is exceeded.
/// </summary>
public class RateLimitExceededException : BusinessException
{
    /// <summary>
    /// Gets the HTTP status code (429 Too Many Requests).
    /// </summary>
    public override int StatusCode => 429;

    /// <summary>
    /// Gets the error code for rate limit exceeded.
    /// </summary>
    public override string ErrorCode => "RATE_LIMIT_EXCEEDED";

    /// <summary>
    /// Gets the retry after seconds.
    /// </summary>
    public int RetryAfterSeconds { get; }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class.
    /// </summary>
    /// <param name="retryAfterSeconds">Seconds to wait before retrying</param>
    public RateLimitExceededException(int retryAfterSeconds)
        : base($"Rate limit exceeded. Please retry after {retryAfterSeconds} seconds.")
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    /// <summary>
    /// Initializes a new instance of the RateLimitExceededException class with custom message.
    /// </summary>
    /// <param name="retryAfterSeconds">Seconds to wait before retrying</param>
    /// <param name="message">Custom error message</param>
    public RateLimitExceededException(int retryAfterSeconds, string message)
        : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}