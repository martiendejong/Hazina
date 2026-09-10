using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AgenticOrchestration.Services.Execution;

/// <summary>
/// Executes work items autonomously by spawning Claude Code sessions
/// Each work item gets its own isolated session with context about the task
/// </summary>
public class AutonomousExecutor
{
    private readonly string _claudeCodePath;
    private readonly string _workingDirectory;

    public AutonomousExecutor(string claudeCodePath, string workingDirectory)
    {
        _claudeCodePath = claudeCodePath;
        _workingDirectory = workingDirectory;
    }

    /// <summary>
    /// Execute a work item autonomously
    /// Returns execution result with success/failure and output
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Autonomous] Starting execution: {workItem.Title}");

        try
        {
            // Build context-aware prompt for Claude
            var prompt = BuildPrompt(workItem);

            // Spawn Claude Code session
            var result = await SpawnClaudeSession(prompt, workItem.Id, cancellationToken);

            Console.WriteLine($"[Autonomous] Completed: {workItem.Title} - {(result.Success ? "SUCCESS" : "FAILED")}");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Autonomous] Error executing {workItem.Title}: {ex.Message}");
            return new ExecutionResult
            {
                Success = false,
                Output = $"Execution failed: {ex.Message}",
                Duration = TimeSpan.Zero
            };
        }
    }

    /// <summary>
    /// Build context-aware prompt based on work item source
    /// </summary>
    private string BuildPrompt(WorkItem workItem)
    {
        var sb = new StringBuilder();

        if (workItem.Source == WorkSource.ClickUp)
        {
            sb.AppendLine("# AUTONOMOUS TASK EXECUTION");
            sb.AppendLine();
            sb.AppendLine($"**Task ID:** {workItem.Id}");
            sb.AppendLine($"**Task:** {workItem.Title}");
            sb.AppendLine();
            sb.AppendLine("**Description:**");
            sb.AppendLine(workItem.Description);
            sb.AppendLine();
            sb.AppendLine("**Instructions:**");
            sb.AppendLine("1. Read the task description carefully");
            sb.AppendLine("2. Use /implement-todo skill or allocate worktree + implement");
            sb.AppendLine("3. Create PR when done");
            sb.AppendLine("4. Post PR link as ClickUp comment");
            sb.AppendLine("5. Move task to 'review' status");
            sb.AppendLine();
            sb.AppendLine("**Execute the task autonomously. Do not ask for confirmation - just do it.**");
        }
        else if (workItem.Source == WorkSource.GitHub)
        {
            sb.AppendLine("# AUTONOMOUS GITHUB EVENT");
            sb.AppendLine();
            sb.AppendLine($"**Event:** {workItem.Title}");
            sb.AppendLine($"**URL:** {workItem.Url}");
            sb.AppendLine();
            sb.AppendLine("**Instructions:**");
            sb.AppendLine("1. Navigate to the URL and analyze the event");
            sb.AppendLine("2. For PRs: Review code, run tests, provide feedback");
            sb.AppendLine("3. For PR comments: Address the requested changes");
            sb.AppendLine("4. For Issues: Analyze and implement fix if straightforward");
            sb.AppendLine();
            sb.AppendLine("**Execute autonomously. Act on the event without asking.**");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Spawn Claude Code session via command line
    /// Captures output and exit code
    /// </summary>
    private async Task<ExecutionResult> SpawnClaudeSession(string prompt, string workItemId, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var outputBuilder = new StringBuilder();

        var processInfo = new ProcessStartInfo
        {
            FileName = _claudeCodePath,
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        // Use ArgumentList to prevent command injection (no shell escaping needed)
        processInfo.ArgumentList.Add("--print");
        processInfo.ArgumentList.Add("--prompt");
        processInfo.ArgumentList.Add(prompt);

        using var process = new Process { StartInfo = processInfo };

        // Capture output
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                Console.WriteLine($"[Claude {workItemId}] {e.Data}");
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine($"ERROR: {e.Data}");
                Console.WriteLine($"[Claude {workItemId} ERROR] {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for completion or cancellation
        await process.WaitForExitAsync(cancellationToken);

        var duration = DateTime.UtcNow - startTime;
        var success = process.ExitCode == 0;

        return new ExecutionResult
        {
            Success = success,
            Output = outputBuilder.ToString(),
            Duration = duration,
            ExitCode = process.ExitCode
        };
    }
}

public class ExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int ExitCode { get; set; }
}
