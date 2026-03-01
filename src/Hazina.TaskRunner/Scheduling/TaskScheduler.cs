using Cronos;
using Hazina.TaskRunner.PowerShell;

namespace Hazina.TaskRunner.Scheduling;

/// <summary>
/// Main task scheduling engine that executes scheduled PowerShell scripts
/// </summary>
public class TaskScheduler : IDisposable
{
    private readonly TaskConfigurationManager _configManager;
    private readonly PowerShellExecutor _executor;
    private readonly Timer _schedulerTimer;
    private readonly object _executionLock = new();
    private bool _disposed;

    public TaskScheduler(string configFilePath)
    {
        _configManager = new TaskConfigurationManager(configFilePath);
        _executor = new PowerShellExecutor();

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
    /// Initialize NextRun times for all tasks on startup
    /// </summary>
    private void InitializeNextRunTimes()
    {
        var config = _configManager.LoadConfiguration();
        bool changed = false;

        foreach (var task in config.Tasks.Where(t => t.Enabled))
        {
            if (task.NextRun == null || task.NextRun < DateTime.UtcNow)
            {
                task.NextRun = CalculateNextRun(task.CronExpression, DateTime.UtcNow);
                changed = true;
            }
        }

        if (changed)
        {
            _configManager.SaveConfiguration(config);
        }
    }

    /// <summary>
    /// Check all tasks and execute those that are due
    /// </summary>
    private void CheckAndExecuteTasks()
    {
        lock (_executionLock)
        {
            var config = _configManager.LoadConfiguration();
            var now = DateTime.UtcNow;
            bool configChanged = false;

            foreach (var task in config.Tasks.Where(t => t.Enabled))
            {
                // Check if task is due to run
                if (task.NextRun.HasValue && task.NextRun.Value <= now)
                {
                    // Execute task
                    ExecuteTask(task);

                    // Update timestamps
                    task.LastRun = now;
                    task.NextRun = CalculateNextRun(task.CronExpression, now);
                    configChanged = true;
                }
            }

            // Save configuration if any tasks were executed
            if (configChanged)
            {
                _configManager.SaveConfiguration(config);
            }
        }
    }

    /// <summary>
    /// Execute a single task using PowerShellExecutor
    /// </summary>
    private void ExecuteTask(ScheduledTask task)
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

            // Log result
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Task '{task.Name}' ({task.Id}) executed: Success={result.Success}, Duration={result.Duration.TotalSeconds:F2}s");

            if (!result.Success && result.Errors.Count > 0)
            {
                Console.WriteLine($"  Errors: {string.Join("; ", result.Errors)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Task '{task.Name}' ({task.Id}) failed with exception: {ex.Message}");
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

    // ====== Task Management API ======

    /// <summary>
    /// Add a new task
    /// </summary>
    public void AddTask(ScheduledTask task)
    {
        // Calculate initial next run time
        task.NextRun = CalculateNextRun(task.CronExpression, DateTime.UtcNow);
        _configManager.SaveTask(task);
    }

    /// <summary>
    /// Remove a task by ID
    /// </summary>
    public bool RemoveTask(string taskId)
    {
        return _configManager.RemoveTask(taskId);
    }

    /// <summary>
    /// Update an existing task
    /// </summary>
    public void UpdateTask(ScheduledTask task)
    {
        // Recalculate next run if cron expression changed
        var existing = _configManager.GetTask(task.Id);
        if (existing == null || existing.CronExpression != task.CronExpression)
        {
            task.NextRun = CalculateNextRun(task.CronExpression, DateTime.UtcNow);
        }

        _configManager.SaveTask(task);
    }

    /// <summary>
    /// Get a task by ID
    /// </summary>
    public ScheduledTask? GetTask(string taskId)
    {
        return _configManager.GetTask(taskId);
    }

    /// <summary>
    /// Get all tasks
    /// </summary>
    public List<ScheduledTask> GetAllTasks()
    {
        var config = _configManager.LoadConfiguration();
        return config.Tasks;
    }

    /// <summary>
    /// Enable a task
    /// </summary>
    public void EnableTask(string taskId)
    {
        var task = _configManager.GetTask(taskId);
        if (task != null)
        {
            task.Enabled = true;
            task.NextRun = CalculateNextRun(task.CronExpression, DateTime.UtcNow);
            _configManager.SaveTask(task);
        }
    }

    /// <summary>
    /// Disable a task
    /// </summary>
    public void DisableTask(string taskId)
    {
        var task = _configManager.GetTask(taskId);
        if (task != null)
        {
            task.Enabled = false;
            _configManager.SaveTask(task);
        }
    }

    /// <summary>
    /// Run a task immediately (manual trigger)
    /// </summary>
    public void RunTaskNow(string taskId)
    {
        var task = _configManager.GetTask(taskId);
        if (task != null)
        {
            ExecuteTask(task);

            // Update last run time
            task.LastRun = DateTime.UtcNow;
            _configManager.SaveTask(task);
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
