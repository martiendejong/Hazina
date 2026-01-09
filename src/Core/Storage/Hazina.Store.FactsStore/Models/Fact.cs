namespace Hazina.Store.FactsStore.Models;

/// <summary>
/// Represents a compact, relevant fact for context engineering.
/// Facts are language-agnostic symbolic or short NL representations.
/// </summary>
public class Fact
{
    /// <summary>
    /// Unique identifier for the fact
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Short content - either symbolic (e.g., "building_X_sensors=24")
    /// or minimal natural language (e.g., "Building X has 24 sensors")
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Fact type for filtering and categorization
    /// Examples: "concept", "entity", "rule", "state", "numeric", "enum"
    /// </summary>
    public string Type { get; set; } = "concept";

    /// <summary>
    /// Additional metadata for filtering and context
    /// Examples: { "entity": "building_X", "domain": "iot" }
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Tags for semantic grouping and relevance scoring
    /// Examples: ["iot", "sensors", "configuration"]
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// When the fact was created
    /// </summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the fact was last updated (null if never updated)
    /// </summary>
    public DateTime? Updated { get; set; }

    /// <summary>
    /// Optional embedding vector for semantic search
    /// Null if fact should only be retrieved via metadata/tags
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    /// Relevance score (0.0-1.0) for ranking
    /// Can be computed or manually assigned
    /// </summary>
    public double RelevanceScore { get; set; } = 1.0;
}
