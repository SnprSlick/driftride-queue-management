using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DriftRide.Models;

namespace DriftRide.Api.Controllers;

/// <summary>
/// Abstract base class for all API controllers providing common functionality,
/// authentication requirements, and standardized response patterns.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Logger instance for the derived controller.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the BaseApiController class.
    /// </summary>
    /// <param name="logger">The logger instance for this controller.</param>
    protected BaseApiController(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current authenticated user's ID from JWT claims.
    /// </summary>
    protected int CurrentUserId
    {
        get
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
            throw new UnauthorizedAccessException("User ID not found in token claims");
        }
    }

    /// <summary>
    /// Gets the current authenticated user's username from JWT claims.
    /// </summary>
    protected string CurrentUsername
    {
        get
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username))
            {
                throw new UnauthorizedAccessException("Username not found in token claims");
            }
            return username;
        }
    }

    /// <summary>
    /// Gets the current authenticated user's role from JWT claims.
    /// </summary>
    protected string CurrentUserRole
    {
        get
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(role))
            {
                throw new UnauthorizedAccessException("User role not found in token claims");
            }
            return role;
        }
    }

    /// <summary>
    /// Gets the current authenticated user's display name from JWT claims.
    /// </summary>
    protected string CurrentUserDisplayName
    {
        get
        {
            var displayName = User.FindFirst("displayName")?.Value;
            if (string.IsNullOrEmpty(displayName))
            {
                throw new UnauthorizedAccessException("Display name not found in token claims");
            }
            return displayName;
        }
    }

    /// <summary>
    /// Checks if the current user has the specified role.
    /// </summary>
    /// <param name="role">The role to check for.</param>
    /// <returns>True if the user has the specified role, false otherwise.</returns>
    protected bool HasRole(string role)
    {
        return User.IsInRole(role);
    }

    /// <summary>
    /// Checks if the current user has any of the specified roles.
    /// </summary>
    /// <param name="roles">The roles to check for.</param>
    /// <returns>True if the user has any of the specified roles, false otherwise.</returns>
    protected bool HasAnyRole(params string[] roles)
    {
        return roles.Any(role => User.IsInRole(role));
    }

    /// <summary>
    /// Creates a successful API response with data.
    /// </summary>
    /// <typeparam name="T">The type of data being returned.</typeparam>
    /// <param name="data">The data to include in the response.</param>
    /// <param name="message">Optional success message.</param>
    /// <returns>A successful API response.</returns>
    protected ActionResult<ApiResponse<T>> Success<T>(T data, string? message = null)
    {
        var response = new ApiResponse<T>
        {
            Success = true,
            Message = message ?? "Operation completed successfully",
            Data = data
        };

        return Ok(response);
    }

    /// <summary>
    /// Creates a successful API response without data.
    /// </summary>
    /// <param name="message">Optional success message.</param>
    /// <returns>A successful API response.</returns>
    protected ActionResult<ApiResponse<object>> Success(string? message = null)
    {
        var response = new ApiResponse<object>
        {
            Success = true,
            Message = message ?? "Operation completed successfully",
            Data = null
        };

        return Ok(response);
    }

    /// <summary>
    /// Creates a successful paginated API response.
    /// </summary>
    /// <typeparam name="T">The type of data being returned.</typeparam>
    /// <param name="data">The paginated data.</param>
    /// <param name="totalCount">The total number of items across all pages.</param>
    /// <param name="page">The current page number.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="message">Optional success message.</param>
    /// <returns>A successful paginated API response.</returns>
    protected ActionResult<PagedResponse<T>> SuccessPaged<T>(
        IEnumerable<T> data,
        int totalCount,
        int page,
        int pageSize,
        string? message = null)
    {
        var response = new PagedResponse<T>
        {
            Success = true,
            Message = message ?? "Operation completed successfully",
            Data = data,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };

        return Ok(response);
    }

    /// <summary>
    /// Creates a bad request error response.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="details">Optional error details.</param>
    /// <returns>A bad request error response.</returns>
    protected ActionResult<ApiResponse<object>> BadRequestError(string message, object? details = null)
    {
        var response = new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Data = null,
            Error = new ErrorResponse
            {
                Code = "BAD_REQUEST",
                Message = message,
                Details = details
            }
        };

        Logger.LogWarning("Bad request: {Message}. Details: {@Details}", message, details);
        return BadRequest(response);
    }

    /// <summary>
    /// Creates a not found error response.
    /// </summary>
    /// <param name="resource">The resource that was not found.</param>
    /// <param name="identifier">The identifier used to search for the resource.</param>
    /// <returns>A not found error response.</returns>
    protected ActionResult<ApiResponse<object>> NotFoundError(string resource, object identifier)
    {
        var message = $"{resource} with identifier '{identifier}' was not found";
        var response = new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Data = null,
            Error = new ErrorResponse
            {
                Code = "NOT_FOUND",
                Message = message,
                Details = new { Resource = resource, Identifier = identifier }
            }
        };

        Logger.LogWarning("Resource not found: {Resource} with identifier {Identifier}", resource, identifier);
        return NotFound(response);
    }

    /// <summary>
    /// Creates an unauthorized error response.
    /// </summary>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>An unauthorized error response.</returns>
    protected ActionResult<ApiResponse<object>> UnauthorizedError(string? message = null)
    {
        var errorMessage = message ?? "You are not authorized to perform this action";
        var response = new ApiResponse<object>
        {
            Success = false,
            Message = errorMessage,
            Data = null,
            Error = new ErrorResponse
            {
                Code = "UNAUTHORIZED",
                Message = errorMessage
            }
        };

        Logger.LogWarning("Unauthorized access attempt: {Message}", errorMessage);
        return Unauthorized(response);
    }

    /// <summary>
    /// Creates a forbidden error response.
    /// </summary>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A forbidden error response.</returns>
    protected ActionResult<ApiResponse<object>> ForbiddenError(string? message = null)
    {
        var errorMessage = message ?? "You do not have permission to access this resource";
        var response = new ApiResponse<object>
        {
            Success = false,
            Message = errorMessage,
            Data = null,
            Error = new ErrorResponse
            {
                Code = "FORBIDDEN",
                Message = errorMessage
            }
        };

        Logger.LogWarning("Forbidden access attempt by user {UserId}: {Message}", CurrentUserId, errorMessage);
        return StatusCode(403, response);
    }

    /// <summary>
    /// Creates a conflict error response.
    /// </summary>
    /// <param name="message">The conflict error message.</param>
    /// <param name="details">Optional conflict details.</param>
    /// <returns>A conflict error response.</returns>
    protected ActionResult<ApiResponse<object>> ConflictError(string message, object? details = null)
    {
        var response = new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Data = null,
            Error = new ErrorResponse
            {
                Code = "CONFLICT",
                Message = message,
                Details = details
            }
        };

        Logger.LogWarning("Conflict error: {Message}. Details: {@Details}", message, details);
        return Conflict(response);
    }

    /// <summary>
    /// Creates an internal server error response.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>An internal server error response.</returns>
    protected ActionResult<ApiResponse<object>> InternalServerError(Exception exception, string? message = null)
    {
        var errorMessage = message ?? "An internal server error occurred";
        var response = new ApiResponse<object>
        {
            Success = false,
            Message = errorMessage,
            Data = null,
            Error = new ErrorResponse
            {
                Code = "INTERNAL_SERVER_ERROR",
                Message = errorMessage
            }
        };

        Logger.LogError(exception, "Internal server error: {Message}", errorMessage);
        return StatusCode(500, response);
    }

    /// <summary>
    /// Creates a validation error response from ModelState.
    /// </summary>
    /// <returns>A validation error response.</returns>
    protected ActionResult<ApiResponse<object>> ValidationError()
    {
        var errors = ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
            );

        var response = new ApiResponse<object>
        {
            Success = false,
            Message = "Validation failed",
            Data = null,
            Error = new ErrorResponse
            {
                Code = "VALIDATION_FAILED",
                Message = "One or more validation errors occurred",
                Details = errors
            }
        };

        Logger.LogWarning("Validation errors: {@Errors}", errors);
        return BadRequest(response);
    }

    /// <summary>
    /// Executes an async operation with standard error handling.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="operationName">The name of the operation for logging.</param>
    /// <returns>The result of the operation or an error response.</returns>
    protected async Task<ActionResult<ApiResponse<T>>> ExecuteAsync<T>(
        Func<Task<T>> operation,
        string operationName)
    {
        try
        {
            Logger.LogInformation("Starting operation: {OperationName}", operationName);
            var result = await operation();
            Logger.LogInformation("Operation completed successfully: {OperationName}", operationName);
            return Success(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning("Unauthorized access: {Message}", ex.Message);
            return Unauthorized(new ApiResponse<T>
            {
                Success = false,
                Message = ex.Message,
                Data = default,
                Error = new ErrorResponse { Code = "UNAUTHORIZED", Message = ex.Message }
            });
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning("Bad request: {Message}", ex.Message);
            return BadRequest(new ApiResponse<T>
            {
                Success = false,
                Message = ex.Message,
                Data = default,
                Error = new ErrorResponse { Code = "BAD_REQUEST", Message = ex.Message }
            });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning("Conflict: {Message}", ex.Message);
            return Conflict(new ApiResponse<T>
            {
                Success = false,
                Message = ex.Message,
                Data = default,
                Error = new ErrorResponse { Code = "CONFLICT", Message = ex.Message }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Internal server error during {OperationName}", operationName);
            return StatusCode(500, new ApiResponse<T>
            {
                Success = false,
                Message = $"An error occurred during {operationName}",
                Data = default,
                Error = new ErrorResponse { Code = "INTERNAL_SERVER_ERROR", Message = ex.Message }
            });
        }
    }

    /// <summary>
    /// Executes an async operation that returns void with standard error handling.
    /// </summary>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="operationName">The name of the operation for logging.</param>
    /// <param name="successMessage">Optional success message.</param>
    /// <returns>A success response or an error response.</returns>
    protected async Task<ActionResult<ApiResponse<object>>> ExecuteAsync(
        Func<Task> operation,
        string operationName,
        string? successMessage = null)
    {
        try
        {
            Logger.LogInformation("Starting operation: {OperationName}", operationName);
            await operation();
            Logger.LogInformation("Operation completed successfully: {OperationName}", operationName);
            return Success(successMessage ?? $"{operationName} completed successfully");
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning("Unauthorized access: {Message}", ex.Message);
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = ex.Message,
                Data = null,
                Error = new ErrorResponse { Code = "UNAUTHORIZED", Message = ex.Message }
            });
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning("Bad request: {Message}", ex.Message);
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = ex.Message,
                Data = null,
                Error = new ErrorResponse { Code = "BAD_REQUEST", Message = ex.Message }
            });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning("Conflict: {Message}", ex.Message);
            return Conflict(new ApiResponse<object>
            {
                Success = false,
                Message = ex.Message,
                Data = null,
                Error = new ErrorResponse { Code = "CONFLICT", Message = ex.Message }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Internal server error during {OperationName}", operationName);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = $"An error occurred during {operationName}",
                Data = null,
                Error = new ErrorResponse { Code = "INTERNAL_SERVER_ERROR", Message = ex.Message }
            });
        }
    }

    /// <summary>
    /// Executes an async operation with standard error handling.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <returns>The result of the operation or an error response.</returns>
    protected async Task<ActionResult<ApiResponse<T>>> ExecuteAsync<T>(Func<Task<ActionResult<ApiResponse<T>>>> operation)
    {
        try
        {
            var result = await operation();
            return result;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning("Unauthorized access: {Message}", ex.Message);
            return Unauthorized(new ApiResponse<T>
            {
                Success = false,
                Message = ex.Message,
                Data = default,
                Error = new ErrorResponse { Code = "UNAUTHORIZED", Message = ex.Message }
            });
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning("Bad request: {Message}", ex.Message);
            return BadRequest(new ApiResponse<T>
            {
                Success = false,
                Message = ex.Message,
                Data = default,
                Error = new ErrorResponse { Code = "BAD_REQUEST", Message = ex.Message }
            });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning("Conflict: {Message}", ex.Message);
            return Conflict(new ApiResponse<T>
            {
                Success = false,
                Message = ex.Message,
                Data = default,
                Error = new ErrorResponse { Code = "CONFLICT", Message = ex.Message }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Internal server error");
            return StatusCode(500, new ApiResponse<T>
            {
                Success = false,
                Message = "An internal server error occurred",
                Data = default,
                Error = new ErrorResponse { Code = "INTERNAL_SERVER_ERROR", Message = ex.Message }
            });
        }
    }
}