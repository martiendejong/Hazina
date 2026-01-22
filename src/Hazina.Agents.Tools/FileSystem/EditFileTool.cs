using System.Text;
using System.Text.Json;

namespace Hazina.Agents.Tools.FileSystem;

/// <summary>
/// Tool for making surgical edits to files with exact string replacement
/// Improvements: Backup files before edit, diff preview in output
/// </summary>
public static class EditFileTool
{
    // Improvement #4: Enable backups by default (can be disabled via config)
    public static bool EnableBackups { get; set; } = true;

    // Improvement #3: Enable diff preview in output
    public static bool ShowDiffPreview { get; set; } = true;

    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "edit_file",
            description: "Make precise edits to existing files by replacing exact string matches. The old_string must match exactly (including whitespace and indentation).",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "file_path",
                    Type = "string",
                    Required = true,
                    Description = "Path to the file to edit"
                },
                new()
                {
                    Name = "old_string",
                    Type = "string",
                    Required = true,
                    Description = "Exact string to replace (must match precisely including whitespace)"
                },
                new()
                {
                    Name = "new_string",
                    Type = "string",
                    Required = true,
                    Description = "New string to insert in place of old_string"
                },
                new()
                {
                    Name = "replace_all",
                    Type = "boolean",
                    Required = false,
                    Description = "Replace all occurrences (default: false, only first occurrence)"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    // Required parameters
                    if (!root.TryGetProperty("file_path", out var filePathElement))
                        return "Error: file_path parameter is required";
                    if (!root.TryGetProperty("old_string", out var oldStringElement))
                        return "Error: old_string parameter is required";
                    if (!root.TryGetProperty("new_string", out var newStringElement))
                        return "Error: new_string parameter is required";

                    var filePath = filePathElement.GetString();
                    var oldString = oldStringElement.GetString();
                    var newString = newStringElement.GetString();

                    if (string.IsNullOrWhiteSpace(filePath))
                        return "Error: file_path cannot be empty";
                    if (oldString == null)
                        return "Error: old_string cannot be null";
                    if (newString == null)
                        return "Error: new_string cannot be null";

                    // Resolve path
                    var resolvedPath = Path.IsPathRooted(filePath)
                        ? filePath
                        : Path.GetFullPath(Path.Combine(workingDirectory, filePath));

                    if (!File.Exists(resolvedPath))
                        return $"Error: File not found: {resolvedPath}";

                    // Parse optional replace_all
                    bool replaceAll = false;
                    if (root.TryGetProperty("replace_all", out var replaceAllElement))
                    {
                        replaceAll = replaceAllElement.GetBoolean();
                    }

                    // Read file
                    var content = await File.ReadAllTextAsync(resolvedPath, cancel);

                    // Check if old_string exists
                    if (!content.Contains(oldString))
                    {
                        return $"Error: old_string not found in file. Make sure the string matches exactly including whitespace.";
                    }

                    // Improvement #4: Create backup before editing
                    string? backupPath = null;
                    if (EnableBackups)
                    {
                        backupPath = resolvedPath + ".bak";
                        try
                        {
                            await File.WriteAllTextAsync(backupPath, content, cancel);
                        }
                        catch
                        {
                            // Non-fatal: continue without backup if it fails
                            backupPath = null;
                        }
                    }

                    // Perform replacement
                    string newContent;
                    int replacementCount;

                    if (replaceAll)
                    {
                        var originalContent = content;
                        newContent = content.Replace(oldString, newString);
                        replacementCount = (originalContent.Length - newContent.Length +
                                           (newString.Length - oldString.Length) * CountOccurrences(originalContent, oldString))
                                           / (oldString.Length - newString.Length + 1);
                        // Simple count: number of times old_string appears
                        replacementCount = CountOccurrences(originalContent, oldString);
                    }
                    else
                    {
                        // Replace only first occurrence
                        var index = content.IndexOf(oldString);
                        newContent = content.Substring(0, index) + newString + content.Substring(index + oldString.Length);
                        replacementCount = 1;
                    }

                    // Write back
                    await File.WriteAllTextAsync(resolvedPath, newContent, cancel);

                    // Improvement #3: Build diff preview
                    var result = new StringBuilder();
                    result.AppendLine($"Success: Replaced {replacementCount} occurrence(s) in {resolvedPath}");
                    result.AppendLine($"Old string length: {oldString.Length} chars");
                    result.AppendLine($"New string length: {newString.Length} chars");

                    if (backupPath != null)
                    {
                        result.AppendLine($"Backup created: {backupPath}");
                    }

                    if (ShowDiffPreview)
                    {
                        result.AppendLine();
                        result.AppendLine("--- Diff Preview ---");
                        result.AppendLine(GenerateDiffPreview(oldString, newString));
                    }

                    return result.ToString();
                }
                catch (Exception ex)
                {
                    return $"Error editing file: {ex.Message}";
                }
            }
        );
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    /// <summary>
    /// Improvement #3: Generate a simple diff preview showing old vs new content
    /// </summary>
    private static string GenerateDiffPreview(string oldString, string newString)
    {
        var sb = new StringBuilder();

        // Split into lines for comparison
        var oldLines = oldString.Split('\n');
        var newLines = newString.Split('\n');

        // Show removed lines (from old)
        foreach (var line in oldLines.Take(10)) // Limit to 10 lines
        {
            var trimmedLine = line.Length > 80 ? line.Substring(0, 77) + "..." : line;
            sb.AppendLine($"- {trimmedLine.TrimEnd('\r')}");
        }
        if (oldLines.Length > 10)
        {
            sb.AppendLine($"  ... ({oldLines.Length - 10} more lines removed)");
        }

        sb.AppendLine();

        // Show added lines (from new)
        foreach (var line in newLines.Take(10)) // Limit to 10 lines
        {
            var trimmedLine = line.Length > 80 ? line.Substring(0, 77) + "..." : line;
            sb.AppendLine($"+ {trimmedLine.TrimEnd('\r')}");
        }
        if (newLines.Length > 10)
        {
            sb.AppendLine($"  ... ({newLines.Length - 10} more lines added)");
        }

        return sb.ToString();
    }
}
