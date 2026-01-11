using System.ComponentModel.DataAnnotations;

namespace Hazina.AI.ContextEngineering.Configuration;

/// <summary>
/// Policy for configuring context packing and assembly
/// </summary>
public class PackingPolicy
{
    /// <summary>
    /// Maximum number of tokens in assembled context
    /// </summary>
    [Range(100, 100000)]
    public int MaxTokens { get; set; } = 12000;

    /// <summary>
    /// Sections to include in context (in order)
    /// Valid sections: "facts", "metadata", "tags", "chunks", "query"
    /// </summary>
    public List<string> Sections { get; set; } = new() { "facts", "metadata", "chunks", "query" };

    /// <summary>
    /// Whether to include source information for each section
    /// </summary>
    public bool IncludeSourceInfo { get; set; } = true;

    /// <summary>
    /// Whether to include relevance scores for each item
    /// </summary>
    public bool IncludeScores { get; set; } = true;

    /// <summary>
    /// Whether to include tags for each item
    /// </summary>
    public bool IncludeTags { get; set; } = true;

    /// <summary>
    /// Target language for final context (e.g., "en", "nl", "de", "auto")
    /// "auto" means detect from query or use system default
    /// </summary>
    public string TargetLanguage { get; set; } = "auto";

    /// <summary>
    /// Separator between sections
    /// </summary>
    public string SectionSeparator { get; set; } = "\n\n";

    /// <summary>
    /// Separator between items within a section
    /// </summary>
    public string ItemSeparator { get; set; } = "\n";

    /// <summary>
    /// Format for section headers (e.g., "[{0}]", "## {0}", "=== {0} ===")
    /// {0} will be replaced with section name
    /// </summary>
    public string SectionHeaderFormat { get; set; } = "[{0}]";

    /// <summary>
    /// Whether to trim content to fit within MaxTokens
    /// If false, throws exception if content exceeds limit
    /// </summary>
    public bool TrimToFit { get; set; } = true;

    /// <summary>
    /// Priority order for sections when trimming
    /// Sections later in this list will be trimmed first
    /// </summary>
    public List<string> TrimPriority { get; set; } = new() { "query", "facts", "metadata", "tags", "chunks" };

    /// <summary>
    /// Default packing policy (balanced, all sections included)
    /// </summary>
    public static PackingPolicy Default => new();

    /// <summary>
    /// Compact policy (minimal context, facts + query only)
    /// </summary>
    public static PackingPolicy Compact => new()
    {
        MaxTokens = 4000,
        Sections = new() { "facts", "query" },
        IncludeScores = false,
        IncludeTags = false
    };

    /// <summary>
    /// Comprehensive policy (maximum context, all sections)
    /// </summary>
    public static PackingPolicy Comprehensive => new()
    {
        MaxTokens = 32000,
        Sections = new() { "facts", "metadata", "tags", "chunks", "query" },
        IncludeSourceInfo = true,
        IncludeScores = true,
        IncludeTags = true
    };

    /// <summary>
    /// Validate the policy configuration
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (MaxTokens < 100)
        {
            errors.Add("MaxTokens must be at least 100");
        }

        if (!Sections.Any())
        {
            errors.Add("At least one section must be included");
        }

        var validSections = new HashSet<string> { "facts", "metadata", "tags", "chunks", "query" };
        var invalidSections = Sections.Where(s => !validSections.Contains(s)).ToList();
        if (invalidSections.Any())
        {
            errors.Add($"Invalid sections: {string.Join(", ", invalidSections)}");
        }

        if (string.IsNullOrWhiteSpace(TargetLanguage))
        {
            errors.Add("TargetLanguage must be specified");
        }

        return errors;
    }
}
