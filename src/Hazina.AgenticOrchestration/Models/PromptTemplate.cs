namespace Hazina.AgenticOrchestration.Models;

/// <summary>
/// Predefined prompt template for starting Claude Code terminal sessions.
/// Allows users to save frequently-used prompts and auto-execute them on session start.
/// </summary>
public class PromptTemplate
{
    /// <summary>
    /// Unique identifier for the template
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Display name for the template (e.g., "Debug Frontend Build", "Run Tests")
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Optional description of what this template does
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The actual prompt text to send to Claude Code terminal
    /// </summary>
    public string PromptText { get; set; } = "";

    /// <summary>
    /// Optional working directory to use when starting the session
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Optional environment variables to set (key-value pairs)
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Whether to auto-submit the prompt immediately after session loads
    /// (true = auto-execute, false = show prompt but wait for user to press Enter)
    /// </summary>
    public bool AutoExecute { get; set; } = true;

    /// <summary>
    /// Optional delay in milliseconds before auto-executing (default 1000ms to allow Claude to load)
    /// </summary>
    public int AutoExecuteDelayMs { get; set; } = 1000;

    /// <summary>
    /// Optional category/tag for organizing templates (e.g., "debugging", "testing", "deployment")
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Icon name for UI display (e.g., "bug", "test-tube", "rocket")
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// When this template was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this template was last used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// How many times this template has been used
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// Whether this template is a favorite (for quick access)
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Display order (for sorting in UI)
    /// </summary>
    public int SortOrder { get; set; }
}
