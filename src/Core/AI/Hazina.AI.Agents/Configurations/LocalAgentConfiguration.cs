namespace Hazina.AI.Agents.Configurations;

/// <summary>
/// Configuration for local-first autonomous agents with transparency and schema-driven UI.
/// Defines operational mode, indexation strategy, UI rendering rules, and approval workflows.
/// </summary>
public class LocalAgentConfiguration
{
    /// <summary>
    /// Enable transparency mode where all agent actions are visible and require explicit approval.
    /// When true, agent must present task breakdown before execution and wait for user approval.
    /// </summary>
    public bool TransparencyMode { get; set; } = true;

    /// <summary>
    /// Working directory for agent operations.
    /// Defaults to current directory if not specified.
    /// </summary>
    public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;

    /// <summary>
    /// Indexation strategy defining what local resources to index and how.
    /// </summary>
    public IndexationStrategy Indexation { get; set; } = new();

    /// <summary>
    /// UI schema mode controlling how agent can render user interfaces.
    /// </summary>
    public UISchemaMode UIMode { get; set; } = UISchemaMode.SchemaOnly;

    /// <summary>
    /// Approval workflow settings for risky operations.
    /// </summary>
    public ApprovalSettings Approval { get; set; } = new();
}

/// <summary>
/// Defines which local resources to index and at what depth.
/// Three phases: Structure (files/dirs), Content (code symbols), Semantic (embeddings).
/// </summary>
public class IndexationStrategy
{
    /// <summary>
    /// Phase A: Index file/directory structure (fast, <1s for 10K files).
    /// Captures paths, sizes, extensions, modification times.
    /// </summary>
    public bool EnablePhaseA { get; set; } = true;

    /// <summary>
    /// Phase B: Index file content and code structure (moderate, ~5-10s for typical project).
    /// Parses code for symbols, functions, classes, dependencies.
    /// Requires Phase A to be enabled.
    /// </summary>
    public bool EnablePhaseB { get; set; } = false;

    /// <summary>
    /// Phase C: Semantic indexation with embeddings (slow, ~30-60s for typical project).
    /// Generates embeddings for semantic search capabilities.
    /// Requires Phase B to be enabled.
    /// </summary>
    public bool EnablePhaseC { get; set; } = false;

    /// <summary>
    /// Maximum file size to index (bytes).
    /// Files larger than this are skipped to prevent memory issues.
    /// Default: 10 MB.
    /// </summary>
    public int MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Glob patterns for files/directories to exclude from indexation.
    /// Prevents indexing of build artifacts, dependencies, and version control directories.
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = new()
    {
        "node_modules/**",
        ".git/**",
        "bin/**",
        "obj/**",
        "*.min.js",
        "*.map",
        ".vs/**",
        ".vscode/**"
    };

    /// <summary>
    /// Re-index interval. How often to refresh the index.
    /// Null = manual re-indexation only.
    /// </summary>
    public TimeSpan? RefreshInterval { get; set; } = null;
}

/// <summary>
/// UI schema mode defining how strictly agent adheres to predefined component schemas.
/// </summary>
public enum UISchemaMode
{
    /// <summary>
    /// Agent can ONLY use pre-defined component schemas from registry.
    /// Most restrictive, highest safety.
    /// Production default.
    /// </summary>
    SchemaOnly,

    /// <summary>
    /// Agent can extend schemas with approved additional properties.
    /// Allows limited flexibility while maintaining core structure.
    /// Requires approval for schema extensions.
    /// </summary>
    SchemaExtended,

    /// <summary>
    /// Development/testing mode allowing arbitrary UI generation.
    /// NOT FOR PRODUCTION USE.
    /// Enables rapid iteration during component development.
    /// </summary>
    Development
}

/// <summary>
/// Approval workflow settings controlling when user approval is required.
/// </summary>
public class ApprovalSettings
{
    /// <summary>
    /// Require approval before executing write operations (file modifications, database changes).
    /// </summary>
    public bool RequireApprovalForWrite { get; set; } = true;

    /// <summary>
    /// Require approval before executing commands/scripts.
    /// </summary>
    public bool RequireApprovalForExecute { get; set; } = true;

    /// <summary>
    /// Require approval before admin-level operations (system config, permissions, destructive actions).
    /// </summary>
    public bool RequireApprovalForAdmin { get; set; } = true;

    /// <summary>
    /// Auto-approve timeout in seconds.
    /// If user doesn't respond within this time, action is auto-denied (not auto-approved).
    /// 0 = infinite wait (no timeout).
    /// </summary>
    public int AutoApproveTimeoutSeconds { get; set; } = 0;

    /// <summary>
    /// Allow batch approval for multiple similar operations.
    /// When true, user can approve/deny entire batch at once.
    /// </summary>
    public bool AllowBatchApproval { get; set; } = true;
}
