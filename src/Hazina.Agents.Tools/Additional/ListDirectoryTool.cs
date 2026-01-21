using System.Text;
using System.Text.Json;

namespace Hazina.Agents.Tools.Additional;

/// <summary>
/// Tool for listing directory contents (cross-platform alternative to ls/dir)
/// </summary>
public static class ListDirectoryTool
{
    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "list_directory",
            description: "List contents of a directory with details (files, sizes, dates)",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "path",
                    Type = "string",
                    Required = false,
                    Description = "Directory path to list (default: current working directory)"
                },
                new()
                {
                    Name = "recursive",
                    Type = "boolean",
                    Required = false,
                    Description = "Include subdirectories recursively (default: false)"
                },
                new()
                {
                    Name = "max_depth",
                    Type = "number",
                    Required = false,
                    Description = "Maximum depth for recursive listing (default: 3)"
                },
                new()
                {
                    Name = "show_hidden",
                    Type = "boolean",
                    Required = false,
                    Description = "Show hidden files (starting with .) (default: false)"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    string dirPath = workingDirectory;
                    if (root.TryGetProperty("path", out var pathElement))
                    {
                        var p = pathElement.GetString();
                        if (!string.IsNullOrWhiteSpace(p))
                        {
                            dirPath = Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(workingDirectory, p));
                        }
                    }

                    bool recursive = false;
                    if (root.TryGetProperty("recursive", out var recursiveElement))
                    {
                        recursive = recursiveElement.GetBoolean();
                    }

                    int maxDepth = 3;
                    if (root.TryGetProperty("max_depth", out var maxDepthElement))
                    {
                        maxDepth = maxDepthElement.GetInt32();
                    }

                    bool showHidden = false;
                    if (root.TryGetProperty("show_hidden", out var showHiddenElement))
                    {
                        showHidden = showHiddenElement.GetBoolean();
                    }

                    if (!Directory.Exists(dirPath))
                        return $"Error: Directory not found: {dirPath}";

                    var result = new StringBuilder();
                    result.AppendLine($"Directory: {dirPath}");
                    result.AppendLine(new string('=', 60));

                    await ListDirectory(result, dirPath, 0, recursive ? maxDepth : 0, showHidden, cancel);

                    return result.ToString();
                }
                catch (Exception ex)
                {
                    return $"Error listing directory: {ex.Message}";
                }
            }
        );
    }

    private static Task ListDirectory(StringBuilder result, string path, int currentDepth, int maxDepth, bool showHidden, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();

        var indent = new string(' ', currentDepth * 2);

        try
        {
            // List directories first
            var directories = Directory.GetDirectories(path)
                .Select(d => new DirectoryInfo(d))
                .OrderBy(d => d.Name);

            foreach (var dir in directories)
            {
                if (!showHidden && dir.Name.StartsWith("."))
                    continue;

                var attrs = "";
                if ((dir.Attributes & FileAttributes.Hidden) != 0) attrs += "[H]";

                result.AppendLine($"{indent}[DIR] {dir.Name}/ {attrs}");

                if (currentDepth < maxDepth)
                {
                    ListDirectory(result, dir.FullName, currentDepth + 1, maxDepth, showHidden, cancel);
                }
            }

            // Then list files
            var files = Directory.GetFiles(path)
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.Name);

            foreach (var file in files)
            {
                if (!showHidden && file.Name.StartsWith("."))
                    continue;

                var size = FormatFileSize(file.Length);
                var modified = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm");

                result.AppendLine($"{indent}{file.Name,-40} {size,10} {modified}");
            }
        }
        catch (UnauthorizedAccessException)
        {
            result.AppendLine($"{indent}[ACCESS DENIED]");
        }

        return Task.CompletedTask;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}
