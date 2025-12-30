using System.Text.Json;

namespace Hazina.Agents.Tools.FileSystem;

/// <summary>
/// Tool for reading file contents with optional line range support
/// </summary>
public static class ReadFileTool
{
    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "read_file",
            description: "Read contents of a file from the file system. Supports reading specific line ranges.",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "file_path",
                    Type = "string",
                    Required = true,
                    Description = "Absolute or relative path to the file to read"
                },
                new()
                {
                    Name = "offset",
                    Type = "number",
                    Required = false,
                    Description = "Line number to start reading from (1-based). Default: 1"
                },
                new()
                {
                    Name = "limit",
                    Type = "number",
                    Required = false,
                    Description = "Maximum number of lines to read. Default: read all lines"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    if (!root.TryGetProperty("file_path", out var filePathElement))
                    {
                        return "Error: file_path parameter is required";
                    }

                    var filePath = filePathElement.GetString();
                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        return "Error: file_path cannot be empty";
                    }

                    // Resolve path
                    var resolvedPath = Path.IsPathRooted(filePath)
                        ? filePath
                        : Path.GetFullPath(Path.Combine(workingDirectory, filePath));

                    if (!File.Exists(resolvedPath))
                    {
                        return $"Error: File not found: {resolvedPath}";
                    }

                    // Parse optional parameters
                    int offset = 1; // 1-based line numbers
                    int? limit = null;

                    if (root.TryGetProperty("offset", out var offsetElement))
                    {
                        offset = offsetElement.GetInt32();
                        if (offset < 1) offset = 1;
                    }

                    if (root.TryGetProperty("limit", out var limitElement))
                    {
                        limit = limitElement.GetInt32();
                    }

                    // Read file
                    var allLines = await File.ReadAllLinesAsync(resolvedPath, cancel);

                    // Apply line range
                    var startIndex = offset - 1; // Convert to 0-based
                    if (startIndex >= allLines.Length)
                    {
                        return $"Error: offset {offset} exceeds file length ({allLines.Length} lines)";
                    }

                    var linesToRead = limit.HasValue
                        ? allLines.Skip(startIndex).Take(limit.Value)
                        : allLines.Skip(startIndex);

                    // Format output with line numbers (cat -n style)
                    var result = new System.Text.StringBuilder();
                    result.AppendLine($"File: {resolvedPath}");
                    result.AppendLine($"Lines {offset}-{offset + linesToRead.Count() - 1} of {allLines.Length}");
                    result.AppendLine();

                    int lineNum = offset;
                    foreach (var line in linesToRead)
                    {
                        result.AppendLine($"{lineNum,6}→{line}");
                        lineNum++;
                    }

                    return result.ToString();
                }
                catch (Exception ex)
                {
                    return $"Error reading file: {ex.Message}";
                }
            }
        );
    }
}
