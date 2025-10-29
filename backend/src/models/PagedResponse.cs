using System.Text.Json.Serialization;

namespace DriftRide.Models;

/// <summary>
/// Represents a paginated API response with metadata about the pagination state.
/// </summary>
/// <typeparam name="T">The type of data items in the paginated collection.</typeparam>
public class PagedResponse<T> : ApiResponse<IEnumerable<T>>
{
    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total number of items across all pages.
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages available.
    /// </summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    /// <summary>
    /// Gets a value indicating whether there is a previous page available.
    /// </summary>
    [JsonPropertyName("hasPreviousPage")]
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Gets a value indicating whether there is a next page available.
    /// </summary>
    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Gets the index of the first item on the current page (1-based).
    /// </summary>
    [JsonPropertyName("firstItemIndex")]
    public int FirstItemIndex => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    /// <summary>
    /// Gets the index of the last item on the current page (1-based).
    /// </summary>
    [JsonPropertyName("lastItemIndex")]
    public int LastItemIndex => Math.Min(Page * PageSize, TotalCount);

    /// <summary>
    /// Gets or sets navigation links for pagination.
    /// </summary>
    [JsonPropertyName("links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PaginationLinks? Links { get; set; }
}

/// <summary>
/// Represents pagination navigation links for API responses.
/// </summary>
public class PaginationLinks
{
    /// <summary>
    /// Gets or sets the URL for the first page.
    /// </summary>
    [JsonPropertyName("first")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? First { get; set; }

    /// <summary>
    /// Gets or sets the URL for the previous page.
    /// </summary>
    [JsonPropertyName("previous")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Previous { get; set; }

    /// <summary>
    /// Gets or sets the URL for the current page.
    /// </summary>
    [JsonPropertyName("self")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Self { get; set; }

    /// <summary>
    /// Gets or sets the URL for the next page.
    /// </summary>
    [JsonPropertyName("next")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Next { get; set; }

    /// <summary>
    /// Gets or sets the URL for the last page.
    /// </summary>
    [JsonPropertyName("last")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Last { get; set; }
}

/// <summary>
/// Represents pagination request parameters.
/// </summary>
public class PaginationRequest
{
    /// <summary>
    /// Gets or sets the page number (1-based). Defaults to 1.
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of items per page. Defaults to 20.
    /// </summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum allowed page size to prevent performance issues.
    /// </summary>
    [JsonIgnore]
    public int MaxPageSize { get; set; } = 100;

    /// <summary>
    /// Validates and normalizes the pagination parameters.
    /// </summary>
    public void Normalize()
    {
        if (Page < 1)
        {
            Page = 1;
        }

        if (PageSize < 1)
        {
            PageSize = 20;
        }

        if (PageSize > MaxPageSize)
        {
            PageSize = MaxPageSize;
        }
    }

    /// <summary>
    /// Calculates the number of items to skip for the current page.
    /// </summary>
    /// <returns>The number of items to skip.</returns>
    public int GetSkipCount()
    {
        return (Page - 1) * PageSize;
    }
}

/// <summary>
/// Extension methods for creating paginated responses.
/// </summary>
public static class PaginationExtensions
{
    /// <summary>
    /// Creates a paginated response from a collection of items.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="items">The items for the current page.</param>
    /// <param name="totalCount">The total number of items across all pages.</param>
    /// <param name="request">The pagination request parameters.</param>
    /// <param name="message">Optional success message.</param>
    /// <returns>A paginated response.</returns>
    public static PagedResponse<T> ToPagedResponse<T>(
        this IEnumerable<T> items,
        int totalCount,
        PaginationRequest request,
        string? message = null)
    {
        request.Normalize();

        return new PagedResponse<T>
        {
            Success = true,
            Message = message ?? "Data retrieved successfully",
            Data = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
        };
    }

    /// <summary>
    /// Creates a paginated response with navigation links.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="items">The items for the current page.</param>
    /// <param name="totalCount">The total number of items across all pages.</param>
    /// <param name="request">The pagination request parameters.</param>
    /// <param name="baseUrl">The base URL for generating navigation links.</param>
    /// <param name="message">Optional success message.</param>
    /// <returns>A paginated response with navigation links.</returns>
    public static PagedResponse<T> ToPagedResponse<T>(
        this IEnumerable<T> items,
        int totalCount,
        PaginationRequest request,
        string baseUrl,
        string? message = null)
    {
        var response = items.ToPagedResponse(totalCount, request, message);

        response.Links = new PaginationLinks
        {
            First = $"{baseUrl}?page=1&pageSize={request.PageSize}",
            Self = $"{baseUrl}?page={request.Page}&pageSize={request.PageSize}",
            Last = $"{baseUrl}?page={response.TotalPages}&pageSize={request.PageSize}"
        };

        if (response.HasPreviousPage)
        {
            response.Links.Previous = $"{baseUrl}?page={request.Page - 1}&pageSize={request.PageSize}";
        }

        if (response.HasNextPage)
        {
            response.Links.Next = $"{baseUrl}?page={request.Page + 1}&pageSize={request.PageSize}";
        }

        return response;
    }
}