using System.Text.Json.Serialization;

namespace Hazina.CLI.Models;

/// <summary>
/// Rich metadata for a document chunk enabling advanced retrieval features.
/// </summary>
public class ChunkMetadata
{
    /// <summary>
    /// The actual text content of the chunk.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    /// <summary>
    /// Relative file path from the store root.
    /// </summary>
    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    /// <summary>
    /// Starting line number in the source file (1-based).
    /// </summary>
    [JsonPropertyName("line_start")]
    public int LineStart { get; set; }

    /// <summary>
    /// Ending line number in the source file (1-based).
    /// </summary>
    [JsonPropertyName("line_end")]
    public int LineEnd { get; set; }

    /// <summary>
    /// Tags for filtering (e.g., "controller", "service", "auth", "frontend").
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Programming language detected from file extension.
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    /// <summary>
    /// Symbols defined in this chunk (classes, methods, interfaces).
    /// </summary>
    [JsonPropertyName("symbols")]
    public List<string> Symbols { get; set; } = new();

    /// <summary>
    /// Symbols referenced/called from this chunk.
    /// </summary>
    [JsonPropertyName("references")]
    public List<string> References { get; set; } = new();

    /// <summary>
    /// Architectural layer (e.g., "api", "domain", "infrastructure", "presentation").
    /// </summary>
    [JsonPropertyName("layer")]
    public string Layer { get; set; } = "";

    /// <summary>
    /// File extension (e.g., ".cs", ".tsx").
    /// </summary>
    [JsonPropertyName("extension")]
    public string Extension { get; set; } = "";

    /// <summary>
    /// When this chunk was indexed.
    /// </summary>
    [JsonPropertyName("indexed_at")]
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Helper class for auto-detecting tags from file paths and content.
/// </summary>
public static class AutoTagger
{
    private static readonly Dictionary<string, string[]> PathPatternTags = new()
    {
        { "controller", new[] { "controller", "api", "backend" } },
        { "controllers", new[] { "controller", "api", "backend" } },
        { "service", new[] { "service", "backend" } },
        { "services", new[] { "service", "backend" } },
        { "model", new[] { "model", "entity" } },
        { "models", new[] { "model", "entity" } },
        { "dto", new[] { "dto", "model" } },
        { "dtos", new[] { "dto", "model" } },
        { "repository", new[] { "repository", "data", "backend" } },
        { "repositories", new[] { "repository", "data", "backend" } },
        { "middleware", new[] { "middleware", "infrastructure" } },
        { "components", new[] { "component", "frontend", "ui" } },
        { "hooks", new[] { "hook", "frontend", "react" } },
        { "pages", new[] { "page", "frontend", "ui" } },
        { "utils", new[] { "utility", "helper" } },
        { "helpers", new[] { "utility", "helper" } },
        { "test", new[] { "test" } },
        { "tests", new[] { "test" } },
        { "spec", new[] { "test", "spec" } },
        { "migrations", new[] { "migration", "database" } },
        { "infrastructure", new[] { "infrastructure" } },
        { "domain", new[] { "domain" } },
        { "core", new[] { "core" } },
        { "api", new[] { "api" } },
        { "web", new[] { "web" } },
    };

    private static readonly Dictionary<string, string[]> ExtensionTags = new()
    {
        { ".cs", new[] { "csharp", "backend" } },
        { ".tsx", new[] { "typescript", "react", "frontend" } },
        { ".ts", new[] { "typescript", "frontend" } },
        { ".jsx", new[] { "javascript", "react", "frontend" } },
        { ".js", new[] { "javascript", "frontend" } },
        { ".py", new[] { "python" } },
        { ".go", new[] { "golang" } },
        { ".rs", new[] { "rust" } },
        { ".java", new[] { "java", "backend" } },
        { ".kt", new[] { "kotlin", "backend" } },
        { ".sql", new[] { "sql", "database" } },
        { ".css", new[] { "css", "frontend", "styling" } },
        { ".scss", new[] { "scss", "frontend", "styling" } },
        { ".html", new[] { "html", "frontend" } },
        { ".md", new[] { "markdown", "documentation" } },
        { ".json", new[] { "json", "config" } },
        { ".yaml", new[] { "yaml", "config" } },
        { ".yml", new[] { "yaml", "config" } },
        { ".xml", new[] { "xml", "config" } },
    };

