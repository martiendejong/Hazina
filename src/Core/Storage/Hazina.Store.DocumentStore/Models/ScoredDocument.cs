using System;
using System.Collections.Generic;

/// <summary>
/// Represents a document with its scoring information.
/// Used during the ranking process.
/// </summary>
public class ScoredDocument
{
    /// <summary>
    /// Document identifier.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Document content or text.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Original similarity score from vector search (0.0 to 1.0).
    /// </summary>
    public double Similarity { get; set; }

    /// <summary>
    /// Tag-based relevance score (0.0 to 1.0).
    /// Computed from TagRelevanceIndex.
    /// </summary>
    public double TagScore { get; set; }

    /// <summary>
    /// Recency score based on document creation date (0.0 to 1.0).
    /// </summary>
    public double RecencyScore { get; set; }

    /// <summary>
    /// Position score based on original retrieval order (0.0 to 1.0).
    /// </summary>
    public double PositionScore { get; set; }

    /// <summary>
    /// Final composite score after applying weights (0.0 to 1.0).
    /// </summary>
    public double CompositeScore { get; set; }

    /// <summary>
    /// Document metadata containing tags, MIME type, etc.
    /// </summary>
    public DocumentMetadata? Metadata { get; set; }

    /// <summary>
    /// Additional scoring details for debugging/transparency.
    /// </summary>
    public Dictionary<string, double> ScoreBreakdown { get; set; } = new();

    /// <summary>
    /// Create a scored document from metadata and similarity.
    /// </summary>
    public static ScoredDocument FromMetadata(DocumentMetadata metadata, double similarity)
    {
        return new ScoredDocument
        {
            Id = metadata.Id,
            Content = metadata.SearchableText ?? metadata.Summary ?? "",
            Similarity = similarity,
            Metadata = metadata
        };
    }
}
