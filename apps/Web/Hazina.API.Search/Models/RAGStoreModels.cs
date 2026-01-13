namespace Hazina.API.Search.Models;

/// <summary>
/// Represents a RAG store
/// </summary>
public class RAGStore
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RAGStoreConfig Config { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int DocumentCount { get; set; }
    public long TotalSizeBytes { get; set; }
}

/// <summary>
/// RAG store configuration
/// </summary>
public class RAGStoreConfig
{
    public Guid Id { get; set; }
    public Guid RAGStoreId { get; set; }
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string LLMModel { get; set; } = "gpt-4o";
    public ChunkingStrategy ChunkingStrategy { get; set; } = ChunkingStrategy.Semantic;
    public int ChunkSize { get; set; } = 1000;
    public int ChunkOverlap { get; set; } = 200;
    public int TopK { get; set; } = 5;
    public float MinRelevanceScore { get; set; } = 0.7f;
}

/// <summary>
/// Chunking strategies for document processing
/// </summary>
public enum ChunkingStrategy
{
    Fixed,
    Semantic,
    SlidingWindow,
    Paragraph
}

/// <summary>
/// Request to create a new RAG store
/// </summary>
public class CreateRAGStoreRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? EmbeddingModel { get; set; }
    public string? LLMModel { get; set; }
    public ChunkingStrategy? ChunkingStrategy { get; set; }
    public int? ChunkSize { get; set; }
    public int? ChunkOverlap { get; set; }
    public int? TopK { get; set; }
    public float? MinRelevanceScore { get; set; }
}

/// <summary>
/// Request to update RAG store configuration
/// </summary>
public class UpdateRAGStoreConfigRequest
{
    public string? EmbeddingModel { get; set; }
    public string? LLMModel { get; set; }
    public ChunkingStrategy? ChunkingStrategy { get; set; }
    public int? ChunkSize { get; set; }
    public int? ChunkOverlap { get; set; }
    public int? TopK { get; set; }
    public float? MinRelevanceScore { get; set; }
}

/// <summary>
/// Extracted content from a document
/// </summary>
public class ExtractedContent
{
    public string Text { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<string> Pages { get; set; } = new();
    public string MimeType { get; set; } = string.Empty;
    public int PageCount { get; set; }
}
