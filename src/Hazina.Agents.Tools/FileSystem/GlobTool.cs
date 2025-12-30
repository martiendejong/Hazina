using System.Text.Json;

namespace Hazina.Agents.Tools.FileSystem;

/// <summary>
/// Tool for finding files by glob patterns
/// </summary>
public static class GlobTool
{
    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "glob",
            description: "Find files matching a glob pattern (e.g., '**/*.cs' for all C# files, '*.json' for JSON files in current dir)",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "pattern",
                    Type = "string",
                    Required = true,
                    Description = "Glob pattern to match files (e.g., '**/*.cs', 'src/**/*.txt', '*.json')"
                },
                new()
                {
                    Name = "path",
                    Type = "string",
                    Required = false,
                    Description = "Directory to search in (default: current working directory)"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    if (!root.TryGetProperty("pattern", out var patternElement))
                        return "Error: pattern parameter is required";

                    var pattern = patternElement.GetString();
                    if (string.IsNullOrWhiteSpace(pattern))
                        return "Error: pattern cannot be empty";

                    // Parse path
                    string searchPath = workingDirectory;
                    if (root.TryGetProperty("path", out var pathElement))
                    {
                        var p = pathElement.GetString();
                        if (!string.IsNullOrWhiteSpace(p))
                        {
                            searchPath = Path.IsPathRooted(p)
                                ? p
                                : Path.GetFullPath(Path.Combine(workingDirectory, p));
                        }
                    }

                    if (!Directory.Exists(searchPath))
                        return $"Error: Directory not found: {searchPath}";

                    // Use Directory.EnumerateFiles with pattern
                    var searchOption = pattern.Contains("**")
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly;

                    // Convert glob pattern to simple search pattern
                    // For ** patterns, we'll do recursive search and filter manually
                    string searchPattern = "*";
                    if (!pattern.Contains("**"))
                    {
                        searchPattern = pattern;
                    }

                    var files = Directory.EnumerateFiles(searchPath, searchPattern, searchOption);

                    // If pattern contains **, filter results
                    if (pattern.Contains("**"))
                    {
                        var regexPattern = ConvertGlobToRegex(pattern);
                        var regex = new System.Text.RegularExpressions.Regex(regexPattern);
                        files = files.Where(f =>
                        {
                            var relativePath = Path.GetRelativePath(searchPath, f);
                            return regex.IsMatch(relativePath.Replace("\\", "/"));
                        });
                    }

                    var matchedFiles = files.OrderBy(f => f).ToList();

                    if (matchedFiles.Count == 0)
                        return $"No files found matching pattern: {pattern}";

                    var result = new System.Text.StringBuilder();
                    result.AppendLine($"Found {matchedFiles.Count} file(s) matching '{pattern}' in {searchPath}:");
                    result.AppendLine();

                    foreach (var file in matchedFiles.Take(1000)) // Limit to 1000 files
                    {
                        var relativePath = Path.GetRelativePath(searchPath, file);
                        result.AppendLine(relativePath);
                    }

                    if (matchedFiles.Count > 1000)
                    {
                        result.AppendLine($"\n... and {matchedFiles.Count - 1000} more files (truncated)");
                    }

                    return result.ToString();
                }
                catch (Exception ex)
                {
                    return $"Error during glob search: {ex.Message}";
                }
            }
        );
    }

    private static string ConvertGlobToRegex(string glob)
    {
        var pattern = glob
            .Replace("\\", "/")
            .Replace(".", "\\.")
            .Replace("**", "DOUBLESTAR")
            .Replace("*", "[^/]*")
            .Replace("DOUBLESTAR", ".*")
            .Replace("?", ".");
        return "^" + pattern + "$";
    }
}
