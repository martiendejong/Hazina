using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Hazina.LLMs.Plugins;

/// <summary>
/// Semantic Kernel plugin for developer tools (git, dotnet, npm, build)
/// </summary>
public class DeveloperToolsPlugin
{
    private readonly string _workingDirectory;

    public DeveloperToolsPlugin(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    #region Git

    [KernelFunction("git")]
    [Description("Execute git commands and return the output")]
    public async Task<string> Git(
        [Description("Git command arguments (e.g., 'status', 'diff', 'log --oneline -10')")] string arguments,
        [Description("Timeout in seconds")] int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(arguments))
                return "No arguments provided";

            var (output, error) = await ExecuteProcess(
                "git",
                arguments,
                _workingDirectory,
                TimeSpan.FromSeconds(timeoutSeconds),
                cancellationToken);

            return output + "\n" + error;
        }
        catch (TimeoutException ex)
        {
            return $"Command timed out: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    #endregion

    #region DotNet

    [KernelFunction("dotnet")]
    [Description("Execute dotnet CLI commands and return the output")]
    public async Task<string> DotNet(
        [Description("Dotnet command arguments (e.g., 'build', 'test', 'run')")] string arguments,
        [Description("Timeout in seconds")] int timeoutSeconds = 120,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(arguments))
                return "No arguments provided";

            var (output, error) = await ExecuteProcess(
                "dotnet",
                arguments,
                _workingDirectory,
                TimeSpan.FromSeconds(timeoutSeconds),
                cancellationToken);

            return output + "\n" + error;
        }
        catch (TimeoutException ex)
        {
            return $"Command timed out: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    #endregion

    #region NPM

    [KernelFunction("npm")]
    [Description("Execute npm commands and return the output")]
    public async Task<string> Npm(
        [Description("NPM command arguments (e.g., 'install', 'build', 'test')")] string arguments,
        [Description("Timeout in seconds")] int timeoutSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(arguments))
                return "No arguments provided";

            // Determine npm path (Windows vs Unix)
            var npmPath = OperatingSystem.IsWindows()
                ? "C:\\Program Files\\nodejs\\npm.cmd"
                : "npm";

            // Try frontend subdirectory first, fallback to root
            var frontendDir = Path.Combine(_workingDirectory, "frontend");
            var workingDir = Directory.Exists(frontendDir) ? frontendDir : _workingDirectory;

            var (output, error) = await ExecuteProcess(
                npmPath,
                arguments,
                workingDir,
                TimeSpan.FromSeconds(timeoutSeconds),
                cancellationToken);

            return output + "\n" + error;
        }
        catch (TimeoutException ex)
        {
            return $"Command timed out: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    #endregion

    #region Build

    [KernelFunction("build")]
    [Description("Execute the build script and return the output")]
    public async Task<string> Build(
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var buildScript = OperatingSystem.IsWindows() ? "build.bat" : "build.sh";
            var buildPath = Path.Combine(_workingDirectory, buildScript);

            if (!File.Exists(buildPath))
                return $"Build script not found: {buildPath}";

            var fileName = OperatingSystem.IsWindows() ? buildScript : "/bin/bash";
            var arguments = OperatingSystem.IsWindows() ? "" : buildScript;

            var (output, error) = await ExecuteProcess(
                fileName,
                arguments,
                _workingDirectory,
                TimeSpan.FromMinutes(10),
                cancellationToken);

            // Also try to read build_errors.log if it exists
            var errorLogPath = Path.Combine(_workingDirectory, "build_errors.log");
            if (File.Exists(errorLogPath))
            {
                var errorLog = await File.ReadAllTextAsync(errorLogPath, cancellationToken);
                error += "\n\nBuild Errors Log:\n" + errorLog;
            }

            return output + "\n" + error;
        }
        catch (TimeoutException ex)
        {
            return $"Build timed out: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    #endregion

    #region Helper Methods

    private static async Task<(string output, string error)> ExecuteProcess(
        string fileName,
        string arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for process with timeout and cancellation support
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Check if it was timeout or external cancellation
            if (cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(true); } catch { }
                throw;
            }
            else
            {
                // Timeout
                try { process.Kill(true); } catch (Exception ex)
                {
                    errorBuilder.AppendLine($"Error killing process: {ex.Message}");
                }
                throw new TimeoutException($"Process exceeded timeout of {timeout.TotalSeconds} seconds.");
            }
        }

        return (outputBuilder.ToString(), errorBuilder.ToString());
    }

    #endregion
}
