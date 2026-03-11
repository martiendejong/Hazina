using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Hazina.TaskRunner.PowerShell;

/// <summary>
/// Executes PowerShell scripts silently without spawning visible console windows
/// Uses Process.Start with hidden window for maximum compatibility
/// </summary>
public class PowerShellExecutor : IDisposable
{
    private bool _disposed;
    private readonly string _powerShellPath;

    public PowerShellExecutor()
    {
        // Use PowerShell.exe (Windows PowerShell 5.1) - always available on Windows
        _powerShellPath = "powershell.exe";
    }

    /// <summary>
    /// Execute a PowerShell script file
    /// </summary>
    /// <param name="scriptPath">Full path to the PowerShell script (.ps1 file)</param>
    /// <param name="options">Execution options</param>
    /// <returns>Execution result with output, errors, and duration</returns>
    public ExecutionResult ExecuteScript(string scriptPath, ExecutionOptions? options = null)
    {
        options ??= new ExecutionOptions();

        var result = new ExecutionResult
        {
            ScriptPath = scriptPath,
            StartTime = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate script exists
            if (!File.Exists(scriptPath))
            {
                result.Success = false;
                result.Errors.Add($"Script file not found: {scriptPath}");
                result.ExitCode = 1;
                result.EndTime = DateTime.UtcNow;
                result.Duration = stopwatch.Elapsed;
                return result;
            }

            // Build PowerShell command
            var arguments = new StringBuilder();
            arguments.Append("-ExecutionPolicy Bypass ");
            arguments.Append("-NoProfile ");
            arguments.Append("-NonInteractive ");
            arguments.Append($"-File \"{scriptPath}\" ");

            // Add parameters
            if (options.Parameters != null)
            {
                foreach (var param in options.Parameters)
                {
                    var value = param.Value?.ToString() ?? string.Empty;
                    arguments.Append($"-{param.Key} \"{value}\" ");
                }
            }

            // Create process start info
            var startInfo = new ProcessStartInfo
            {
                FileName = _powerShellPath,
                Arguments = arguments.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = options.Silent,  // CRITICAL: No popup window
                WindowStyle = options.Silent ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                WorkingDirectory = options.WorkingDirectory ?? Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory
            };

            using var process = new Process { StartInfo = startInfo };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            // Capture output and errors
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            // Start process
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for completion with timeout
            bool completed = process.WaitForExit((int)options.Timeout.TotalMilliseconds);

            if (!completed)
            {
                // Timeout occurred
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Process may have already exited
                }

                result.TimedOut = true;
                result.Success = false;
                result.Errors.Add($"Script execution timed out after {options.Timeout.TotalSeconds} seconds");
                result.ExitCode = -1;
            }
            else
            {
                // Wait for async output to complete
                process.WaitForExit();

                result.Output = outputBuilder.ToString();
                result.ExitCode = process.ExitCode;

                var errorOutput = errorBuilder.ToString();
                if (!string.IsNullOrWhiteSpace(errorOutput))
                {
                    result.Errors.AddRange(errorOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
                }

                // Success if exit code is 0 and no errors
                result.Success = result.ExitCode == 0 && result.Errors.Count == 0;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Exception during execution: {ex.Message}");
            result.ExceptionDetails = ex.ToString();
            result.ExitCode = 1;
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.EndTime = DateTime.UtcNow;

            // Log execution if enabled
            if (options.LogExecution)
            {
                LogExecutionToFile(result, options.LogFilePath);
            }
        }

        return result;
    }

    /// <summary>
    /// Execute a PowerShell command string (not from file)
    /// </summary>
    /// <param name="command">PowerShell command to execute</param>
    /// <param name="options">Execution options</param>
    /// <returns>Execution result</returns>
    public ExecutionResult ExecuteCommand(string command, ExecutionOptions? options = null)
    {
        options ??= new ExecutionOptions();

        // Create temporary script file
        var tempFile = Path.GetTempFileName();
        var scriptFile = Path.ChangeExtension(tempFile, ".ps1");

        try
        {
            File.WriteAllText(scriptFile, command);

            var result = ExecuteScript(scriptFile, options);
            result.ScriptPath = "[inline command]";

            return result;
        }
        finally
        {
            // Cleanup temp file
            try
            {
                if (File.Exists(scriptFile))
                {
                    File.Delete(scriptFile);
                }
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Log execution result to JSONL file
    /// </summary>
    private void LogExecutionToFile(ExecutionResult result, string logFilePath)
    {
        try
        {
            var logDir = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            var logEntry = new
            {
                ts = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                scriptPath = result.ScriptPath,
                success = result.Success,
                duration = result.Duration.TotalMilliseconds,
                exitCode = result.ExitCode,
                timedOut = result.TimedOut,
                errorCount = result.Errors.Count,
                warningCount = result.Warnings.Count,
                outputLength = result.Output.Length
            };

            var jsonLine = JsonSerializer.Serialize(logEntry);
            File.AppendAllText(logFilePath, jsonLine + Environment.NewLine);
        }
        catch
        {
            // Ignore logging errors - don't fail execution because of logging issues
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
