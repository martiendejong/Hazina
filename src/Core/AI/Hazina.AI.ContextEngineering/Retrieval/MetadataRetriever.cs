using Hazina.AI.ContextEngineering.Interfaces;
using Hazina.AI.ContextEngineering.Models;
// DocumentStore types are in global namespace
using Microsoft.Extensions.Logging;

namespace Hazina.AI.ContextEngineering.Retrieval;

/// <summary>
/// Retriever that uses metadata filtering and keyword search
/// (wraps existing metadata store functionality)
/// </summary>
public class MetadataRetriever : IContextRetriever
{
    private readonly IQueryableMetadataStore _metadataStore;
    private readonly ILogger<MetadataRetriever>? _logger;

    /// <inheritdoc />
    public string Name => "metadata";

    /// <summary>
    /// Creates a new metadata retriever
    /// </summary>
    /// <param name="metadataStore">Metadata store instance</param>
    /// <param name="logger">Optional logger</param>
    public MetadataRetriever(
        IQueryableMetadataStore metadataStore,
        ILogger<MetadataRetriever>? logger = null)
    {
        _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<RetrievalResult>> RetrieveAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        var filter = new MetadataFilter
        {
            Limit = query.TopK
        };

        // Apply metadata filters if provided
        if (query.MetadataFilters != null && query.MetadataFilters.Any())
        {
            filter.CustomMetadata = query.MetadataFilters;
        }

        // Apply tag filters if provided
        if (query.Tags != null && query.Tags.Any())
        {
            filter.Tags = query.Tags;
        }

        // Perform text search
        var searchText = query.QueryText;
        var results = await _metadataStore.SearchTextAsync(
            searchText,
            filter,
            query.TopK,
            cancellationToken
        );

        _logger?.LogDebug(
            "Metadata retriever found {Count} results for query: {Query}",
            results.Count,
            query.QueryText
        );

        return results.Select((meta, index) => new RetrievalResult
        {
            Id = meta.Id,
            Content = meta.SearchableText ?? meta.Summary ?? "",
            Score = CalculateKeywordRelevance(searchText, meta, index, results.Count),
            Source = Name,
            Type = "document",
            Metadata = new Dictionary<string, object>
            {
                ["originalPath"] = meta.OriginalPath,
                ["mimeType"] = meta.MimeType,
                ["customMetadata"] = meta.CustomMetadata ?? new Dictionary<string, string>()
            },
            Tags = meta.Tags ?? new List<string>(),
            Timestamp = meta.Created
        }).ToList();
    }

    private double CalculateKeywordRelevance(
        string searchText,
        DocumentMetadata meta,
        int position,
        int total)
    {
        // Position-based score (first results ranked higher)
        double positionScore = 1.0 - (position / (double)Math.Max(total, 1));

        // Boost if search text appears in content
        var content = (meta.SearchableText ?? meta.Summary ?? "").ToLowerInvariant();
        var hasMatch = content.Contains(searchText.ToLowerInvariant());
        double matchBoost = hasMatch ? 0.2 : 0.0;

        return Math.Min(positionScore + matchBoost, 1.0);
    }
}
