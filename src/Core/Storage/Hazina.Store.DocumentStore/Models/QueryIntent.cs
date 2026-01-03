using System;
using System.Collections.Generic;

/// <summary>
/// Represents the detected intent of a search query.
/// Used to route queries to optimal search strategies.
/// </summary>
public enum QueryIntentType
{
    /// <summary>
    /// Semantic/conceptual search - needs embeddings.
    /// Examples: "What is the main thesis?", "Explain the argument"
    /// </summary>
    Semantic,

    /// <summary>
    /// Metadata filter - SQL only, no embeddings needed.
    /// Examples: "Find PDFs from last week", "Show all images"
    /// </summary>
    MetadataFilter,

    /// <summary>
    /// Tag-based search - filter by tags, optional embeddings.
    /// Examples: "Documents tagged evidence", "Find research materials"
    /// </summary>
    TagSearch,

    /// <summary>
    /// Similarity search - find documents similar to a reference.
    /// Examples: "Similar to document X", "More like this"
    /// </summary>
    Similarity,

    /// <summary>
    /// Keyword/exact match search - full-text search.
    /// Examples: "Search for 'climate change'", "Find mentions of X"
    /// </summary>
    Keyword,

    /// <summary>
    /// Hybrid search - combine multiple strategies.
    /// Examples: "Recent PDFs about climate change"
    /// </summary>
    Hybrid,

    /// <summary>
    /// Unknown or ambiguous intent.
    /// </summary>
    Unknown
}

/// <summary>
/// Result of query intent classification.
/// </summary>
public class QueryIntent
{
    /// <summary>
    /// Primary detected intent type.
    /// </summary>
    public QueryIntentType Type { get; set; } = QueryIntentType.Unknown;

    /// <summary>
    /// Confidence score for the classification (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; } = 0.5;

    /// <summary>
    /// Secondary intent if hybrid search is detected.
    /// </summary>
    public QueryIntentType? SecondaryType { get; set; }

    /// <summary>
    /// Extracted metadata filters from the query.
    /// </summary>
    public ExtractedFilters Filters { get; set; } = new();

    /// <summary>
    /// The semantic/keyword portion of the query after filter extraction.
    /// </summary>
    public string SemanticQuery { get; set; } = "";

    /// <summary>
    /// Whether embeddings are recommended for this query.
    /// </summary>
    public bool RecommendEmbeddings => Type == QueryIntentType.Semantic ||
                                        Type == QueryIntentType.Similarity ||
                                        Type == QueryIntentType.Hybrid;

    /// <summary>
    /// Whether metadata filtering should be applied.
    /// </summary>
    public bool RecommendMetadataFilter => Type == QueryIntentType.MetadataFilter ||
                                            Type == QueryIntentType.TagSearch ||
                                            Type == QueryIntentType.Hybrid ||
                                            Filters.HasFilters;

    /// <summary>
    /// Explanation of why this intent was detected.
    /// </summary>
    public string Explanation { get; set; } = "";
}

/// <summary>
/// Filters extracted from a query.
/// </summary>
public class ExtractedFilters
{
    /// <summary>
    /// MIME type filter (e.g., "application/pdf").
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// MIME type prefix (e.g., "image/").
    /// </summary>
    public string? MimeTypePrefix { get; set; }

    /// <summary>
    /// Tags to filter by (ANY match).
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Date filter - after this date.
    /// </summary>
    public DateTime? CreatedAfter { get; set; }

    /// <summary>
    /// Date filter - before this date.
    /// </summary>
    public DateTime? CreatedBefore { get; set; }

    /// <summary>
    /// Reference document ID for similarity search.
    /// </summary>
    public string? SimilarToDocumentId { get; set; }

    /// <summary>
    /// Exact keywords to search for.
    /// </summary>
    public List<string> ExactKeywords { get; set; } = new();

    /// <summary>
    /// Whether any filters were extracted.
    /// </summary>
    public bool HasFilters =>
        !string.IsNullOrEmpty(MimeType) ||
        !string.IsNullOrEmpty(MimeTypePrefix) ||
        Tags.Count > 0 ||
        CreatedAfter.HasValue ||
        CreatedBefore.HasValue ||
        !string.IsNullOrEmpty(SimilarToDocumentId) ||
        ExactKeywords.Count > 0;

    /// <summary>
    /// Convert to MetadataFilter for store queries.
    /// </summary>
    public MetadataFilter ToMetadataFilter()
    {
        return new MetadataFilter
        {
            MimeType = MimeType,
            MimeTypePrefix = MimeTypePrefix,
            AnyTags = Tags.Count > 0 ? Tags : null,
            CreatedAfter = CreatedAfter,
            CreatedBefore = CreatedBefore
        };
    }
}
