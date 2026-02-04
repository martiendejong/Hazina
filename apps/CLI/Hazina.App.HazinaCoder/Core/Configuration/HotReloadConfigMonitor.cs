namespace Hazina.App.HazinaCoder.Core.Configuration;

/// <summary>
/// Monitors configuration files for changes and triggers hot-reload.
/// Improvement #9: Hot-reload configuration
/// </summary>
public class HotReloadConfigMonitor : IDisposable
{
    private readonly string _configDirectory;
    private readonly FileSystemWatcher _watcher;
    private readonly Action<HazinaCoderConfiguration> _onConfigChanged;
    private readonly EnvironmentConfigLoader _loader;
    private DateTime _lastReloadTime = DateTime.MinValue;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromSeconds(2);

    public HotReloadConfigMonitor(string configDirectory, Action<HazinaCoderConfiguration> onConfigChanged)
    {
        _configDirectory = configDirectory;
        _onConfigChanged = onConfigChanged;
        _loader = new EnvironmentConfigLoader(configDirectory);

        _watcher = new FileSystemWatcher(configDirectory)
        {
            Filter = "appsettings*.json",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = false
        };

        _watcher.Changed += OnConfigFileChanged;
        _watcher.Created += OnConfigFileChanged;
        _watcher.Renamed += OnConfigFileChanged;
    }

    /// <summary>
    /// Starts monitoring for configuration changes
    /// </summary>
    public void Start()
    {
        _watcher.EnableRaisingEvents = true;
        Console.WriteLine($"[HotReload] Monitoring configuration files in {_configDirectory}");
    }

    /// <summary>
    /// Stops monitoring
    /// </summary>
    public void Stop()
    {
        _watcher.EnableRaisingEvents = false;
    }

    private async void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: Ignore rapid successive changes
        var now = DateTime.UtcNow;
        if (now - _lastReloadTime < _debounceInterval)
        {
            return;
        }

        _lastReloadTime = now;

        try
        {
            // Wait a bit for file to be fully written
            await Task.Delay(500);

            Console.WriteLine($"[HotReload] Configuration file changed: {e.Name}");
            Console.WriteLine("[HotReload] Reloading configuration...");

            var newConfig = await _loader.LoadConfigurationAsync();
            var validationErrors = newConfig.Validate();

            if (validationErrors.Any())
            {
                Console.WriteLine("[HotReload] Configuration validation failed:");
                foreach (var error in validationErrors)
                {
                    Console.WriteLine($"  - {error}");
                }
                return;
            }

            _onConfigChanged(newConfig);
            Console.WriteLine("[HotReload] Configuration reloaded successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HotReload] Failed to reload configuration: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
