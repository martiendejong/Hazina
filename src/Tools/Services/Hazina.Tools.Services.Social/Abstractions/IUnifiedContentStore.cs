using Hazina.Tools.Services.Social.Models;

namespace Hazina.Tools.Services.Social.Abstractions;

/// <summary>
/// Generic content storage that works for ALL platforms.
/// Replaces separate ISocialContentStore with Posts/Articles split.
/// </summary>
public interface IUnifiedContentStore
{
    // ===== Basic CRUD =====

    /// <summary>
    /// Saves content items (works for any platform)
    /// </summary>
    Task SaveContentAsync(
        string projectId,
        IEnumerable<UnifiedContent> content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single content item by ID
    /// </summary>
    Task<UnifiedContent?> GetContentAsync(
        string projectId,
        string contentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content items with filtering
    /// </summary>
    Task<List<UnifiedContent>> GetContentListAsync(
        string projectId,
        UnifiedContentQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes content for an account
    /// </summary>
    Task DeleteAccountContentAsync(
        string projectId,
        string accountId,
        CancellationToken cancellationToken = default);

    // ===== Search & Query =====

    /// <summary>
    /// Full-text search across content
    /// </summary>
    Task<List<UnifiedContent>> SearchAsync(
        string projectId,
        string query,
        UnifiedContentSearchOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Semantic search using vector embeddings
    /// </summary>
    Task<List<UnifiedContent>> SemanticSearchAsync(
        string projectId,
        string query,
        UnifiedContentSearchOptions options,
        CancellationToken cancellationToken = default);

    // ===== Calendar Integration =====

    /// <summary>
    /// Gets content items for calendar display in date range
    /// </summary>
    Task<List<UnifiedContent>> GetCalendarEventsAsync(
        string projectId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    // ===== Analytics =====

    /// <summary>
    /// Gets content statistics
    /// </summary>
    Task<UnifiedContentStats> GetStatsAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets top performing content
    /// </summary>
    Task<List<UnifiedContent>> GetTopPerformingAsync(
        string projectId,
        int limit = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Query parameters for content retrieval
/// </summary>
public class UnifiedContentQuery
{
    public string? AccountId { get; set; }
    public List<string>? SourceTypes { get; set; } // Filter by platform
    public List<string>? ContentTypes { get; set; } // Filter by type (post, page, etc.)
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
    public string? OrderBy { get; set; } = "published_at";
    public bool Descending { get; set; } = true;
}

/// <summary>
/// Search options
/// </summary>
public class UnifiedContentSearchOptions
{
    public List<string>? SourceTypes { get; set; }
    public List<string>? ContentTypes { get; set; }
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
    public int Limit { get; set; } = 20;
    public bool UseSemanticSearch { get; set; } = true;
}

/// <summary>
/// Content statistics
/// </summary>
public class UnifiedContentStats
{
    public int TotalItems { get; set; }
    public Dictionary<string, int> ItemsBySource { get; set; } = new();
    public Dictionary<string, int> ItemsByType { get; set; } = new();
    public int TotalComments { get; set; }
    public long TotalLikes { get; set; }
    public long TotalShares { get; set; }
    public long TotalViews { get; set; }
    public DateTime? OldestContent { get; set; }
    public DateTime? NewestContent { get; set; }
}
