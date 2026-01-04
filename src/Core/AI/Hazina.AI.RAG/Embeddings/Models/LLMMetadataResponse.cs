namespace Hazina.AI.RAG.Embeddings.Models;

/// <summary>
/// LLM-generated metadata for a single text chunk
/// Used for premium metadata extraction (Tier: LLM)
/// </summary>
public class ChunkMetadata : ChatResponse<ChunkMetadata>
{
    /// <summary>
    /// One-sentence summary of the chunk's main idea
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Primary topic or subject matter of the chunk
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// List of 3-5 key terms or concepts
    /// </summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>
    /// Type of content section (e.g., Introduction, TechnicalDescription, Example, Configuration)
    /// </summary>
    public string SectionType { get; set; } = string.Empty;

    /// <summary>
    /// Importance score (1-10, where 10 is most important)
    /// Used for relevance boosting in retrieval
    /// </summary>
    public int ImportanceScore { get; set; } = 5;

    /// <summary>
    /// Topics or concepts this chunk references
    /// Enables relationship tracking across chunks
    /// </summary>
    public List<string> ReferencedTopics { get; set; } = new();

    public override ChunkMetadata _example => new()
    {
        Summary = "Describes JWT-based authentication implementation using AuthService",
        Topic = "Authentication System",
        Keywords = new List<string> { "JWT", "authentication", "tokens", "AuthService", "security" },
        SectionType = "TechnicalDescription",
        ImportanceScore = 8,
        ReferencedTopics = new List<string> { "Security", "API Design" }
    };

    public override string _signature =>
        "ChunkMetadata(Summary: string, Topic: string, Keywords: List<string>, SectionType: string, ImportanceScore: int, ReferencedTopics: List<string>)";
}

/// <summary>
/// Batch response for multiple chunks metadata extraction
/// More efficient than individual calls (reduces latency and can reduce costs)
/// </summary>
public class BatchChunkMetadata : ChatResponse<BatchChunkMetadata>
{
    /// <summary>
    /// List of metadata for each chunk, in the same order as the input
    /// </summary>
    public List<ChunkMetadata> Chunks { get; set; } = new();

    public override BatchChunkMetadata _example => new()
    {
        Chunks = new List<ChunkMetadata>
        {
            new ChunkMetadata
            {
                Summary = "Explains JWT authentication flow",
                Topic = "Authentication",
                Keywords = new List<string> { "JWT", "tokens", "auth" },
                SectionType = "TechnicalDescription",
                ImportanceScore = 8,
                ReferencedTopics = new List<string> { "Security" }
            },
            new ChunkMetadata
            {
                Summary = "Describes database configuration for PostgreSQL",
                Topic = "Database Setup",
                Keywords = new List<string> { "PostgreSQL", "database", "configuration" },
                SectionType = "Configuration",
                ImportanceScore = 7,
                ReferencedTopics = new List<string> { "Infrastructure" }
            }
        }
    };

    public override string _signature =>
        "BatchChunkMetadata(Chunks: List<ChunkMetadata>)";
}

/// <summary>
/// Supported section types for content classification
/// </summary>
public static class SectionTypes
{
    public const string Introduction = "Introduction";
    public const string TechnicalDescription = "TechnicalDescription";
    public const string Example = "Example";
    public const string CodeSnippet = "CodeSnippet";
    public const string Configuration = "Configuration";
    public const string Tutorial = "Tutorial";
    public const string Reference = "Reference";
    public const string Explanation = "Explanation";
    public const string Comparison = "Comparison";
    public const string BestPractice = "BestPractice";
    public const string Warning = "Warning";
    public const string Conclusion = "Conclusion";
    public const string Summary = "Summary";
    public const string Other = "Other";

    public static List<string> All => new()
    {
        Introduction, TechnicalDescription, Example, CodeSnippet, Configuration,
        Tutorial, Reference, Explanation, Comparison, BestPractice, Warning,
        Conclusion, Summary, Other
    };
}