    private static readonly Dictionary<string, string[]> ContentPatternTags = new()
    {
        { "[Authorize", new[] { "auth", "security" } },
        { "[AllowAnonymous", new[] { "auth", "public" } },
        { "[ApiController]", new[] { "controller", "api" } },
        { "[HttpGet", new[] { "api", "http-get" } },
        { "[HttpPost", new[] { "api", "http-post" } },
        { "[HttpPut", new[] { "api", "http-put" } },
        { "[HttpDelete", new[] { "api", "http-delete" } },
        { "DbContext", new[] { "database", "ef-core" } },
        { "DbSet<", new[] { "database", "ef-core" } },
        { "async Task", new[] { "async" } },
        { "ILogger", new[] { "logging" } },
        { "try {", new[] { "error-handling" } },
        { "catch (", new[] { "error-handling" } },
        { "useState", new[] { "react", "hook", "state" } },
        { "useEffect", new[] { "react", "hook", "effect" } },
        { "useMemo", new[] { "react", "hook", "memo" } },
        { "useCallback", new[] { "react", "hook", "callback" } },
        { "useContext", new[] { "react", "hook", "context" } },
        { "useReducer", new[] { "react", "hook", "reducer" } },
        { "fetch(", new[] { "api-call", "http" } },
        { "axios", new[] { "api-call", "http" } },
        { "HttpClient", new[] { "api-call", "http" } },
        { "interface I", new[] { "interface" } },
        { "abstract class", new[] { "abstract" } },
        { "sealed class", new[] { "sealed" } },
        { "record ", new[] { "record" } },
        { "enum ", new[] { "enum" } },
    };

    /// <summary>
    /// Auto-detect tags from file path.
    /// </summary>
    public static HashSet<string> GetTagsFromPath(string relativePath)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pathLower = relativePath.ToLowerInvariant().Replace('\\', '/');
        var parts = pathLower.Split('/');

        foreach (var part in parts)
        {
            if (PathPatternTags.TryGetValue(part, out var pathTags))
            {
                foreach (var tag in pathTags)
                    tags.Add(tag);
            }
        }

        return tags;
    }

    /// <summary>
    /// Auto-detect tags from file extension.
    /// </summary>
    public static HashSet<string> GetTagsFromExtension(string extension)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (ExtensionTags.TryGetValue(extension.ToLowerInvariant(), out var extTags))
        {
            foreach (var tag in extTags)
                tags.Add(tag);
        }

        return tags;
    }

    /// <summary>
    /// Auto-detect tags from content patterns.
    /// </summary>
    public static HashSet<string> GetTagsFromContent(string content)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (pattern, patternTags) in ContentPatternTags)
        {
            if (content.Contains(pattern, StringComparison.Ordinal))
            {
                foreach (var tag in patternTags)
                    tags.Add(tag);
            }
        }

        return tags;
    }

    /// <summary>
    /// Detect programming language from file extension.
    /// </summary>
    public static string GetLanguage(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".tsx" or ".ts" => "typescript",
            ".jsx" or ".js" => "javascript",
            ".py" => "python",
            ".go" => "go",
            ".rs" => "rust",
            ".java" => "java",
            ".kt" => "kotlin",
            ".rb" => "ruby",
            ".php" => "php",
            ".swift" => "swift",
            ".c" or ".h" => "c",
            ".cpp" or ".hpp" or ".cc" => "cpp",
            ".sql" => "sql",
            ".html" or ".htm" => "html",
            ".css" => "css",
            ".scss" => "scss",
            ".md" => "markdown",
            ".json" => "json",
            ".yaml" or ".yml" => "yaml",
            ".xml" => "xml",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Detect architectural layer from file path.
    /// </summary>
    public static string GetLayer(string relativePath)
    {
        var pathLower = relativePath.ToLowerInvariant().Replace('\\', '/');

        if (pathLower.Contains("/controller") || pathLower.Contains("/api/"))
            return "api";
        if (pathLower.Contains("/service") || pathLower.Contains("/domain/"))
            return "domain";
        if (pathLower.Contains("/infrastructure/") || pathLower.Contains("/data/") || pathLower.Contains("/repository"))
            return "infrastructure";
        if (pathLower.Contains("/component") || pathLower.Contains("/page") || pathLower.Contains("/view"))
            return "presentation";
        if (pathLower.Contains("/test") || pathLower.Contains(".test.") || pathLower.Contains(".spec."))
            return "test";
        if (pathLower.Contains("/shared/") || pathLower.Contains("/common/"))
            return "shared";

        return "unknown";
    }

    /// <summary>
    /// Get all auto-detected tags for a file and its content.
    /// </summary>
    public static HashSet<string> GetAllTags(string relativePath, string extension, string content)
    {
        var tags = GetTagsFromPath(relativePath);

        foreach (var tag in GetTagsFromExtension(extension))
            tags.Add(tag);

        foreach (var tag in GetTagsFromContent(content))
            tags.Add(tag);

        return tags;
    }
}
