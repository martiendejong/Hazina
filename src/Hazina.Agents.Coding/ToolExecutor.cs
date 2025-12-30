using System.Diagnostics;
using System.Text;

namespace Hazina.Agents.Coding;

/// <summary>
/// Executes tools deterministically outside the GLM model.
/// All PowerShell execution happens here for Windows compatibility.
/// </summary>
public class ToolExecutor
{
    private readonly string _workingDirectory;

    public ToolExecutor(string workingDirectory)
    {
        _workingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));

        if (!Directory.Exists(_workingDirectory))
        {
            throw new DirectoryNotFoundException($"Working directory not found: {_workingDirectory}");
        }
    }

    /// <summary>
    /// Execute a tool action and return the result
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(ToolAction action)
    {
        try
        {
            return action.Tool switch
            {
                "read_file" => await ReadFileAsync(action.Path!),
                "apply_diff" => await ApplyDiffAsync(action.Path!, action.Diff!),
                "run" => await RunCommandAsync(action.Command!),
                "git_status" => await RunGitStatusAsync(),
                "git_diff" => await RunGitDiffAsync(),
                _ => ExecutionResult.FailureResult($"Unknown tool: {action.Tool}", action.Tool)
            };
        }
        catch (Exception ex)
        {
            return ExecutionResult.FailureResult(
                $"Tool execution failed: {ex.Message}",
                action.Tool
            );
        }
    }

    /// <summary>
    /// Read a file from the working directory
    /// </summary>
    private async Task<ExecutionResult> ReadFileAsync(string relativePath)
    {
        try
        {
            var fullPath = Path.Combine(_workingDirectory, relativePath);

            if (!File.Exists(fullPath))
            {
                return ExecutionResult.FailureResult(
                    $"File not found: {relativePath}",
                    "read_file"
                );
            }

            var content = await File.ReadAllTextAsync(fullPath);
            return ExecutionResult.SuccessResult(content, "read_file");
        }
        catch (Exception ex)
        {
            return ExecutionResult.FailureResult(
                $"Failed to read file: {ex.Message}",
                "read_file"
            );
        }
    }

    /// <summary>
    /// Apply a unified diff to a file
    /// </summary>
    private async Task<ExecutionResult> ApplyDiffAsync(string relativePath, string diff)
    {
        try
        {
            var fullPath = Path.Combine(_workingDirectory, relativePath);

            if (!File.Exists(fullPath))
            {
                return ExecutionResult.FailureResult(
                    $"File not found: {relativePath}",
                    "apply_diff"
                );
            }

            var content = await File.ReadAllTextAsync(fullPath);
            var patchedContent = ApplyUnifiedDiff(content, diff);

            if (patchedContent == null)
            {
                return ExecutionResult.FailureResult(
                    "Failed to apply diff - patch did not match",
                    "apply_diff"
                );
            }

            await File.WriteAllTextAsync(fullPath, patchedContent);

            return ExecutionResult.SuccessResult(
                $"Applied diff to {relativePath}",
                "apply_diff"
            );
        }
        catch (Exception ex)
        {
            return ExecutionResult.FailureResult(
                $"Failed to apply diff: {ex.Message}",
                "apply_diff"
            );
        }
    }

    /// <summary>
    /// Run a PowerShell command (Windows-compatible)
    /// </summary>
    private async Task<ExecutionResult> RunCommandAsync(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{command.Replace("\"", "`\"")}\"",
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    outputBuilder.AppendLine(args.Data);
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    errorBuilder.AppendLine(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            if (process.ExitCode != 0)
            {
                return ExecutionResult.FailureResult(
                    error.Length > 0 ? error : $"Command exited with code {process.ExitCode}",
                    "run",
                    output
                );
            }

            return ExecutionResult.SuccessResult(output, "run");
        }
        catch (Exception ex)
        {
            return ExecutionResult.FailureResult(
                $"Failed to run command: {ex.Message}",
                "run"
            );
        }
    }

    /// <summary>
    /// Run git status
    /// </summary>
    private async Task<ExecutionResult> RunGitStatusAsync()
    {
        return await RunCommandAsync("git status");
    }

    /// <summary>
    /// Run git diff
    /// </summary>
    private async Task<ExecutionResult> RunGitDiffAsync()
    {
        return await RunCommandAsync("git diff");
    }

    /// <summary>
    /// Simple unified diff parser and applier
    /// </summary>
    private string? ApplyUnifiedDiff(string original, string diff)
    {
        try
        {
            var lines = original.Split('\n').ToList();
            var diffLines = diff.Split('\n');

            int currentLine = 0;
            bool inHunk = false;
            int hunkLine = 0;

            foreach (var diffLine in diffLines)
            {
                if (diffLine.StartsWith("@@"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        diffLine,
                        @"@@ -(\d+),?\d* \+(\d+),?\d* @@"
                    );

                    if (match.Success)
                    {
                        currentLine = int.Parse(match.Groups[1].Value) - 1;
                        hunkLine = currentLine;
                        inHunk = true;
                    }
                    continue;
                }

                if (!inHunk)
                    continue;

                if (diffLine.StartsWith("-"))
                {
                    if (hunkLine < lines.Count)
                    {
                        lines.RemoveAt(hunkLine);
                    }
                }
                else if (diffLine.StartsWith("+"))
                {
                    var lineContent = diffLine.Substring(1);
                    lines.Insert(hunkLine, lineContent);
                    hunkLine++;
                }
                else if (diffLine.StartsWith(" "))
                {
                    hunkLine++;
                }
            }

            return string.Join("\n", lines);
        }
        catch
        {
            return null;
        }
    }
}
