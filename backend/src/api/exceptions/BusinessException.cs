namespace DriftRide.Api.Exceptions;

/// <summary>
/// Base exception class for business rule violations and domain logic errors.
/// Provides a foundation for handling business-specific exceptions with appropriate HTTP status codes.
/// </summary>
public abstract class BusinessException : Exception
{
    /// <summary>
    /// Gets the HTTP status code that should be returned for this exception.
    /// </summary>
    public abstract int StatusCode { get; }

    /// <summary>
    /// Gets the error code that identifies the specific business rule violation.
    /// </summary>
    public abstract string ErrorCode { get; }

    /// <summary>
    /// Gets additional details about the error for debugging or user guidance.
    /// </summary>
    public virtual object? Details { get; }

    /// <summary>
    /// Initializes a new instance of the BusinessException class.
    /// </summary>
    /// <param name="message">The error message</param>
    protected BusinessException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the BusinessException class with details.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="details">Additional error details</param>
    protected BusinessException(string message, object? details) : base(message)
    {
        Details = details;
    }

    /// <summary>
    /// Initializes a new instance of the BusinessException class with an inner exception.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="innerException">The inner exception</param>
    protected BusinessException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the BusinessException class with details and an inner exception.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="details">Additional error details</param>
    /// <param name="innerException">The inner exception</param>
    protected BusinessException(string message, object? details, Exception innerException) : base(message, innerException)
    {
        Details = details;
    }
}