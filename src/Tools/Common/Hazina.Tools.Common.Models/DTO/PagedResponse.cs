namespace Hazina.Tools.Common.Models.DTO
{
    /// <summary>
    /// Generic paginated response wrapper for list endpoints.
    /// Provides metadata about the current page and total results.
    /// </summary>
    /// <typeparam name="T">The type of items in the list</typeparam>
    public class PagedResponse<T>
    {
        /// <summary>
        /// The items for the current page
        /// </summary>
        public List<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Total number of items across all pages
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number (1-based)
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Number of items per page
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        /// <summary>
        /// Indicates if there is a previous page
        /// </summary>
        public bool HasPreviousPage => PageNumber > 1;

        /// <summary>
        /// Indicates if there is a next page
        /// </summary>
        public bool HasNextPage => PageNumber < TotalPages;

        /// <summary>
        /// Creates an empty paged response
        /// </summary>
        public PagedResponse() { }

        /// <summary>
        /// Creates a paged response with items and metadata
        /// </summary>
        public PagedResponse(List<T> items, int totalCount, int pageNumber, int pageSize)
        {
            Items = items ?? new List<T>();
            TotalCount = totalCount;
            PageNumber = pageNumber;
            PageSize = pageSize;
        }

        /// <summary>
        /// Creates a paged response from a PagedRequest
        /// </summary>
        public static PagedResponse<T> Create(List<T> items, int totalCount, PagedRequest request)
        {
            return new PagedResponse<T>(items, totalCount, request.PageNumber, request.PageSize);
        }
    }
}
