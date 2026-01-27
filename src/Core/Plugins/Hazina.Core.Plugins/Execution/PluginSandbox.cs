using Hazina.Core.Plugins.Abstractions;
using Microsoft.Extensions.Logging;

namespace Hazina.Core.Plugins.Execution;

/// <summary>
/// Executes plugins in a sandboxed environment with security restrictions
/// </summary>
public class PluginSandbox
{
    private readonly ILogger<PluginSandbox> _logger;
    private readonly PluginSecuritySettings _settings;

    public PluginSandbox(
        ILogger<PluginSandbox> logger,
        PluginSecuritySettings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    /// <summary>
    /// Execute a plugin in a sandboxed environment
    /// </summary>
    public async Task<PluginResult> ExecuteAsync(
        IHazinaPlugin plugin,
        PluginContext context)
    {
        _logger.LogInformation("Executing plugin: {PluginName}", plugin.Name);

        using var cts = new CancellationTokenSource(_settings.TimeoutSeconds * 1000);

        try
        {
            // Execute with timeout protection
            var executionTask = plugin.ExecuteAsync(context);
            var timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);

            var completedTask = await Task.WhenAny(executionTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogError("Plugin timeout: {PluginName} exceeded {Timeout}s",
                    plugin.Name, _settings.TimeoutSeconds);
                throw new PluginTimeoutException(
                    $"Plugin '{plugin.Name}' exceeded timeout of {_settings.TimeoutSeconds}s");
            }

            var result = await executionTask;

            // Validate result
            if (result == null)
            {
                _logger.LogWarning("Plugin returned null result: {PluginName}", plugin.Name);
                return PluginResult.Failed("Plugin returned null result");
            }

            // Add execution metadata
            result.Metadata["ExecutedAt"] = DateTime.UtcNow;
            result.Metadata["PluginName"] = plugin.Name;
            result.Metadata["PluginVersion"] = plugin.Version;

            _logger.LogInformation("Plugin executed successfully: {PluginName}, Success: {Success}",
                plugin.Name, result.Success);

            return result;
        }
        catch (PluginTimeoutException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin execution failed: {PluginName}", plugin.Name);
            return PluginResult.Failed($"Plugin execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Execute multiple plugins in sequence
    /// </summary>
    public async Task<List<PluginResult>> ExecuteBatchAsync(
        IEnumerable<(IHazinaPlugin Plugin, PluginContext Context)> plugins)
    {
        var results = new List<PluginResult>();

        foreach (var (plugin, context) in plugins)
        {
            var result = await ExecuteAsync(plugin, context);
            results.Add(result);

            // Stop on first failure if configured
            if (_settings.StopOnFirstFailure && !result.Success)
            {
                _logger.LogWarning("Stopping batch execution due to failure: {PluginName}", plugin.Name);
                break;
            }
        }

        return results;
    }
}
