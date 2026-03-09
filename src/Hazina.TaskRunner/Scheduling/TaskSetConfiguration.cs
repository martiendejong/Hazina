using System.Text.Json;

namespace Hazina.TaskRunner.Scheduling;

/// <summary>
/// Manages loading and saving of task set configurations
/// </summary>
public class TaskSetConfiguration
{
    private readonly string _configDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public TaskSetConfiguration(string configDirectory)
    {
        _configDirectory = configDirectory;
    }

    /// <summary>
    /// Load all task sets from the configuration directory
    /// </summary>
    public List<TaskSet> LoadAllTaskSets()
    {
        var taskSets = new List<TaskSet>();

        // Ensure directory exists
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
            return taskSets;
        }

        // Load all .json files in the config directory
        var configFiles = Directory.GetFiles(_configDirectory, "*.json");

        foreach (var configFile in configFiles)
        {
            try
            {
                var taskSet = LoadTaskSet(configFile);
                if (taskSet != null)
                {
                    taskSets.Add(taskSet);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - skip invalid config files
                Console.WriteLine($"Error loading config file {configFile}: {ex.Message}");
            }
        }

        // Backward compatibility: Look for legacy tasks.json format
        var legacyFile = Path.Combine(_configDirectory, "tasks.json");
        if (File.Exists(legacyFile))
        {
            try
            {
                var legacyTaskSet = LoadLegacyTasksFile(legacyFile);
                if (legacyTaskSet != null && !taskSets.Any(ts => ts.Id == legacyTaskSet.Id))
                {
                    taskSets.Add(legacyTaskSet);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading legacy tasks.json: {ex.Message}");
            }
        }

        return taskSets;
    }

    /// <summary>
    /// Load a task set from a JSON file
    /// </summary>
    private TaskSet? LoadTaskSet(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var taskSet = JsonSerializer.Deserialize<TaskSet>(json, JsonOptions);

        if (taskSet != null)
        {
            // Use filename as ID if not specified
            if (string.IsNullOrEmpty(taskSet.Id))
            {
                taskSet.Id = Path.GetFileNameWithoutExtension(filePath);
            }

            // Use ID as name if not specified
            if (string.IsNullOrEmpty(taskSet.Name))
            {
                taskSet.Name = taskSet.Id;
            }
        }

        return taskSet;
    }

    /// <summary>
    /// Load legacy tasks.json format (flat list of tasks)
    /// </summary>
    private TaskSet? LoadLegacyTasksFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var legacyConfig = JsonSerializer.Deserialize<LegacyTaskConfiguration>(json, JsonOptions);

        if (legacyConfig?.Tasks == null || legacyConfig.Tasks.Count == 0)
        {
            return null;
        }

        return new TaskSet
        {
            Id = "default",
            Name = "Default Tasks",
            Description = "Legacy tasks from tasks.json",
            Enabled = true,
            Tasks = legacyConfig.Tasks
        };
    }

    /// <summary>
    /// Save a task set to its configuration file
    /// </summary>
    public void SaveTaskSet(TaskSet taskSet)
    {
        var fileName = $"{taskSet.Id}.json";
        var filePath = Path.Combine(_configDirectory, fileName);

        var json = JsonSerializer.Serialize(taskSet, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Save all task sets to their respective files
    /// </summary>
    public void SaveAllTaskSets(List<TaskSet> taskSets)
    {
        foreach (var taskSet in taskSets)
        {
            SaveTaskSet(taskSet);
        }
    }

    /// <summary>
    /// Legacy format for backward compatibility
    /// </summary>
    private class LegacyTaskConfiguration
    {
        public List<ScheduledTask> Tasks { get; set; } = new();
    }
}
