namespace Hazina.LLMs.Classes.ToolSets;

/// <summary>
/// Pre-defined standard tool sets
/// </summary>
public static class StandardToolSets
{
    /// <summary>
    /// Core tools that are safe and commonly needed
    /// </summary>
    public static ToolSet CoreTools => new()
    {
        Id = "core",
        Name = "Core Tools",
        Description = "Essential read-only tools for basic operations",
        Category = "Core",
        Version = "1.0.0",
        Tags = new List<string> { "core", "safe", "readonly" },
        ToolNames = new List<string>
        {
            "list",
            "read",
            "relevancy"
        },
        EnabledByDefault = true,
        RequiresOptIn = false
    };

    /// <summary>
    /// File system write operations
    /// </summary>
    public static ToolSet WriteTools => new()
    {
        Id = "write",
        Name = "Write Tools",
        Description = "Tools for modifying files and data",
        Category = "FileSystem",
        Version = "1.0.0",
        Tags = new List<string> { "write", "modify", "dangerous" },
        ToolNames = new List<string>
        {
            "write",
            "delete"
        },
        EnabledByDefault = false,
        RequiresOptIn = true,
        Dependencies = new List<string> { "core" }
    };

    /// <summary>
    /// Build and compilation tools
    /// </summary>
    public static ToolSet BuildTools => new()
    {
        Id = "build",
        Name = "Build Tools",
        Description = "Tools for building and testing code",
        Category = "Development",
        Version = "1.0.0",
        Tags = new List<string> { "build", "compile", "test" },
        ToolNames = new List<string>
        {
            "build",
            "build_dotnet",
            "build_quasar",
            "test_quasar",
            "dotnet",
            "npm"
        },
        EnabledByDefault = false,
        RequiresOptIn = true
    };

    /// <summary>
    /// Version control tools
    /// </summary>
    public static ToolSet GitTools => new()
    {
        Id = "git",
        Name = "Git Tools",
        Description = "Version control operations",
        Category = "Development",
        Version = "1.0.0",
        Tags = new List<string> { "git", "vcs", "development" },
        ToolNames = new List<string>
        {
            "git"
        },
        EnabledByDefault = false,
        RequiresOptIn = true
    };

    /// <summary>
    /// Email tools
    /// </summary>
    public static ToolSet EmailTools => new()
    {
        Id = "email",
        Name = "Email Tools",
        Description = "Email reading and sending capabilities",
        Category = "Communication",
        Version = "1.0.0",
        Tags = new List<string> { "email", "communication" },
        ToolNames = new List<string>
        {
            "email_send",
            "email_list_inbox",
            "email_read",
            "email_create_folder",
            "email_move",
            "email_list_folders"
        },
        EnabledByDefault = false,
        RequiresOptIn = true
    };

    /// <summary>
    /// WordPress tools
    /// </summary>
    public static ToolSet WordPressTools => new()
    {
        Id = "wordpress",
        Name = "WordPress Tools",
        Description = "WordPress CLI integration",
        Category = "CMS",
        Version = "1.0.0",
        Tags = new List<string> { "wordpress", "cms", "web" },
        ToolNames = new List<string>
        {
            "wordpress_cli"
        },
        EnabledByDefault = false,
        RequiresOptIn = true
    };

    /// <summary>
    /// BigQuery database tools
    /// </summary>
    public static ToolSet BigQueryTools => new()
    {
        Id = "bigquery",
        Name = "BigQuery Tools",
        Description = "Google BigQuery database operations",
        Category = "Database",
        Version = "1.0.0",
        Tags = new List<string> { "bigquery", "database", "google" },
        ToolNames = new List<string>
        {
            "bigquery_datasets",
            "query_tables",
            "query_tablefields",
            "query_bigquery"
        },
        EnabledByDefault = false,
        RequiresOptIn = true
    };

    /// <summary>
    /// Agent orchestration tools
    /// </summary>
    public static ToolSet AgentTools => new()
    {
        Id = "agents",
        Name = "Agent Tools",
        Description = "Multi-agent orchestration and delegation",
        Category = "Agents",
        Version = "1.0.0",
        Tags = new List<string> { "agents", "orchestration", "delegation" },
        ToolNames = new List<string>
        {
            "custom_agent_getstores",
            "custom_agent_run",
            "custom_agent_write"
        },
        EnabledByDefault = false,
        RequiresOptIn = true,
        Dependencies = new List<string> { "core" }
    };

    /// <summary>
    /// Get all standard tool sets
    /// </summary>
    public static IReadOnlyList<ToolSet> GetAll()
    {
        return new List<ToolSet>
        {
            CoreTools,
            WriteTools,
            BuildTools,
            GitTools,
            EmailTools,
            WordPressTools,
            BigQueryTools,
            AgentTools
        };
    }

    /// <summary>
    /// Register all standard tool sets with a manager
    /// </summary>
    public static void RegisterAll(IToolSetManager manager)
    {
        foreach (var toolSet in GetAll())
        {
            manager.RegisterToolSet(toolSet);
        }
    }
}
