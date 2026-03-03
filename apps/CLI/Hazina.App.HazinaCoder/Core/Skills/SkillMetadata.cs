using System.Collections.Generic;

namespace Hazina.App.HazinaCoder.Core.Skills;

/// <summary>
/// Metadata for auto-discoverable skills defined in markdown files.
/// Skills are discovered at startup from markdown files with YAML frontmatter.
/// </summary>
/// <remarks>
/// Improvement #21: Auto-Discoverable Skills
///
/// Format:
/// ---
/// name: skill-name
/// description: Brief description
/// triggers: [keyword1, keyword2]
/// parameters:
///   - name: param1
///     type: string
///     required: true
/// examples:
///   - "Example usage 1"
/// ---
///
/// # Skill Implementation
/// Rest of markdown is skill documentation
/// </remarks>
public class SkillMetadata
{
    /// <summary>
    /// Unique skill identifier (kebab-case)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Keywords that trigger this skill
    /// </summary>
    public List<string> Triggers { get; set; } = new();

    /// <summary>
    /// Skill parameters
    /// </summary>
    public List<SkillParameter> Parameters { get; set; } = new();

    /// <summary>
    /// Example usage strings
    /// </summary>
    public List<string> Examples { get; set; } = new();

    /// <summary>
    /// Full markdown content (excluding frontmatter)
    /// </summary>
    public string Documentation { get; set; } = string.Empty;

    /// <summary>
    /// File path where skill was discovered
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Skill version (from frontmatter or file timestamp)
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Dependencies on other skills
    /// </summary>
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// Parameter definition for a skill
/// </summary>
public class SkillParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public string Description { get; set; } = string.Empty;
}
