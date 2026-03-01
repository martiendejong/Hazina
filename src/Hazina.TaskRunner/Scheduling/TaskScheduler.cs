using Cronos;
using Hazina.TaskRunner.PowerShell;

namespace Hazina.TaskRunner.Scheduling;

/// <summary>
/// Main task scheduling engine that executes scheduled PowerShell scripts
/// Supports multiple task sets for bundled applications
/// </summary>
public class TaskScheduler : IDisposable
{
    private readonly TaskSetConfiguration _taskSetConfig;
    private readonly TaskConfigurationManager? _legacyConfigManager; // Backward compatibility
    private readonly PowerShellExecutor _executor;
    private readonly Timer _schedulerTimer;
    private readonly object _executionLock = new();
    private bool _disposed;
    private List<TaskSet> _taskSets = new();

    /// <summary>
    /// Create a TaskScheduler with bundled task sets support
    /// </summary>
    /// <param name="configDirectory">Directory containing task set configuration files</param>
    public TaskScheduler(string configDirectory)
    {
        // If configDirectory is a .json file (legacy), use legacy mode
        if (configDirectory.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var configDir = Path.GetDirectoryName(configDirectory) ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Hazina",
                "TaskRunner");

            _taskSetConfig = new TaskSetConfiguration(configDir);
            _legacyConfigManager = new TaskConfigurationManager(configDirectory);
        }
        else
        {
            _taskSetConfig = new TaskSetConfiguration(configDirectory);
        }

        _executor = new PowerShellExecutor();

        // Load all task sets
        LoadTaskSets();

        // Initialize next run times for all tasks
        InitializeNextRunTimes();

