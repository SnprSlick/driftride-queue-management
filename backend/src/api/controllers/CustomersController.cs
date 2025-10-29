using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DriftRide.Services;
using DriftRide.Models;
using DriftRide.Api.Models;
using DriftRide.Api.Shared;

namespace DriftRide.Api.Controllers;

/// <summary>
/// Controller for customer management operations.
/// Handles customer creation and retrieval following the OpenAPI specification.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CustomersController : BaseApiController
{
    private readonly ICustomerService _customerService;

    /// <summary>
    /// Initializes a new instance of the CustomersController.
    /// </summary>
    /// <param name="customerService">Customer service for business operations</param>
    /// <param name="logger">Logger instance</param>
    public CustomersController(
        ICustomerService customerService,
        ILogger<CustomersController> logger) : base(logger)
    {
        _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
    }

    /// <summary>
    /// Creates a new customer record.
    /// </summary>
    /// <param name="request">Customer creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created customer response</returns>
    /// <response code="201">Customer created successfully</response>
    /// <response code="400">Bad request - validation failed</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - insufficient permissions</response>
    /// <response code="409">Conflict - customer already exists</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Api.Models.Customer>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<Api.Models.Customer>>> CreateCustomer(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate model state
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        // Check authorization - only Sales role can create customers
        if (!HasRole("Sales"))
        {
            return ForbiddenError("Only sales staff can create customers");
        }

        try
        {
            // Create customer using the service
            var domainCustomer = await _customerService.CreateAsync(
                request.Name,
                request.Email,
                request.Phone,
                cancellationToken);

            // Map domain model to API model
            var apiCustomer = MapToApiModel(domainCustomer);

            Logger.LogInformation("Customer created successfully with ID {CustomerId} by user {UserId}",
                domainCustomer.Id, CurrentUserId);

            var response = new ApiResponse<Api.Models.Customer>
            {
                Success = true,
                Message = "Customer created successfully",
                Data = apiCustomer
            };

            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning("Customer creation failed due to validation: {Message}", ex.Message);
            return BadRequestError(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning("Customer creation failed due to business rule: {Message}", ex.Message);
            return ConflictError(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during customer creation");
            return InternalServerError(ex, "An error occurred while creating the customer");
        }
    }

    /// <summary>
    /// Retrieves a customer by their unique identifier.
    /// </summary>
    /// <param name="id">Customer identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Customer details</returns>
    /// <response code="200">Customer retrieved successfully</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="403">Forbidden - insufficient permissions</response>
    /// <response code="404">Customer not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<Api.Models.Customer>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<Api.Models.Customer>>> GetCustomer(
        int id,
        CancellationToken cancellationToken = default)
    {
        // Check authorization - both Sales and Driver roles can view customers
        if (!HasAnyRole("Sales", "Driver"))
        {
            return ForbiddenError("Insufficient permissions to view customer details");
        }

        try
        {
            // Convert int ID to Guid for domain service call
            // This is a temporary mapping until ID strategy is unified
            var domainId = MapToDomainId(id);

            var domainCustomer = await _customerService.GetByIdAsync(domainId, cancellationToken);

            if (domainCustomer == null)
            {
                return NotFoundError("Customer", id);
            }

            // Map domain model to API model
            var apiCustomer = MapToApiModel(domainCustomer);

            Logger.LogInformation("Customer {CustomerId} retrieved by user {UserId}",
                domainCustomer.Id, CurrentUserId);

            return Success(apiCustomer, "Customer retrieved successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during customer retrieval for ID {CustomerId}", id);
            return InternalServerError(ex, "An error occurred while retrieving the customer");
        }
    }

    /// <summary>
    /// Maps a domain Customer model to API Customer model.
    /// Handles the conversion between Guid and int IDs temporarily.
    /// </summary>
    /// <param name="domainCustomer">Domain customer model</param>
    /// <returns>API customer model</returns>
    private static Api.Models.Customer MapToApiModel(DriftRide.Models.Customer domainCustomer)
    {
        return new Api.Models.Customer
        {
            Id = IdMapper.MapCustomerToApiId(domainCustomer.Id),
            Name = domainCustomer.Name,
            Email = domainCustomer.Email,
            Phone = domainCustomer.PhoneNumber,
            IsActive = domainCustomer.IsActive,
            CreatedAt = domainCustomer.CreatedAt
        };
    }

    /// <summary>
    /// Maps an int API ID to a Guid domain ID using the shared ID mapper.
    /// </summary>
    /// <param name="apiId">API int ID</param>
    /// <returns>Domain Guid ID</returns>
    private static Guid MapToDomainId(int apiId)
    {
        return IdMapper.MapCustomerToDomainId(apiId);
    }
}