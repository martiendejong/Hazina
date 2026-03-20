/// <summary>
/// Pre-defined standard tool sets
/// </summary>
public static class StandardToolSets
{
    public static ToolSet CoreTools => new()
    {
        Id = "core",
        Name = "Core Tools",
        Description = "Essential read-only tools for basic operations",
        Category = "Core",
        Tags = new List<string> { "core", "safe", "readonly" },
        ToolNames = new List<string> { "list", "read", "relevancy" },
        EnabledByDefault = true,
        RequiresOptIn = false
    };

    public static ToolSet WriteTools => new()
    {
        Id = "write",
        Name = "Write Tools",
        Description = "Tools for modifying files and data",
        Category = "FileSystem",
        Tags = new List<string> { "write", "modify" },
        ToolNames = new List<string> { "write", "delete" },
        EnabledByDefault = false,
        RequiresOptIn = true,
        Dependencies = new List<string> { "core" }
    };

    public static ToolSet BuildTools => new()
    {
        Id = "build",
        Name = "Build Tools",
        Description = "Tools for building and testing code",
        Category = "Development",
        Tags = new List<string> { "build", "compile", "test" },
        ToolNames = new List<string> { "build", "build_dotnet", "build_quasar", "test_quasar", "dotnet", "npm" },
        EnabledByDefault = false,
        RequiresOptIn = true
    };

    public static ToolSet GitTools => new()
    {
        Id = "git",
        Name = "Git Tools",
        Description = "Version control operations",
        Category = "Development",
        Tags = new List<string> { "git", "vcs" },
        ToolNames = new List<string> { "git" },
        EnabledByDefault = false,
        RequiresOptIn = true
    };

    public static IReadOnlyList<ToolSet> GetAll()
    {
        return new List<ToolSet> { CoreTools, WriteTools, BuildTools, GitTools };
    }

    public static void RegisterAll(IToolSetManager manager)
    {
        foreach (var toolSet in GetAll())
        {
            manager.RegisterToolSet(toolSet);
        }
    }
}
