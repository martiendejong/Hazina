using System.Text.Json;

namespace Hazina.Agents.Tools.FileSystem;

/// <summary>
/// Tool for writing or creating files
/// </summary>
public static class WriteFileTool
{
    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "write_file",
            description: "Write content to a file. Creates new file or overwrites existing file. Creates directories if needed.",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "file_path",
                    Type = "string",
                    Required = true,
                    Description = "Path to the file to write (absolute or relative)"
                },
                new()
                {
                    Name = "content",
                    Type = "string",
                    Required = true,
                    Description = "Content to write to the file"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    if (!root.TryGetProperty("file_path", out var filePathElement))
                        return "Error: file_path parameter is required";
                    if (!root.TryGetProperty("content", out var contentElement))
                        return "Error: content parameter is required";

                    var filePath = filePathElement.GetString();
                    var content = contentElement.GetString();

                    if (string.IsNullOrWhiteSpace(filePath))
                        return "Error: file_path cannot be empty";
                    if (content == null)
                        return "Error: content cannot be null";

                    // Resolve path
                    var resolvedPath = Path.IsPathRooted(filePath)
                        ? filePath
                        : Path.GetFullPath(Path.Combine(workingDirectory, filePath));

                    // Create directory if doesn't exist
                    var directory = Path.GetDirectoryName(resolvedPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Write file
                    await File.WriteAllTextAsync(resolvedPath, content, cancel);

                    var fileInfo = new FileInfo(resolvedPath);
                    var lineCount = content.Split('\n').Length;

                    return $"Success: Wrote {fileInfo.Length} bytes ({lineCount} lines) to {resolvedPath}";
                }
                catch (Exception ex)
                {
                    return $"Error writing file: {ex.Message}";
                }
            }
        );
    }
}
