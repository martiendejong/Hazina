using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hazina.AI.ContextEngineering.Configuration;

/// <summary>
/// Root configuration for Context Engineering system
/// Combines retrieval, scoring, and packing policies
/// </summary>
public class ContextEngineConfig
{
    /// <summary>
    /// Policy for retrieval behavior
    /// </summary>
    public RetrievalPolicy RetrievalPolicy { get; set; } = RetrievalPolicy.Default;

    /// <summary>
    /// Policy for scoring and fusion
    /// </summary>
    public ScoringPolicy ScoringPolicy { get; set; } = ScoringPolicy.Default;

    /// <summary>
    /// Policy for context packing
    /// </summary>
    public PackingPolicy PackingPolicy { get; set; } = PackingPolicy.Default;

    /// <summary>
    /// Final number of results to return after fusion
    /// </summary>
    public int FinalTopK { get; set; } = 10;

    /// <summary>
    /// Optional name for this configuration (for logging/debugging)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Optional description of this configuration
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Default configuration (balanced mix)
    /// </summary>
    public static ContextEngineConfig Default => new()
    {
        Name = "Default",
        Description = "Balanced mix of all retrievers with weighted fusion"
    };

    /// <summary>
    /// Semantic-focused configuration
    /// </summary>
    public static ContextEngineConfig SemanticFocused => new()
    {
        Name = "Semantic Focused",
        Description = "Heavy emphasis on embedding-based semantic search",
        RetrievalPolicy = RetrievalPolicy.SemanticFocused,
        ScoringPolicy = ScoringPolicy.Default,
        PackingPolicy = PackingPolicy.Default
    };

    /// <summary>
    /// Facts-focused configuration
    /// </summary>
    public static ContextEngineConfig FactsFocused => new()
    {
        Name = "Facts Focused",
        Description = "Heavy emphasis on compact, relevant facts",
        RetrievalPolicy = RetrievalPolicy.FactsFocused,
        ScoringPolicy = ScoringPolicy.Default,
        PackingPolicy = PackingPolicy.Compact
    };

    /// <summary>
    /// Tag-focused configuration
    /// </summary>
    public static ContextEngineConfig TagFocused => new()
    {
        Name = "Tag Focused",
        Description = "Optimized for tag-based filtering and relevance",
        RetrievalPolicy = RetrievalPolicy.Default,
        ScoringPolicy = ScoringPolicy.TagFocused,
        PackingPolicy = PackingPolicy.Default
    };

    /// <summary>
    /// Recency-focused configuration
    /// </summary>
    public static ContextEngineConfig RecencyFocused => new()
    {
        Name = "Recency Focused",
        Description = "Prioritizes recent content over older content",
        RetrievalPolicy = RetrievalPolicy.Default,
        ScoringPolicy = ScoringPolicy.RecencyFocused,
        PackingPolicy = PackingPolicy.Default
    };

    /// <summary>
    /// Compact configuration (minimal tokens)
    /// </summary>
    public static ContextEngineConfig Compact => new()
    {
        Name = "Compact",
        Description = "Minimal context with facts and query only",
        RetrievalPolicy = RetrievalPolicy.FactsFocused,
        ScoringPolicy = ScoringPolicy.Default,
        PackingPolicy = PackingPolicy.Compact,
        FinalTopK = 5
    };

    /// <summary>
    /// Comprehensive configuration (maximum context)
    /// </summary>
    public static ContextEngineConfig Comprehensive => new()
    {
        Name = "Comprehensive",
        Description = "Maximum context with all sections included",
        RetrievalPolicy = RetrievalPolicy.Default,
        ScoringPolicy = ScoringPolicy.Default,
        PackingPolicy = PackingPolicy.Comprehensive,
        FinalTopK = 20
    };

    /// <summary>
    /// Validate the entire configuration
    /// </summary>
    /// <returns>List of validation errors (empty if valid)</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        errors.AddRange(RetrievalPolicy.Validate());
        errors.AddRange(ScoringPolicy.Validate());
        errors.AddRange(PackingPolicy.Validate());

        if (FinalTopK < 1 || FinalTopK > 100)
        {
            errors.Add("FinalTopK must be between 1 and 100");
        }

        return errors;
    }

    /// <summary>
    /// Serialize configuration to JSON
    /// </summary>
    public string ToJson(bool indented = true)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Deserialize configuration from JSON
    /// </summary>
    public static ContextEngineConfig FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        return JsonSerializer.Deserialize<ContextEngineConfig>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize configuration");
    }

    /// <summary>
    /// Load configuration from file
    /// </summary>
    public static ContextEngineConfig FromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return FromJson(json);
    }

    /// <summary>
    /// Save configuration to file
    /// </summary>
    public void ToFile(string filePath, bool indented = true)
    {
        var json = ToJson(indented);
        File.WriteAllText(filePath, json);
    }
}
