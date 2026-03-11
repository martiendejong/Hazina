using System.Text.Json;

namespace Hazina.TaskRunner.Scheduling;

/// <summary>
/// Manages persistent storage of scheduled tasks in JSON format
/// </summary>
public class TaskConfigurationManager
{
    private readonly string _configFilePath;
    private readonly object _fileLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TaskConfigurationManager(string configFilePath)
    {
        _configFilePath = configFilePath;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(configFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Create empty config if doesn't exist
        if (!File.Exists(_configFilePath))
        {
            var emptyConfig = new TaskConfiguration { Tasks = new List<ScheduledTask>() };
            SaveConfiguration(emptyConfig);
        }
    }

    /// <summary>
    /// Load all tasks from JSON file
    /// </summary>
    public TaskConfiguration LoadConfiguration()
    {
        lock (_fileLock)
        {
            try
            {
                var json = File.ReadAllText(_configFilePath);
                var config = JsonSerializer.Deserialize<TaskConfiguration>(json, JsonOptions);
                return config ?? new TaskConfiguration { Tasks = new List<ScheduledTask>() };
            }
            catch (JsonException)
            {
                // If JSON is invalid, return empty configuration
                return new TaskConfiguration { Tasks = new List<ScheduledTask>() };
            }
        }
    }

    /// <summary>
    /// Save all tasks to JSON file
    /// </summary>
    public void SaveConfiguration(TaskConfiguration configuration)
    {
        lock (_fileLock)
        {
            var json = JsonSerializer.Serialize(configuration, JsonOptions);
            File.WriteAllText(_configFilePath, json);
        }
    }

    /// <summary>
    /// Add or update a task
    /// </summary>
    public void SaveTask(ScheduledTask task)
    {
        var config = LoadConfiguration();

        var existingIndex = config.Tasks.FindIndex(t => t.Id == task.Id);
        if (existingIndex >= 0)
        {
            config.Tasks[existingIndex] = task;
        }
        else
        {
            config.Tasks.Add(task);
        }

        SaveConfiguration(config);
    }

    /// <summary>
    /// Remove a task by ID
    /// </summary>
    public bool RemoveTask(string taskId)
    {
        var config = LoadConfiguration();
        var removed = config.Tasks.RemoveAll(t => t.Id == taskId) > 0;

        if (removed)
        {
            SaveConfiguration(config);
        }

        return removed;
    }

    /// <summary>
    /// Get a task by ID
    /// </summary>
    public ScheduledTask? GetTask(string taskId)
    {
        var config = LoadConfiguration();
        return config.Tasks.FirstOrDefault(t => t.Id == taskId);
    }
}

/// <summary>
/// Root configuration object containing all tasks
/// </summary>
public class TaskConfiguration
{
    public List<ScheduledTask> Tasks { get; set; } = new();
}
