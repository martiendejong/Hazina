using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Text.Json;

namespace Hazina.TaskRunner.PowerShell;

/// <summary>
/// Executes PowerShell scripts in-process without spawning visible console windows
/// </summary>
public class PowerShellExecutor : IDisposable
{
    private Runspace? _persistentRunspace;
    private readonly object _runspaceLock = new();
    private bool _disposed;

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

            // Read script content
            var scriptContent = File.ReadAllText(scriptPath);

            // Get or create runspace
            Runspace runspace;
            if (options.IsolatedRunspace)
            {
                runspace = RunspaceFactory.CreateRunspace();
                runspace.Open();
            }
            else
            {
                lock (_runspaceLock)
                {
                    if (_persistentRunspace == null || _persistentRunspace.RunspaceStateInfo.State != RunspaceState.Opened)
                    {
                        _persistentRunspace = RunspaceFactory.CreateRunspace();
                        _persistentRunspace.Open();
                    }
                    runspace = _persistentRunspace;
                }
            }

            try
            {
                using var powerShell = System.Management.Automation.PowerShell.Create();
                powerShell.Runspace = runspace;

                // Set working directory if specified
                if (!string.IsNullOrEmpty(options.WorkingDirectory))
                {
                    powerShell.AddScript($"Set-Location '{options.WorkingDirectory}'");
                    powerShell.Invoke();
                    powerShell.Commands.Clear();
                }

                // Add script
                powerShell.AddScript(scriptContent);

                // Add parameters if provided
                if (options.Parameters != null)
                {
                    foreach (var param in options.Parameters)
                    {
                        powerShell.AddParameter(param.Key, param.Value);
                    }
                }

                // Execute with timeout
                var asyncResult = powerShell.BeginInvoke();
                var completed = asyncResult.AsyncWaitHandle.WaitOne(options.Timeout);

                if (!completed)
                {
                    // Timeout occurred
                    powerShell.Stop();
                    result.TimedOut = true;
                    result.Success = false;
                    result.Errors.Add($"Script execution timed out after {options.Timeout.TotalSeconds} seconds");
                    result.ExitCode = -1;
                }
                else
                {
                    // Get results
                    var output = powerShell.EndInvoke(asyncResult);

                    // Collect output
                    var outputBuilder = new StringBuilder();
                    foreach (var item in output)
                    {
                        outputBuilder.AppendLine(item?.ToString() ?? string.Empty);
                    }
                    result.Output = outputBuilder.ToString();

                    // Collect errors
                    if (powerShell.Streams.Error.Count > 0)
                    {
                        foreach (var error in powerShell.Streams.Error)
                        {
                            result.Errors.Add(error.ToString());
                        }
                        result.Success = false;
                        result.ExitCode = 1;
                    }
                    else
                    {
                        result.Success = true;
                        result.ExitCode = 0;
                    }

                    // Collect warnings
                    foreach (var warning in powerShell.Streams.Warning)
                    {
                        result.Warnings.Add(warning.Message);
                    }

                    // Collect verbose output (add to main output)
                    foreach (var verbose in powerShell.Streams.Verbose)
                    {
                        result.Output += $"VERBOSE: {verbose.Message}\n";
                    }

                    // Collect debug output (add to main output)
                    foreach (var debug in powerShell.Streams.Debug)
                    {
                        result.Output += $"DEBUG: {debug.Message}\n";
                    }

                    // Collect information output (add to main output)
                    foreach (var info in powerShell.Streams.Information)
                    {
                        result.Output += $"{info.MessageData}\n";
                    }
                }
            }
            finally
            {
                // Dispose isolated runspace
                if (options.IsolatedRunspace && runspace != null)
                {
                    runspace.Close();
                    runspace.Dispose();
                }
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

        var result = new ExecutionResult
        {
            ScriptPath = "[inline command]",
            StartTime = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var runspace = RunspaceFactory.CreateRunspace();
            runspace.Open();

            using var powerShell = System.Management.Automation.PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(command);

            var asyncResult = powerShell.BeginInvoke();
            var completed = asyncResult.AsyncWaitHandle.WaitOne(options.Timeout);

            if (!completed)
            {
                powerShell.Stop();
                result.TimedOut = true;
                result.Success = false;
                result.Errors.Add($"Command execution timed out after {options.Timeout.TotalSeconds} seconds");
                result.ExitCode = -1;
            }
            else
            {
                var output = powerShell.EndInvoke(asyncResult);
                var outputBuilder = new StringBuilder();
                foreach (var item in output)
                {
                    outputBuilder.AppendLine(item?.ToString() ?? string.Empty);
                }
                result.Output = outputBuilder.ToString();

                if (powerShell.Streams.Error.Count > 0)
                {
                    foreach (var error in powerShell.Streams.Error)
                    {
                        result.Errors.Add(error.ToString());
                    }
                    result.Success = false;
                    result.ExitCode = 1;
                }
                else
                {
                    result.Success = true;
                    result.ExitCode = 0;
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Exception: {ex.Message}");
            result.ExceptionDetails = ex.ToString();
            result.ExitCode = 1;
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.EndTime = DateTime.UtcNow;

            if (options.LogExecution)
            {
                LogExecutionToFile(result, options.LogFilePath);
            }
        }

        return result;
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

        lock (_runspaceLock)
        {
            if (_persistentRunspace != null)
            {
                _persistentRunspace.Close();
                _persistentRunspace.Dispose();
                _persistentRunspace = null;
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
