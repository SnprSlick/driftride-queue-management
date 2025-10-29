namespace DriftRide.Api.Exceptions;

/// <summary>
/// Exception thrown when payment-related business rules are violated.
/// </summary>
public class PaymentBusinessException : BusinessException
{
    /// <summary>
    /// Gets the HTTP status code (400 Bad Request).
    /// </summary>
    public override int StatusCode => 400;

    /// <summary>
    /// Gets the error code for payment business violations.
    /// </summary>
    public override string ErrorCode => "PAYMENT_BUSINESS_RULE_VIOLATION";

    /// <summary>
    /// Initializes a new instance of the PaymentBusinessException class.
    /// </summary>
    /// <param name="message">The error message</param>
    public PaymentBusinessException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the PaymentBusinessException class with details.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="details">Additional error details</param>
    public PaymentBusinessException(string message, object? details) : base(message, details)
    {
    }

    /// <summary>
    /// Initializes a new instance of the PaymentBusinessException class with an inner exception.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="innerException">The inner exception</param>
    public PaymentBusinessException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a payment is not found.
/// </summary>
public class PaymentNotFoundException : BusinessException
{
    /// <summary>
    /// Gets the HTTP status code (404 Not Found).
    /// </summary>
    public override int StatusCode => 404;

    /// <summary>
    /// Gets the error code for payment not found.
    /// </summary>
    public override string ErrorCode => "PAYMENT_NOT_FOUND";

    /// <summary>
    /// Gets the payment identifier that was not found.
    /// </summary>
    public object PaymentId { get; }

    /// <summary>
    /// Initializes a new instance of the PaymentNotFoundException class.
    /// </summary>
    /// <param name="paymentId">The payment identifier that was not found</param>
    public PaymentNotFoundException(object paymentId)
        : base($"Payment with identifier '{paymentId}' was not found")
    {
        PaymentId = paymentId;
    }

    /// <summary>
    /// Initializes a new instance of the PaymentNotFoundException class with a custom message.
    /// </summary>
    /// <param name="paymentId">The payment identifier that was not found</param>
    /// <param name="message">Custom error message</param>
    public PaymentNotFoundException(object paymentId, string message)
        : base(message)
    {
        PaymentId = paymentId;
    }
}

/// <summary>
/// Exception thrown when payment state conflicts occur.
/// </summary>
public class PaymentStateConflictException : BusinessException
{
    /// <summary>
    /// Gets the HTTP status code (409 Conflict).
    /// </summary>
    public override int StatusCode => 409;

    /// <summary>
    /// Gets the error code for payment state conflicts.
    /// </summary>
    public override string ErrorCode => "PAYMENT_STATE_CONFLICT";

    /// <summary>
    /// Gets the current payment state.
    /// </summary>
    public string CurrentState { get; }

    /// <summary>
    /// Gets the attempted action.
    /// </summary>
    public string AttemptedAction { get; }

    /// <summary>
    /// Initializes a new instance of the PaymentStateConflictException class.
    /// </summary>
    /// <param name="currentState">The current payment state</param>
    /// <param name="attemptedAction">The action that was attempted</param>
    public PaymentStateConflictException(string currentState, string attemptedAction)
        : base($"Cannot {attemptedAction} payment in state '{currentState}'")
    {
        CurrentState = currentState;
        AttemptedAction = attemptedAction;
    }

    /// <summary>
    /// Initializes a new instance of the PaymentStateConflictException class with custom message.
    /// </summary>
    /// <param name="currentState">The current payment state</param>
    /// <param name="attemptedAction">The action that was attempted</param>
    /// <param name="message">Custom error message</param>
    public PaymentStateConflictException(string currentState, string attemptedAction, string message)
        : base(message)
    {
        CurrentState = currentState;
        AttemptedAction = attemptedAction;
    }
}

/// <summary>
/// Exception thrown when payment processing fails.
/// </summary>
public class PaymentProcessingException : BusinessException
{
    /// <summary>
    /// Gets the HTTP status code (422 Unprocessable Entity).
    /// </summary>
    public override int StatusCode => 422;

    /// <summary>
    /// Gets the error code for payment processing failures.
    /// </summary>
    public override string ErrorCode => "PAYMENT_PROCESSING_FAILED";

    /// <summary>
    /// Gets the payment processor error code if available.
    /// </summary>
    public string? ProcessorErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the PaymentProcessingException class.
    /// </summary>
    /// <param name="message">The error message</param>
    public PaymentProcessingException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the PaymentProcessingException class with processor error code.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="processorErrorCode">The payment processor error code</param>
    public PaymentProcessingException(string message, string processorErrorCode)
        : base(message)
    {
        ProcessorErrorCode = processorErrorCode;
    }

    /// <summary>
    /// Initializes a new instance of the PaymentProcessingException class with an inner exception.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="innerException">The inner exception</param>
    public PaymentProcessingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when duplicate payments are detected.
/// </summary>
public class DuplicatePaymentException : BusinessException
{
    /// <summary>
    /// Gets the HTTP status code (409 Conflict).
    /// </summary>
    public override int StatusCode => 409;

    /// <summary>
    /// Gets the error code for duplicate payments.
    /// </summary>
    public override string ErrorCode => "DUPLICATE_PAYMENT";

    /// <summary>
    /// Gets the existing payment identifier.
    /// </summary>
    public object ExistingPaymentId { get; }

    /// <summary>
    /// Initializes a new instance of the DuplicatePaymentException class.
    /// </summary>
    /// <param name="existingPaymentId">The ID of the existing payment</param>
    public DuplicatePaymentException(object existingPaymentId)
        : base("A payment with similar details already exists")
    {
        ExistingPaymentId = existingPaymentId;
    }

    /// <summary>
    /// Initializes a new instance of the DuplicatePaymentException class with custom message.
    /// </summary>
    /// <param name="existingPaymentId">The ID of the existing payment</param>
    /// <param name="message">Custom error message</param>
    public DuplicatePaymentException(object existingPaymentId, string message)
        : base(message)
    {
        ExistingPaymentId = existingPaymentId;
    }
}