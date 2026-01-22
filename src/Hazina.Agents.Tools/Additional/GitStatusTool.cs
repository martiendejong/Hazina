using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Hazina.Agents.Tools.Additional;

/// <summary>
/// Tool for getting structured git status information
/// </summary>
public static class GitStatusTool
{
    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "git_status",
            description: "Get structured git status including branch, changes, and recent commits",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "path",
                    Type = "string",
                    Required = false,
                    Description = "Repository path (default: current working directory)"
                },
                new()
                {
                    Name = "include_diff",
                    Type = "boolean",
                    Required = false,
                    Description = "Include diff of staged changes (default: false)"
                },
                new()
                {
                    Name = "recent_commits",
                    Type = "number",
                    Required = false,
                    Description = "Number of recent commits to show (default: 5)"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    string repoPath = workingDirectory;
                    if (root.TryGetProperty("path", out var pathElement))
                    {
                        var p = pathElement.GetString();
                        if (!string.IsNullOrWhiteSpace(p))
                        {
                            repoPath = Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(workingDirectory, p));
                        }
                    }

                    bool includeDiff = false;
                    if (root.TryGetProperty("include_diff", out var diffElement))
                    {
                        includeDiff = diffElement.GetBoolean();
                    }

                    int recentCommits = 5;
                    if (root.TryGetProperty("recent_commits", out var commitsElement))
                    {
                        recentCommits = commitsElement.GetInt32();
                    }

                    if (!Directory.Exists(repoPath))
                        return $"Error: Directory not found: {repoPath}";

                    var result = new StringBuilder();
                    result.AppendLine($"Git Status for: {repoPath}");
                    result.AppendLine(new string('=', 50));

                    // Get current branch
                    var branch = await RunGit(repoPath, "rev-parse --abbrev-ref HEAD", cancel);
                    result.AppendLine($"\nBranch: {branch.Trim()}");

                    // Get status
                    var status = await RunGit(repoPath, "status --porcelain", cancel);
                    if (string.IsNullOrWhiteSpace(status))
                    {
                        result.AppendLine("\nWorking tree clean");
                    }
                    else
                    {
                        result.AppendLine("\nChanges:");
                        var lines = status.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        var staged = new List<string>();
                        var modified = new List<string>();
                        var untracked = new List<string>();

                        foreach (var line in lines)
                        {
                            if (line.Length < 3) continue;
                            var indexStatus = line[0];
                            var workTreeStatus = line[1];
                            var file = line.Substring(3);

                            if (indexStatus != ' ' && indexStatus != '?')
                                staged.Add($"  {line}");
                            if (workTreeStatus != ' ')
                                modified.Add($"  {line}");
                            if (indexStatus == '?')
                                untracked.Add($"  {file}");
                        }

                        if (staged.Count > 0)
                        {
                            result.AppendLine($"\nStaged ({staged.Count}):");
                            foreach (var s in staged.Take(20))
                                result.AppendLine(s);
                            if (staged.Count > 20)
                                result.AppendLine($"  ... and {staged.Count - 20} more");
                        }

                        if (modified.Count > 0)
                        {
                            result.AppendLine($"\nModified ({modified.Count}):");
                            foreach (var m in modified.Take(20))
                                result.AppendLine(m);
                            if (modified.Count > 20)
                                result.AppendLine($"  ... and {modified.Count - 20} more");
                        }

                        if (untracked.Count > 0)
                        {
                            result.AppendLine($"\nUntracked ({untracked.Count}):");
                            foreach (var u in untracked.Take(10))
                                result.AppendLine(u);
                            if (untracked.Count > 10)
                                result.AppendLine($"  ... and {untracked.Count - 10} more");
                        }
                    }

                    // Get recent commits
                    if (recentCommits > 0)
                    {
                        var log = await RunGit(repoPath, $"log --oneline -n {recentCommits}", cancel);
                        result.AppendLine($"\nRecent Commits ({recentCommits}):");
                        result.AppendLine(log);
                    }

                    // Include diff if requested
                    if (includeDiff)
                    {
                        var diff = await RunGit(repoPath, "diff --cached", cancel);
                        if (!string.IsNullOrWhiteSpace(diff))
                        {
                            result.AppendLine("\nStaged Diff:");
                            result.AppendLine(diff.Length > 5000 ? diff.Substring(0, 5000) + "\n... (truncated)" : diff);
                        }
                    }

                    return result.ToString();
                }
                catch (Exception ex)
                {
                    return $"Error getting git status: {ex.Message}";
                }
            }
        );
    }

    private static async Task<string> RunGit(string workingDirectory, string arguments, CancellationToken cancel)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancel);
        await process.WaitForExitAsync(cancel);
        return output;
    }
}
