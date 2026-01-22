using System.Text;
using System.Text.Json;

namespace Hazina.Agents.Tools.Additional;

/// <summary>
/// Tool for getting output from background tasks
/// </summary>
public static class TaskOutputTool
{
    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "task_output",
            description: "Get output and status from a background task. Can also list all tasks or kill a running task.",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "task_id",
                    Type = "string",
                    Required = false,
                    Description = "The task ID to get output from. If not provided, lists all tasks."
                },
                new()
                {
                    Name = "action",
                    Type = "string",
                    Required = false,
                    Description = "Action to perform: 'output' (default), 'list', or 'kill'"
                },
                new()
                {
                    Name = "wait",
                    Type = "boolean",
                    Required = false,
                    Description = "Wait for task to complete before returning (default: false)"
                },
                new()
                {
                    Name = "timeout_ms",
                    Type = "number",
                    Required = false,
                    Description = "Maximum time to wait in milliseconds (default: 30000)"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    var action = "output";
                    if (root.TryGetProperty("action", out var actionElement))
                    {
                        action = actionElement.GetString()?.ToLower() ?? "output";
                    }

                    // List all tasks
                    if (action == "list" || !root.TryGetProperty("task_id", out var taskIdElement))
                    {
                        return ListTasks();
                    }

                    var taskId = taskIdElement.GetString();
                    if (string.IsNullOrWhiteSpace(taskId))
                    {
                        return ListTasks();
                    }

                    var task = BackgroundTaskManager.GetTask(taskId);
                    if (task == null)
                    {
                        return $"Error: Task '{taskId}' not found.\n\n{ListTasks()}";
                    }

                    // Kill task
                    if (action == "kill")
                    {
                        if (BackgroundTaskManager.KillTask(taskId))
                        {
                            return $"Task '{taskId}' has been killed.";
                        }
                        else
                        {
                            return $"Could not kill task '{taskId}' - it may have already completed.";
                        }
                    }

                    // Wait for task if requested
                    bool wait = false;
                    if (root.TryGetProperty("wait", out var waitElement))
                    {
                        wait = waitElement.GetBoolean();
                    }

                    int timeoutMs = 30000;
                    if (root.TryGetProperty("timeout_ms", out var timeoutElement))
                    {
                        timeoutMs = timeoutElement.GetInt32();
                    }

                    if (wait && task.Status == BackgroundTaskManager.TaskStatus.Running)
                    {
                        var startWait = DateTime.UtcNow;
                        while (task.Status == BackgroundTaskManager.TaskStatus.Running)
                        {
                            if ((DateTime.UtcNow - startWait).TotalMilliseconds > timeoutMs)
                            {
                                return FormatTaskOutput(task, timedOut: true);
                            }
                            await Task.Delay(500, cancel);
                        }
                    }

                    return FormatTaskOutput(task);
                }
                catch (Exception ex)
                {
                    return $"Error getting task output: {ex.Message}";
                }
            }
        );
    }

    private static string ListTasks()
    {
        var tasks = BackgroundTaskManager.GetAllTasks().ToList();

        if (tasks.Count == 0)
        {
            return "No background tasks.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Background Tasks:");
        sb.AppendLine();

        foreach (var task in tasks)
        {
            var statusIcon = task.Status switch
            {
                BackgroundTaskManager.TaskStatus.Running => "⏳",
                BackgroundTaskManager.TaskStatus.Completed => "✅",
                BackgroundTaskManager.TaskStatus.Failed => "❌",
                BackgroundTaskManager.TaskStatus.Killed => "🛑",
                _ => "?"
            };

            sb.AppendLine($"{statusIcon} [{task.Id}] {task.Description}");
            sb.AppendLine($"   Status: {task.Status} | Duration: {task.Duration.TotalSeconds:F1}s");
            if (task.ExitCode.HasValue)
            {
                sb.AppendLine($"   Exit Code: {task.ExitCode}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatTaskOutput(BackgroundTaskManager.BackgroundTask task, bool timedOut = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Task: {task.Id}");
        sb.AppendLine($"Description: {task.Description}");
        sb.AppendLine($"Command: {task.Command}");
        sb.AppendLine($"Status: {task.Status}");
        sb.AppendLine($"Duration: {task.Duration.TotalSeconds:F1}s");

        if (task.ExitCode.HasValue)
        {
            sb.AppendLine($"Exit Code: {task.ExitCode}");
        }

        if (timedOut)
        {
            sb.AppendLine();
            sb.AppendLine("[TIMEOUT] Task is still running. Output so far:");
        }

        sb.AppendLine();
        sb.AppendLine("Output:");
        sb.AppendLine("─".PadRight(40, '─'));

        var output = task.GetOutput();
        if (string.IsNullOrWhiteSpace(output))
        {
            sb.AppendLine("(no output yet)");
        }
        else
        {
            sb.AppendLine(output);
        }

        return sb.ToString();
    }
}