        // Start scheduler timer (check every minute)
        _schedulerTimer = new Timer(
            callback: _ => CheckAndExecuteTasks(),
            state: null,
            dueTime: TimeSpan.FromSeconds(10), // First check after 10 seconds
            period: TimeSpan.FromMinutes(1)     // Then every minute
        );
    }

    /// <summary>
    /// Load all task sets from configuration
    /// </summary>
    private void LoadTaskSets()
    {
        _taskSets = _taskSetConfig.LoadAllTaskSets();
    }

    /// <summary>
    /// Reload task sets from disk (useful after config changes)
    /// </summary>
    public void ReloadTaskSets()
    {
        LoadTaskSets();
        InitializeNextRunTimes();
    }

    /// <summary>
    /// Initialize NextRun times for all tasks on startup
    /// </summary>
    private void InitializeNextRunTimes()
    {
        bool changed = false;

        foreach (var taskSet in _taskSets.Where(ts => ts.Enabled))
        {
            foreach (var task in taskSet.Tasks.Where(t => t.Enabled))
            {
                if (task.NextRun == null || task.NextRun < DateTime.UtcNow)
                {
                    task.NextRun = CalculateNextRun(task.CronExpression, DateTime.UtcNow);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            _taskSetConfig.SaveAllTaskSets(_taskSets);
        }
    }

    /// <summary>
    /// Check all tasks and execute those that are due
    /// </summary>
    private void CheckAndExecuteTasks()
    {
        lock (_executionLock)
        {
            var now = DateTime.UtcNow;
            bool configChanged = false;

            // Only check tasks in enabled task sets
            foreach (var taskSet in _taskSets.Where(ts => ts.Enabled))
            {
                foreach (var task in taskSet.Tasks.Where(t => t.Enabled))
                {
                    // Check if task is due to run
                    if (task.NextRun.HasValue && task.NextRun.Value <= now)
                    {
                        // Execute task
                        ExecuteTask(task, taskSet.Name);

                        // Update timestamps
                        task.LastRun = now;
                        task.NextRun = CalculateNextRun(task.CronExpression, now);
                        configChanged = true;
                    }
                }
            }

            // Save configuration if any tasks were executed
            if (configChanged)
            {
                _taskSetConfig.SaveAllTaskSets(_taskSets);
            }
        }
    }

    /// <summary>
    /// Execute a single task using PowerShellExecutor
    /// </summary>
    private void ExecuteTask(ScheduledTask task, string taskSetName)
    {
        try
        {
            var options = new ExecutionOptions
            {
                Silent = true,
                Elevated = task.RunElevated,
                Timeout = TimeSpan.FromSeconds(task.TimeoutSeconds),
                WorkingDirectory = task.WorkingDirectory,
                Parameters = task.Parameters,
                LogExecution = true
            };

            var result = _executor.ExecuteScript(task.ScriptPath, options);

            // Log result with task set name
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{taskSetName}] Task '{task.Name}' ({task.Id}) executed: Success={result.Success}, Duration={result.Duration.TotalSeconds:F2}s");

            if (!result.Success && result.Errors.Count > 0)
            {
                Console.WriteLine($"  Errors: {string.Join("; ", result.Errors)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{taskSetName}] Task '{task.Name}' ({task.Id}) failed with exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculate next execution time based on cron expression
    /// </summary>
    private DateTime? CalculateNextRun(string cronExpression, DateTime fromTime)
    {
        try
        {
            // Try standard format first (5 fields: minute hour day month weekday)
            var expression = CronExpression.Parse(cronExpression, CronFormat.Standard);
            return expression.GetNextOccurrence(fromTime, TimeZoneInfo.Utc);
        }
        catch (CronFormatException)
        {
            try
            {
                // Try with seconds included (6 fields: second minute hour day month weekday)
                var expression = CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);
                return expression.GetNextOccurrence(fromTime, TimeZoneInfo.Utc);
            }
            catch (CronFormatException)
            {
                // Invalid cron expression in both formats
                return null;
            }
        }
    }

    // ====== Task Set Management API ======

    /// <summary>
    /// Get all task sets
    /// </summary>
    public List<TaskSet> GetAllTaskSets()
    {
        return _taskSets;
    }

    /// <summary>
    /// Get a task set by ID
    /// </summary>
    public TaskSet? GetTaskSet(string taskSetId)
    {
        return _taskSets.FirstOrDefault(ts => ts.Id == taskSetId);
    }

    /// <summary>
    /// Enable an entire task set
    /// </summary>
    public void EnableTaskSet(string taskSetId)
    {
        var taskSet = GetTaskSet(taskSetId);
        if (taskSet != null)
        {
            taskSet.Enabled = true;
            _taskSetConfig.SaveTaskSet(taskSet);
            LoadTaskSets(); // Reload
        }
    }

    /// <summary>
    /// Disable an entire task set
    /// </summary>
    public void DisableTaskSet(string taskSetId)
    {
        var taskSet = GetTaskSet(taskSetId);
        if (taskSet != null)
        {
            taskSet.Enabled = false;
            _taskSetConfig.SaveTaskSet(taskSet);
            LoadTaskSets(); // Reload
        }
    }

    // ====== Task Management API ======

    /// <summary>
    /// Add a new task to a specific task set
    /// </summary>
    public void AddTask(string taskSetId, ScheduledTask task)
    {
        var taskSet = GetTaskSet(taskSetId);
        if (taskSet != null)
        {
            // Calculate initial next run time
            task.NextRun = CalculateNextRun(task.CronExpression, DateTime.UtcNow);
            taskSet.Tasks.Add(task);
            _taskSetConfig.SaveTaskSet(taskSet);
            LoadTaskSets(); // Reload
        }
    }

    /// <summary>
    /// Remove a task by ID from any task set
    /// </summary>
    public bool RemoveTask(string taskId)
    {
        foreach (var taskSet in _taskSets)
        {
            var task = taskSet.Tasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                taskSet.Tasks.Remove(task);
                _taskSetConfig.SaveTaskSet(taskSet);
                LoadTaskSets(); // Reload
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Update an existing task
    /// </summary>
    public void UpdateTask(string taskSetId, ScheduledTask task)
    {
        var taskSet = GetTaskSet(taskSetId);
        if (taskSet != null)
        {
            var existing = taskSet.Tasks.FirstOrDefault(t => t.Id == task.Id);
            if (existing != null)
            {
                // Recalculate next run if cron expression changed
                if (existing.CronExpression != task.CronExpression)
                {
                    task.NextRun = CalculateNextRun(task.CronExpression, DateTime.UtcNow);
                }

                // Replace the task
                int index = taskSet.Tasks.IndexOf(existing);
                taskSet.Tasks[index] = task;

                _taskSetConfig.SaveTaskSet(taskSet);
                LoadTaskSets(); // Reload
            }
        }
    }

    /// <summary>
    /// Get a task by ID from any task set
    /// </summary>
    public ScheduledTask? GetTask(string taskId)
    {
        foreach (var taskSet in _taskSets)
        {
            var task = taskSet.Tasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null) return task;
        }
        return null;
    }

    /// <summary>
    /// Get all tasks from all enabled task sets
    /// </summary>
    public List<ScheduledTask> GetAllTasks()
    {
        return _taskSets
            .Where(ts => ts.Enabled)
            .SelectMany(ts => ts.Tasks)
            .ToList();
    }

    /// <summary>
    /// Enable a task
    /// </summary>
    public void EnableTask(string taskId)
    {
        foreach (var taskSet in _taskSets)
        {
            var task = taskSet.Tasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                task.Enabled = true;
                task.NextRun = CalculateNextRun(task.CronExpression, DateTime.UtcNow);
                _taskSetConfig.SaveTaskSet(taskSet);
                LoadTaskSets(); // Reload
                return;
            }
        }
    }

    /// <summary>
    /// Disable a task
    /// </summary>
    public void DisableTask(string taskId)
    {
        foreach (var taskSet in _taskSets)
        {
            var task = taskSet.Tasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                task.Enabled = false;
                _taskSetConfig.SaveTaskSet(taskSet);
                LoadTaskSets(); // Reload
                return;
            }
        }
    }

    /// <summary>
    /// Run a task immediately (manual trigger)
    /// </summary>
    public void RunTaskNow(string taskId)
    {
        foreach (var taskSet in _taskSets)
        {
            var task = taskSet.Tasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                ExecuteTask(task, taskSet.Name);

                // Update last run time
                task.LastRun = DateTime.UtcNow;
                _taskSetConfig.SaveTaskSet(taskSet);
                LoadTaskSets(); // Reload
                return;
            }
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _schedulerTimer?.Dispose();
        _executor?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
