using System.Text.Json.Serialization;

namespace DriftRide.Models;

/// <summary>
/// Generic API response wrapper providing consistent response structure across all endpoints.
/// </summary>
/// <typeparam name="T">The type of data being returned in the response.</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets a human-readable message describing the result of the operation.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data payload of the response. Null for error responses or operations that don't return data.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; set; }

    /// <summary>
    /// Gets or sets error details when the operation fails. Null for successful responses.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorResponse? Error { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the response was generated.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Non-generic API response wrapper for operations that don't return specific data.
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    /// <summary>
    /// Initializes a new instance of the ApiResponse class.
    /// </summary>
    public ApiResponse()
    {
        Data = null;
    }
}