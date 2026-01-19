using System.ComponentModel;

namespace Hazina.Tools.Common.Models.DTO
{
    /// <summary>
    /// Generic pagination request parameters for list endpoints.
    /// Use this in controllers to enable consistent pagination across all APIs.
    /// </summary>
    public class PagedRequest
    {
        /// <summary>
        /// Page number (1-based index)
        /// </summary>
        [DefaultValue(1)]
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Number of items per page
        /// </summary>
        [DefaultValue(20)]
        public int PageSize { get; set; } = 20;

        /// <summary>
        /// Maximum allowed page size to prevent abuse
        /// </summary>
        public const int MaxPageSize = 100;

        /// <summary>
        /// Property name to sort by (optional)
        /// Example: "CreatedAt", "Name", "Id"
        /// </summary>
        public string? SortBy { get; set; }

        /// <summary>
        /// Sort direction (true = descending, false = ascending)
        /// </summary>
        [DefaultValue(false)]
        public bool SortDescending { get; set; } = false;

        /// <summary>
        /// Search/filter query (optional)
        /// Implementation depends on the specific endpoint
        /// </summary>
        public string? SearchQuery { get; set; }

        /// <summary>
        /// Validates and normalizes pagination parameters
        /// </summary>
        public void Normalize()
        {
            // Ensure page number is at least 1
            if (PageNumber < 1)
                PageNumber = 1;

            // Ensure page size is within limits
            if (PageSize < 1)
                PageSize = 20;
            else if (PageSize > MaxPageSize)
                PageSize = MaxPageSize;
        }

        /// <summary>
        /// Calculates the number of items to skip for the current page
        /// </summary>
        public int Skip => (PageNumber - 1) * PageSize;

        /// <summary>
        /// Returns the page size (for clarity in LINQ queries)
        /// </summary>
        public int Take => PageSize;
    }
}
