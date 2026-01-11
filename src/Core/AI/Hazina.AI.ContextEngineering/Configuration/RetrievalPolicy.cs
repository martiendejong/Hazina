using System.ComponentModel.DataAnnotations;

namespace Hazina.AI.ContextEngineering.Configuration;

/// <summary>
/// Policy for configuring retrieval behavior
/// </summary>
public class RetrievalPolicy
{
    /// <summary>
    /// Whether to use semantic (embedding-based) retrieval
    /// </summary>
    public bool SemanticEnabled { get; set; } = true;

    /// <summary>
    /// Number of results to retrieve from semantic search
    /// </summary>
    [Range(1, 100)]
    public int SemanticTopK { get; set; } = 8;

    /// <summary>
    /// Weight for semantic results in fusion (0.0-1.0)
    /// </summary>
    [Range(0.0, 1.0)]
    public double SemanticWeight { get; set; } = 0.6;

    /// <summary>
    /// Minimum similarity threshold for semantic results
    /// </summary>
    [Range(0.0, 1.0)]
    public double SemanticMinSimilarity { get; set; } = 0.7;

    /// <summary>
    /// Whether to use facts retrieval
    /// </summary>
    public bool FactsEnabled { get; set; } = true;

    /// <summary>
    /// Number of facts to retrieve
    /// </summary>
    [Range(1, 50)]
    public int FactsTopK { get; set; } = 5;

    /// <summary>
    /// Weight for facts in fusion (0.0-1.0)
    /// </summary>
    [Range(0.0, 1.0)]
    public double FactsWeight { get; set; } = 0.3;

    /// <summary>
    /// Whether to use metadata filtering retrieval
    /// </summary>
    public bool MetadataEnabled { get; set; } = true;

    /// <summary>
    /// Number of results to retrieve from metadata search
    /// </summary>
    [Range(1, 50)]
    public int MetadataTopK { get; set; } = 5;

    /// <summary>
    /// Weight for metadata results in fusion (0.0-1.0)
    /// </summary>
    [Range(0.0, 1.0)]
    public double MetadataWeight { get; set; } = 0.1;

    /// <summary>
    /// Optional metadata filters to apply
    /// </summary>
    public Dictionary<string, string>? MetadataFilters { get; set; }

    /// <summary>
    /// Optional tags to filter by
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Optional types to filter by
    /// </summary>
    public List<string>? Types { get; set; }

    /// <summary>
    /// Whether to use direct ID lookup
    /// </summary>
    public bool LookupEnabled { get; set; } = false;

    /// <summary>
    /// Optional specific IDs to retrieve
    /// </summary>
    public List<string>? LookupIds { get; set; }

    /// <summary>
    /// Default retrieval policy (balanced mix of all retrievers)
    /// </summary>
    public static RetrievalPolicy Default => new();

    /// <summary>
    /// Semantic-focused policy (heavy emphasis on embedding search)
    /// </summary>
    public static RetrievalPolicy SemanticFocused => new()
    {
        SemanticEnabled = true,
        SemanticTopK = 10,
        SemanticWeight = 0.8,
        FactsEnabled = true,
        FactsTopK = 3,
        FactsWeight = 0.2,
        MetadataEnabled = false
    };

    /// <summary>
    /// Facts-focused policy (heavy emphasis on compact facts)
    /// </summary>
    public static RetrievalPolicy FactsFocused => new()
    {
        SemanticEnabled = false,
        FactsEnabled = true,
        FactsTopK = 10,
        FactsWeight = 1.0,
        MetadataEnabled = false
    };

    /// <summary>
    /// Validate the policy configuration
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (!SemanticEnabled && !FactsEnabled && !MetadataEnabled && !LookupEnabled)
        {
            errors.Add("At least one retriever must be enabled");
        }

        var totalWeight = 0.0;
        if (SemanticEnabled) totalWeight += SemanticWeight;
        if (FactsEnabled) totalWeight += FactsWeight;
        if (MetadataEnabled) totalWeight += MetadataWeight;

        if (totalWeight <= 0)
        {
            errors.Add("Total weight of enabled retrievers must be > 0");
        }

        if (LookupEnabled && (LookupIds == null || !LookupIds.Any()))
        {
            errors.Add("LookupIds must be provided when LookupEnabled is true");
        }

        return errors;
    }
}
