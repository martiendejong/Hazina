using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Configuration;

/// <summary>
/// Manages configuration versioning and migration.
/// Improvement #14: Config versioning
/// Improvement #15: Migration configs
/// </summary>
public class ConfigVersioning
{
    private const int CurrentVersion = 1;
    private readonly string _configDirectory;

    public ConfigVersioning(string configDirectory)
    {
        _configDirectory = configDirectory;
    }

    /// <summary>
    /// Checks if configuration needs migration
    /// </summary>
    public bool NeedsMigration(ConfigMetadata metadata)
    {
        return metadata.Version < CurrentVersion;
    }

    /// <summary>
    /// Migrates configuration from older version to current
    /// </summary>
    public async Task<HazinaCoderConfiguration> MigrateAsync(HazinaCoderConfiguration oldConfig, int fromVersion)
    {
        var config = oldConfig;

        // Backup old configuration
        await BackupConfigurationAsync(oldConfig, fromVersion);

        // Apply migrations sequentially
        for (int v = fromVersion; v < CurrentVersion; v++)
        {
            config = await ApplyMigrationAsync(config, v, v + 1);
        }

        return config;
    }

    private async Task<HazinaCoderConfiguration> ApplyMigrationAsync(
        HazinaCoderConfiguration config,
        int fromVersion,
        int toVersion)
    {
        Console.WriteLine($"[Migration] Migrating configuration from v{fromVersion} to v{toVersion}");

        // Example migration logic
        if (fromVersion == 0 && toVersion == 1)
        {
            // v0 -> v1: Add new performance settings
            if (config.Performance.ThreadPoolMinThreads == 0)
            {
                config.Performance.ThreadPoolMinThreads = 4;
            }
            if (config.Performance.ThreadPoolMaxThreads == 0)
            {
                config.Performance.ThreadPoolMaxThreads = 16;
            }
        }

        await Task.CompletedTask;
        return config;
    }

    private async Task BackupConfigurationAsync(HazinaCoderConfiguration config, int version)
    {
        var backupDir = Path.Combine(_configDirectory, "backups");
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupFile = Path.Combine(backupDir, $"appsettings_v{version}_{timestamp}.json");

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(backupFile, json);
        Console.WriteLine($"[Migration] Backup created: {backupFile}");
    }

    /// <summary>
    /// Saves configuration metadata
    /// </summary>
    public async Task SaveMetadataAsync(ConfigMetadata metadata)
    {
        var metadataFile = Path.Combine(_configDirectory, "config.metadata.json");
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(metadataFile, json);
    }

    /// <summary>
    /// Loads configuration metadata
    /// </summary>
    public async Task<ConfigMetadata> LoadMetadataAsync()
    {
        var metadataFile = Path.Combine(_configDirectory, "config.metadata.json");

        if (!File.Exists(metadataFile))
        {
            return new ConfigMetadata { Version = CurrentVersion };
        }

        var json = await File.ReadAllTextAsync(metadataFile);
        return JsonSerializer.Deserialize<ConfigMetadata>(json) ?? new ConfigMetadata { Version = CurrentVersion };
    }
}

public class ConfigMetadata
{
    public int Version { get; set; }
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public string ModifiedBy { get; set; } = Environment.UserName;
    public Dictionary<string, string> Tags { get; set; } = new();
}
