namespace Hazina.AI.RAG.Interfaces;

/// <summary>
/// Represents a single retrieval candidate from a document store.
/// Carries all information needed for reranking and answer composition.
/// </summary>
public interface IRetrievalCandidate
{
    /// <summary>
    /// Unique identifier for the chunk
    /// </summary>
    string ChunkId { get; }

    /// <summary>
    /// Identifier of the source document this chunk belongs to
    /// </summary>
    string SourceDocumentId { get; }

    /// <summary>
    /// Original similarity score from vector search (0.0 to 1.0)
    /// </summary>
    double OriginalScore { get; }

    /// <summary>
    /// Rerank score if reranking was applied, null otherwise
    /// </summary>
    double? RerankScore { get; }

    /// <summary>
    /// Text content of the chunk
    /// </summary>
    string Text { get; }

    /// <summary>
    /// Optional metadata for extensibility
    /// </summary>
    Dictionary<string, object>? Metadata { get; }
}

/// <summary>
/// Default implementation of IRetrievalCandidate
/// </summary>
public class RetrievalCandidate : IRetrievalCandidate
{
    public required string ChunkId { get; init; }
    public required string SourceDocumentId { get; init; }
    public required double OriginalScore { get; init; }
    public double? RerankScore { get; set; }
    public required string Text { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
