using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.App.HazinaCoder.Core.Streaming;

/// <summary>
/// Execute dotnet build with line-by-line output streaming.
/// Shows errors immediately instead of waiting for build completion.
///
/// Improvement #93: Build Output Streaming (Value: 7/10, Effort: 4/10, Ratio: 1.75)
/// </summary>
public class BuildStreamingExecutor
{
    private readonly StreamingOrchestrator _streamer;

    public BuildStreamingExecutor(StreamingOrchestrator streamer)
    {
        _streamer = streamer;
    }

    /// <summary>
    /// Execute dotnet build with streaming output
    /// </summary>
    public async Task<BuildResult> BuildAsync(
        string projectPath,
        string configuration = "Release",
        CancellationToken cancellationToken = default)
    {
        var result = new BuildResult
        {
            ProjectPath = projectPath,
            StartTime = DateTime.UtcNow
        };

        await _streamer.EmitEventAsync(new BuildEvent
        {
            EventType = AgentEventType.BuildStarted,
            Message = $"Building: {Path.GetFileName(projectPath)}",
            ProjectPath = projectPath
        });

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{projectPath}\" --configuration {configuration}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory()
            };

            using var process = new Process { StartInfo = processInfo };

            // Stream stdout
            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;

                ProcessOutputLine(e.Data, result, projectPath);
            };

            // Stream stderr
            process.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;

                result.ErrorCount++;
                ProcessOutputLine(e.Data, result, projectPath, isError: true);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0;
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            // Emit completion event
            var eventType = result.Success ? AgentEventType.BuildCompleted : AgentEventType.BuildFailed;
            await _streamer.EmitEventAsync(new BuildEvent
            {
                EventType = eventType,
                Message = result.Success
                    ? $"Build successful ({result.ErrorCount} errors, {result.WarningCount} warnings)"
                    : $"Build failed ({result.ErrorCount} errors, {result.WarningCount} warnings)",
                ProjectPath = projectPath,
                ErrorCount = result.ErrorCount,
                WarningCount = result.WarningCount,
                Success = result.Success
            });
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorCount++;
            result.ErrorMessage = ex.Message;

            await _streamer.EmitEventAsync(new BuildEvent
            {
                EventType = AgentEventType.BuildFailed,
                Message = $"Build error: {ex.Message}",
                ProjectPath = projectPath,
                Success = false
            });
        }

        return result;
    }

    /// <summary>
    /// Process single output line
    /// </summary>
    private void ProcessOutputLine(string line, BuildResult result, string projectPath, bool isError = false)
    {
        // Count errors and warnings
        if (line.Contains("error", StringComparison.OrdinalIgnoreCase))
            result.ErrorCount++;
        else if (line.Contains("warning", StringComparison.OrdinalIgnoreCase))
            result.WarningCount++;

        // Emit streaming event
        Task.Run(async () =>
        {
            await _streamer.EmitBuildProgressAsync(
                projectPath,
                line,
                result.ErrorCount,
                result.WarningCount);
        });
    }
}

/// <summary>
/// Build result
/// </summary>
public class BuildResult
{
    public string ProjectPath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Execute dotnet test with streaming output.
///
/// Improvement #94: Test Result Streaming (Value: 7/10, Effort: 4/10, Ratio: 1.75)
/// </summary>
public class TestStreamingExecutor
{
    private readonly StreamingOrchestrator _streamer;

    public TestStreamingExecutor(StreamingOrchestrator streamer)
    {
        _streamer = streamer;
    }

    /// <summary>
    /// Execute dotnet test with streaming output
    /// </summary>
    public async Task<TestResult> TestAsync(
        string projectPath,
        string configuration = "Release",
        CancellationToken cancellationToken = default)
    {
        var result = new TestResult
        {
            ProjectPath = projectPath,
            StartTime = DateTime.UtcNow
        };

        await _streamer.EmitEventAsync(new TestEvent
        {
            EventType = AgentEventType.TestStarted,
            Message = $"Running tests: {Path.GetFileName(projectPath)}",
            TestName = projectPath
        });

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{projectPath}\" --configuration {configuration} --no-build",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory()
            };

            using var process = new Process { StartInfo = processInfo };

            // Stream output
            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;

                ProcessTestOutputLine(e.Data, result, projectPath);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;

                result.FailedTests++;
                ProcessTestOutputLine(e.Data, result, projectPath, isError: true);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0;
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            // Emit completion
            var eventType = result.Success ? AgentEventType.TestCompleted : AgentEventType.TestFailed;
            await _streamer.EmitEventAsync(new TestEvent
            {
                EventType = eventType,
                Message = result.Success
                    ? $"All tests passed ({result.PassedTests}/{result.TotalTests})"
                    : $"Tests failed ({result.PassedTests}/{result.TotalTests} passed, {result.FailedTests} failed)",
                TestName = projectPath,
                TotalTests = result.TotalTests,
                PassedTests = result.PassedTests,
                FailedTests = result.FailedTests,
                Success = result.Success
            });
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;

            await _streamer.EmitEventAsync(new TestEvent
            {
                EventType = AgentEventType.TestFailed,
                Message = $"Test error: {ex.Message}",
                TestName = projectPath,
                Success = false
            });
        }

        return result;
    }

    /// <summary>
    /// Process test output line
    /// </summary>
    private void ProcessTestOutputLine(string line, TestResult result, string projectPath, bool isError = false)
    {
        // Parse test results
        if (line.Contains("Passed!", StringComparison.OrdinalIgnoreCase))
            result.PassedTests++;
        else if (line.Contains("Failed!", StringComparison.OrdinalIgnoreCase))
            result.FailedTests++;

        // Extract total tests
        if (line.Contains("Total tests:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = line.Split(':');
            if (parts.Length > 1 && int.TryParse(parts[1].Trim().Split(' ')[0], out var total))
                result.TotalTests = total;
        }

        // Emit streaming event
        Task.Run(async () =>
        {
            await _streamer.EmitTestProgressAsync(
                Path.GetFileName(projectPath),
                result.TotalTests,
                result.PassedTests,
                result.FailedTests);
        });
    }
}

/// <summary>
/// Test result
/// </summary>
public class TestResult
{
    public string ProjectPath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int SkippedTests { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}
