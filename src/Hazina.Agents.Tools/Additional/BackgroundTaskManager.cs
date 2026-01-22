using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Hazina.Agents.Tools.Additional;

/// <summary>
/// Manages background task execution and tracking
/// </summary>
public static class BackgroundTaskManager
{
    private static readonly ConcurrentDictionary<string, BackgroundTask> _tasks = new();
    private static int _taskCounter = 0;

    public static string StartTask(string command, string workingDirectory, string description)
    {
        var taskId = $"task-{Interlocked.Increment(ref _taskCounter):D3}";
        var task = new BackgroundTask
        {
            Id = taskId,
            Command = command,
            Description = description,
            WorkingDirectory = workingDirectory,
            StartTime = DateTime.UtcNow,
            Status = TaskStatus.Running
        };

        _tasks[taskId] = task;

        // Start the process in background
        Task.Run(async () => await ExecuteTask(task));

        return taskId;
    }

    private static async Task ExecuteTask(BackgroundTask task)
    {
        try
        {
            string shell;
            string shellArgs;

            if (OperatingSystem.IsWindows())
            {
                shell = "powershell.exe";
                var escapedCommand = task.Command.Replace("\"", "`\"");
                shellArgs = $"-NoProfile -Command \"{escapedCommand}\"";
            }
            else
            {
                shell = "/bin/bash";
                shellArgs = $"-c \"{task.Command.Replace("\"", "\\\"")}\"";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = shellArgs,
                WorkingDirectory = task.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    lock (task.Output)
                    {
                        task.Output.AppendLine(e.Data);
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    lock (task.Output)
                    {
                        task.Output.AppendLine($"[STDERR] {e.Data}");
                    }
                }
            };

            task.Process = process;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            task.ExitCode = process.ExitCode;
            task.EndTime = DateTime.UtcNow;
            task.Status = process.ExitCode == 0 ? TaskStatus.Completed : TaskStatus.Failed;
        }
        catch (Exception ex)
        {
            task.Status = TaskStatus.Failed;
            task.EndTime = DateTime.UtcNow;
            lock (task.Output)
            {
                task.Output.AppendLine($"[ERROR] {ex.Message}");
            }
        }
    }

    public static BackgroundTask? GetTask(string taskId)
    {
        _tasks.TryGetValue(taskId, out var task);
        return task;
    }

    public static IEnumerable<BackgroundTask> GetAllTasks()
    {
        return _tasks.Values.OrderByDescending(t => t.StartTime);
    }

    public static bool KillTask(string taskId)
    {
        if (_tasks.TryGetValue(taskId, out var task) && task.Process != null)
        {
            try
            {
                if (!task.Process.HasExited)
                {
                    task.Process.Kill(entireProcessTree: true);
                    task.Status = TaskStatus.Killed;
                    task.EndTime = DateTime.UtcNow;
                    return true;
                }
            }
            catch
            {
                // Process may have already exited
            }
        }
        return false;
    }

    public class BackgroundTask
    {
        public string Id { get; set; } = "";
        public string Command { get; set; } = "";
        public string Description { get; set; } = "";
        public string WorkingDirectory { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TaskStatus Status { get; set; }
        public int? ExitCode { get; set; }
        public StringBuilder Output { get; } = new();
        public Process? Process { get; set; }

        public string GetOutput()
        {
            lock (Output)
            {
                return Output.ToString();
            }
        }

        public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;
    }

    public enum TaskStatus
    {
        Running,
        Completed,
        Failed,
        Killed
    }
}
