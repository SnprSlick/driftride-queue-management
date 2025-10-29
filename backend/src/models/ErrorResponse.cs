using System.Text.Json.Serialization;

namespace DriftRide.Models;

/// <summary>
/// Represents error details in API responses, providing structured error information.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Gets or sets a machine-readable error code that identifies the type of error.
    /// </summary>
    /// <example>VALIDATION_FAILED, NOT_FOUND, UNAUTHORIZED, INTERNAL_SERVER_ERROR</example>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a human-readable error message describing what went wrong.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional error details, such as validation errors or debugging information.
    /// This field is optional and may contain different types of data depending on the error type.
    /// </summary>
    /// <example>
    /// For validation errors: Dictionary of field names to error messages
    /// For not found errors: Object with resource type and identifier
    /// For conflict errors: Details about the conflicting data
    /// </example>
    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; set; }

    /// <summary>
    /// Gets or sets the stack trace or additional debugging information.
    /// This field should only be populated in development environments.
    /// </summary>
    [JsonPropertyName("stackTrace")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StackTrace { get; set; }

    /// <summary>
    /// Gets or sets a unique identifier for this error instance, useful for support and debugging.
    /// </summary>
    [JsonPropertyName("traceId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; set; }

    /// <summary>
    /// Gets or sets inner error information for nested or chained exceptions.
    /// </summary>
    [JsonPropertyName("innerError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorResponse? InnerError { get; set; }
}

/// <summary>
/// Represents validation error details with field-specific error messages.
/// </summary>
public class ValidationErrorResponse : ErrorResponse
{
    /// <summary>
    /// Gets or sets a dictionary of field names to their validation error messages.
    /// </summary>
    [JsonPropertyName("validationErrors")]
    public Dictionary<string, string[]> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the ValidationErrorResponse class.
    /// </summary>
    public ValidationErrorResponse()
    {
        Code = "VALIDATION_FAILED";
        Message = "One or more validation errors occurred";
    }

    /// <summary>
    /// Initializes a new instance of the ValidationErrorResponse class with validation errors.
    /// </summary>
    /// <param name="validationErrors">Dictionary of field names to error messages.</param>
    public ValidationErrorResponse(Dictionary<string, string[]> validationErrors) : this()
    {
        ValidationErrors = validationErrors ?? new Dictionary<string, string[]>();
        Details = ValidationErrors;
    }
}