namespace DriftRide.Api.Exceptions;

/// <summary>
/// Exception thrown when customer-related business rules are violated.
/// </summary>
public class CustomerBusinessException : BusinessException
{
    /// <summary>
    /// Gets the HTTP status code (400 Bad Request).
    /// </summary>
    public override int StatusCode => 400;

    /// <summary>
    /// Gets the error code for customer business violations.
    /// </summary>
    public override string ErrorCode => "CUSTOMER_BUSINESS_RULE_VIOLATION";

    /// <summary>
    /// Initializes a new instance of the CustomerBusinessException class.
    /// </summary>
    /// <param name="message">The error message</param>
    public CustomerBusinessException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the CustomerBusinessException class with details.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="details">Additional error details</param>
    public CustomerBusinessException(string message, object? details) : base(message, details)
    {
    }

    /// <summary>
    /// Initializes a new instance of the CustomerBusinessException class with an inner exception.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="innerException">The inner exception</param>
    public CustomerBusinessException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a customer is not found.
/// </summary>
public class CustomerNotFoundException : BusinessException
{
    /// <summary>
    /// Gets the HTTP status code (404 Not Found).
    /// </summary>
    public override int StatusCode => 404;

    /// <summary>
    /// Gets the error code for customer not found.
    /// </summary>
    public override string ErrorCode => "CUSTOMER_NOT_FOUND";

    /// <summary>
    /// Gets the customer identifier that was not found.
    /// </summary>
    public object CustomerId { get; }

    /// <summary>
    /// Initializes a new instance of the CustomerNotFoundException class.
    /// </summary>
    /// <param name="customerId">The customer identifier that was not found</param>
    public CustomerNotFoundException(object customerId)
        : base($"Customer with identifier '{customerId}' was not found")
    {
        CustomerId = customerId;
    }

    /// <summary>
    /// Initializes a new instance of the CustomerNotFoundException class with a custom message.
    /// </summary>
    /// <param name="customerId">The customer identifier that was not found</param>
    /// <param name="message">Custom error message</param>
    public CustomerNotFoundException(object customerId, string message)
        : base(message)
    {
        CustomerId = customerId;
    }
}

/// <summary>
/// Exception thrown when customer data validation fails.
/// </summary>
public class CustomerValidationException : BusinessException
{
    /// <summary>
    /// Gets the HTTP status code (400 Bad Request).
    /// </summary>
    public override int StatusCode => 400;

    /// <summary>
    /// Gets the error code for customer validation failures.
    /// </summary>
    public override string ErrorCode => "CUSTOMER_VALIDATION_FAILED";

    /// <summary>
    /// Initializes a new instance of the CustomerValidationException class.
    /// </summary>
    /// <param name="message">The validation error message</param>
    public CustomerValidationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the CustomerValidationException class with validation details.
    /// </summary>
    /// <param name="message">The validation error message</param>
    /// <param name="validationErrors">Dictionary of field validation errors</param>
    public CustomerValidationException(string message, Dictionary<string, string[]> validationErrors)
        : base(message, validationErrors)
    {
    }
}