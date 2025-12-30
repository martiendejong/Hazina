using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hazina.Agents.Tools.FileSystem;

/// <summary>
/// Tool for searching file contents with regex patterns
/// </summary>
public static class GrepTool
{
    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "grep",
            description: "Search for patterns in file contents using regex. Can search single file or recursively in directories.",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "pattern",
                    Type = "string",
                    Required = true,
                    Description = "Regex pattern to search for"
                },
                new()
                {
                    Name = "path",
                    Type = "string",
                    Required = false,
                    Description = "File or directory to search (default: current working directory)"
                },
                new()
                {
                    Name = "file_pattern",
                    Type = "string",
                    Required = false,
                    Description = "File pattern to filter (e.g., '*.cs', '*.txt'). Default: all files"
                },
                new()
                {
                    Name = "case_insensitive",
                    Type = "boolean",
                    Required = false,
                    Description = "Perform case-insensitive search (default: false)"
                },
                new()
                {
                    Name = "output_mode",
                    Type = "string",
                    Required = false,
                    Description = "Output mode: 'content' (show matching lines), 'files' (show matching file paths only), 'count' (show match counts). Default: 'content'"
                },
                new()
                {
                    Name = "max_results",
                    Type = "number",
                    Required = false,
                    Description = "Maximum number of results to return (default: 100)"
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

                    // Parse optional parameters
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

                    string filePattern = "*";
                    if (root.TryGetProperty("file_pattern", out var fpElement))
                    {
                        var fp = fpElement.GetString();
                        if (!string.IsNullOrWhiteSpace(fp))
                            filePattern = fp;
                    }

                    bool caseInsensitive = false;
                    if (root.TryGetProperty("case_insensitive", out var ciElement))
                    {
                        caseInsensitive = ciElement.GetBoolean();
                    }

                    string outputMode = "content";
                    if (root.TryGetProperty("output_mode", out var omElement))
                    {
                        var om = omElement.GetString();
                        if (!string.IsNullOrWhiteSpace(om))
                            outputMode = om.ToLower();
                    }

                    int maxResults = 100;
                    if (root.TryGetProperty("max_results", out var mrElement))
                    {
                        maxResults = mrElement.GetInt32();
                    }

                    // Validate path
                    if (!File.Exists(searchPath) && !Directory.Exists(searchPath))
                        return $"Error: Path not found: {searchPath}";

                    // Create regex
                    var regexOptions = RegexOptions.Compiled;
                    if (caseInsensitive)
                        regexOptions |= RegexOptions.IgnoreCase;

                    Regex regex;
                    try
                    {
                        regex = new Regex(pattern, regexOptions);
                    }
                    catch (Exception ex)
                    {
                        return $"Error: Invalid regex pattern: {ex.Message}";
                    }

                    // Get files to search
                    var filesToSearch = new List<string>();
                    if (File.Exists(searchPath))
                    {
                        filesToSearch.Add(searchPath);
                    }
                    else
                    {
                        filesToSearch.AddRange(
                            Directory.EnumerateFiles(searchPath, filePattern, SearchOption.AllDirectories)
                        );
                    }

                    // Search files
                    var results = new List<(string file, int lineNum, string line)>();
                    var matchingFiles = new HashSet<string>();
                    var fileCounts = new Dictionary<string, int>();

                    foreach (var file in filesToSearch)
                    {
                        try
                        {
                            var lines = await File.ReadAllLinesAsync(file, cancel);
                            int lineNum = 1;
                            int fileMatchCount = 0;

                            foreach (var line in lines)
                            {
                                if (regex.IsMatch(line))
                                {
                                    results.Add((file, lineNum, line));
                                    matchingFiles.Add(file);
                                    fileMatchCount++;

                                    if (results.Count >= maxResults && outputMode == "content")
                                        break;
                                }
                                lineNum++;
                            }

                            if (fileMatchCount > 0)
                                fileCounts[file] = fileMatchCount;

                            if (results.Count >= maxResults && outputMode == "content")
                                break;
                        }
                        catch
                        {
                            // Skip files that can't be read (binary, permissions, etc.)
                            continue;
                        }
                    }

                    // Format output based on mode
                    var output = new StringBuilder();

                    if (outputMode == "files")
                    {
                        output.AppendLine($"Found pattern in {matchingFiles.Count} file(s):");
                        output.AppendLine();
                        foreach (var file in matchingFiles.Take(maxResults))
                        {
                            var relativePath = Path.GetRelativePath(workingDirectory, file);
                            output.AppendLine(relativePath);
                        }
                    }
                    else if (outputMode == "count")
                    {
                        output.AppendLine($"Match counts by file:");
                        output.AppendLine();
                        foreach (var kvp in fileCounts.OrderByDescending(x => x.Value).Take(maxResults))
                        {
                            var relativePath = Path.GetRelativePath(workingDirectory, kvp.Key);
                            output.AppendLine($"{kvp.Value,6} matches: {relativePath}");
                        }
                    }
                    else // content
                    {
                        output.AppendLine($"Found {results.Count} match(es) for pattern '{pattern}':");
                        output.AppendLine();

                        string? lastFile = null;
                        foreach (var (file, lineNum, line) in results.Take(maxResults))
                        {
                            var relativePath = Path.GetRelativePath(workingDirectory, file);
                            if (relativePath != lastFile)
                            {
                                output.AppendLine();
                                output.AppendLine($"File: {relativePath}");
                                lastFile = relativePath;
                            }
                            output.AppendLine($"{lineNum,6}: {line.TrimEnd()}");
                        }
                    }

                    if (results.Count == 0 && matchingFiles.Count == 0)
                    {
                        return $"No matches found for pattern: {pattern}";
                    }

                    return output.ToString();
                }
                catch (Exception ex)
                {
                    return $"Error during grep search: {ex.Message}";
                }
            }
        );
    }
}
