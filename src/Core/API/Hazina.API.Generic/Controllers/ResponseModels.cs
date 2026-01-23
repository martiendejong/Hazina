namespace Hazina.API.Generic.Controllers;

/// <summary>
/// Paginated result container
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// The items on this page
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Whether there is a previous page
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Whether there is a next page
    /// </summary>
    public bool HasNextPage => Page < TotalPages;
}

/// <summary>
/// Result of a bulk operation
/// </summary>
public class BulkResult
{
    /// <summary>
    /// Number of successful operations
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed operations
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Details of any errors
    /// </summary>
    public List<BulkError> Errors { get; set; } = new();
}

/// <summary>
/// Error detail for a single item in a bulk operation
/// </summary>
public class BulkError
{
    /// <summary>
    /// Index of the item that failed
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result of a count operation
/// </summary>
public class CountResult
{
    /// <summary>
    /// Total count of entities
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// Result of an exists check
/// </summary>
public class ExistsResult
{
    /// <summary>
    /// Whether the entity exists
    /// </summary>
    public bool Exists { get; set; }
}
