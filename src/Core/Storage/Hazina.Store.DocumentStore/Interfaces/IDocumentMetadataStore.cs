using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IDocumentMetadataStore
{
    // Existing methods - unchanged for backwards compatibility
    public Task<bool> Store(string id, DocumentMetadata metadata);
    public Task<DocumentMetadata?> Get(string id);
    public Task<bool> Remove(string id);
    public Task<bool> Exists(string id);

    // New query methods - optional implementation via extension interface
}

/// <summary>
/// Extended interface for metadata querying.
/// Implement this alongside IDocumentMetadataStore to enable metadata-first search.
/// </summary>
public interface IQueryableMetadataStore : IDocumentMetadataStore
{
    /// <summary>
    /// Query metadata using filters. Returns all matching documents.
    /// </summary>
    Task<List<DocumentMetadata>> QueryAsync(
        MetadataFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all document IDs matching the filter (lightweight query).
    /// </summary>
    Task<List<string>> GetMatchingIdsAsync(
        MetadataFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Count documents matching the filter.
    /// </summary>
    Task<int> CountAsync(
        MetadataFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Full-text search on content/summary fields.
    /// </summary>
    Task<List<DocumentMetadata>> SearchTextAsync(
        string searchText,
        MetadataFilter? filter = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Filter criteria for metadata queries.
/// All conditions are AND-ed together.
/// </summary>
public class MetadataFilter
{
    /// <summary>Filter by MIME type (exact match)</summary>
    public string? MimeType { get; set; }

    /// <summary>Filter by MIME type prefix (e.g., "text/" matches text/plain, text/html)</summary>
    public string? MimeTypePrefix { get; set; }

    /// <summary>Filter by original path pattern (supports * wildcard)</summary>
    public string? PathPattern { get; set; }

    /// <summary>Filter documents created after this date</summary>
    public DateTime? CreatedAfter { get; set; }

    /// <summary>Filter documents created before this date</summary>
    public DateTime? CreatedBefore { get; set; }

    /// <summary>Filter by custom metadata key-value pairs (all must match)</summary>
    public Dictionary<string, string>? CustomMetadata { get; set; }

    /// <summary>Filter by tags (document must have ALL specified tags)</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Filter by tags (document must have ANY of the specified tags)</summary>
    public List<string>? AnyTags { get; set; }

    /// <summary>Include only binary files</summary>
    public bool? IsBinary { get; set; }

    /// <summary>Maximum number of results to return</summary>
    public int Limit { get; set; } = 100;

    /// <summary>Skip this many results (for pagination)</summary>
    public int Offset { get; set; } = 0;

    /// <summary>Returns true if no filters are set</summary>
    public bool IsEmpty =>
        MimeType == null &&
        MimeTypePrefix == null &&
        PathPattern == null &&
        CreatedAfter == null &&
        CreatedBefore == null &&
        (CustomMetadata == null || CustomMetadata.Count == 0) &&
        (Tags == null || Tags.Count == 0) &&
        (AnyTags == null || AnyTags.Count == 0) &&
        IsBinary == null;
}
