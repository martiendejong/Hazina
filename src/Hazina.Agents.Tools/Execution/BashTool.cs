using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Hazina.Agents.Tools.Execution;

/// <summary>
/// Tool for executing PowerShell commands (Windows) or bash commands (Linux/Mac)
/// UNRESTRICTED - Full system access
/// </summary>
public static class BashTool
{
    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "bash",
            description: "Execute system commands via PowerShell (Windows) or bash (Linux/Mac). Full unrestricted access to system commands.",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "command",
                    Type = "string",
                    Required = true,
                    Description = "The command to execute"
                },
                new()
                {
                    Name = "timeout",
                    Type = "number",
                    Required = false,
                    Description = "Timeout in milliseconds (default: 120000 = 2 minutes)"
                },
                new()
                {
                    Name = "working_directory",
                    Type = "string",
                    Required = false,
                    Description = "Override working directory for this command"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    if (!root.TryGetProperty("command", out var commandElement))
                        return "Error: command parameter is required";

                    var command = commandElement.GetString();
                    if (string.IsNullOrWhiteSpace(command))
                        return "Error: command cannot be empty";

                    // Parse optional parameters
                    int timeout = 120000; // 2 minutes default
                    if (root.TryGetProperty("timeout", out var timeoutElement))
                    {
                        timeout = timeoutElement.GetInt32();
                    }

                    string execWorkingDir = workingDirectory;
                    if (root.TryGetProperty("working_directory", out var wdElement))
                    {
                        var wd = wdElement.GetString();
                        if (!string.IsNullOrWhiteSpace(wd))
                        {
                            execWorkingDir = Path.IsPathRooted(wd)
                                ? wd
                                : Path.GetFullPath(Path.Combine(workingDirectory, wd));
                        }
                    }

                    // Determine shell based on OS
                    string shell;
                    string shellArgs;

                    if (OperatingSystem.IsWindows())
                    {
                        shell = "powershell.exe";
                        // Escape quotes in command
                        var escapedCommand = command.Replace("\"", "`\"");
                        shellArgs = $"-NoProfile -Command \"{escapedCommand}\"";
                    }
                    else
                    {
                        shell = "/bin/bash";
                        shellArgs = $"-c \"{command.Replace("\"", "\\\"")}\"";
                    }

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = shell,
                        Arguments = shellArgs,
                        WorkingDirectory = execWorkingDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    var stdout = new StringBuilder();
                    var stderr = new StringBuilder();

                    using var process = new Process { StartInfo = startInfo };

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null) stdout.AppendLine(e.Data);
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null) stderr.AppendLine(e.Data);
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Wait for process with timeout
                    await process.WaitForExitAsync(cancel).WaitAsync(TimeSpan.FromMilliseconds(timeout));

                    var exitCode = process.ExitCode;

                    var result = new StringBuilder();
                    result.AppendLine($"Command: {command}");
                    result.AppendLine($"Working Directory: {execWorkingDir}");
                    result.AppendLine($"Exit Code: {exitCode}");
                    result.AppendLine();

                    if (stdout.Length > 0)
                    {
                        result.AppendLine("STDOUT:");
                        result.AppendLine(stdout.ToString());
                    }

                    if (stderr.Length > 0)
                    {
                        result.AppendLine("STDERR:");
                        result.AppendLine(stderr.ToString());
                    }

                    if (stdout.Length == 0 && stderr.Length == 0)
                    {
                        result.AppendLine("(No output)");
                    }

                    return result.ToString();
                }
                catch (OperationCanceledException)
                {
                    return "Error: Command execution cancelled or timed out";
                }
                catch (Exception ex)
                {
                    return $"Error executing command: {ex.Message}";
                }
            }
        );
    }
}
