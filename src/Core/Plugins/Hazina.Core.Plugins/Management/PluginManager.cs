using System.Collections.Concurrent;
using Hazina.Core.Plugins.Abstractions;
using Hazina.Core.Plugins.Compilation;
using Hazina.Core.Plugins.Execution;
using Microsoft.Extensions.Logging;

namespace Hazina.Core.Plugins.Management;

/// <summary>
/// Manages plugin lifecycle: registration, compilation, caching, and execution
/// </summary>
public class PluginManager : IDisposable
{
    private readonly HazinaPluginCompiler _compiler;
    private readonly PluginSandbox _sandbox;
    private readonly IPluginRepository _repository;
    private readonly ILogger<PluginManager> _logger;
    private readonly ConcurrentDictionary<string, CompiledPlugin> _pluginCache;
    private readonly SemaphoreSlim _compilationLock = new(1, 1);

    public PluginManager(
        HazinaPluginCompiler compiler,
        PluginSandbox sandbox,
        IPluginRepository repository,
        ILogger<PluginManager> logger)
    {
        _compiler = compiler;
        _sandbox = sandbox;
        _repository = repository;
        _logger = logger;
        _pluginCache = new ConcurrentDictionary<string, CompiledPlugin>();
    }

    /// <summary>
    /// Register a new plugin (AI-generated or user-provided)
    /// </summary>
    public async Task<string> RegisterPluginAsync(
        PluginMetadata metadata,
        bool compileImmediately = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering plugin: {PluginName}", metadata.Name);

        // Validate metadata
        if (string.IsNullOrWhiteSpace(metadata.SourceCode))
        {
            throw new ArgumentException("Plugin source code cannot be empty", nameof(metadata));
        }

        // Save to repository
        var pluginId = await _repository.SaveAsync(metadata, cancellationToken);

        // Optionally compile immediately
        if (compileImmediately)
        {
            try
            {
                var compiled = await _compiler.CompileAsync(metadata);
                _pluginCache.TryAdd(pluginId, compiled);
                _logger.LogInformation("Plugin compiled and cached: {PluginName}", metadata.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile plugin on registration: {PluginName}", metadata.Name);
                // Don't fail registration if compilation fails - can be retried later
            }
        }

        return pluginId;
    }

    /// <summary>
    /// Execute a plugin by ID
    /// </summary>
    public async Task<PluginResult> ExecutePluginAsync(
        string pluginId,
        PluginContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing plugin: {PluginId}", pluginId);

        // Get or compile plugin
        var compiled = await GetOrCompilePluginAsync(pluginId, cancellationToken);
        if (compiled == null)
        {
            return PluginResult.Failed($"Plugin not found or disabled: {pluginId}");
        }

        // Create instance
        var pluginInstance = compiled.CreateInstance();

        // Execute in sandbox
        return await _sandbox.ExecuteAsync(pluginInstance, context);
    }

    /// <summary>
    /// Execute a plugin by name
    /// </summary>
    public async Task<PluginResult> ExecutePluginByNameAsync(
        string pluginName,
        PluginContext context,
        CancellationToken cancellationToken = default)
    {
        var metadata = await _repository.GetByNameAsync(pluginName, cancellationToken);
        if (metadata == null)
        {
            return PluginResult.Failed($"Plugin not found: {pluginName}");
        }

        return await ExecutePluginAsync(metadata.Id, context, cancellationToken);
    }

    /// <summary>
    /// Get list of all registered plugins
    /// </summary>
    public async Task<List<PluginMetadata>> ListPluginsAsync(
        bool enabledOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (enabledOnly)
        {
            return await _repository.GetEnabledAsync(cancellationToken);
        }

        return await _repository.GetAllAsync(cancellationToken);
    }

    /// <summary>
    /// Get plugin metadata by ID
    /// </summary>
    public async Task<PluginMetadata?> GetPluginMetadataAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(pluginId, cancellationToken);
    }

    /// <summary>
    /// Delete a plugin
    /// </summary>
    public async Task<bool> DeletePluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting plugin: {PluginId}", pluginId);

        // Remove from cache
        if (_pluginCache.TryRemove(pluginId, out var compiled))
        {
            compiled.Dispose();
        }

        // Delete from repository
        return await _repository.DeleteAsync(pluginId, cancellationToken);
    }

    /// <summary>
    /// Enable or disable a plugin
    /// </summary>
    public async Task<bool> SetPluginEnabledAsync(
        string pluginId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting plugin {PluginId} enabled: {Enabled}", pluginId, enabled);

        // Update repository
        var success = await _repository.SetEnabledAsync(pluginId, enabled, cancellationToken);

        // Remove from cache if disabled
        if (success && !enabled && _pluginCache.TryRemove(pluginId, out var compiled))
        {
            compiled.Dispose();
        }

        return success;
    }

    /// <summary>
    /// Clear the compilation cache
    /// </summary>
    public void ClearCache()
    {
        _logger.LogInformation("Clearing plugin cache ({Count} plugins)", _pluginCache.Count);

        foreach (var kvp in _pluginCache)
        {
            kvp.Value.Dispose();
        }

        _pluginCache.Clear();
    }

    /// <summary>
    /// Get or compile plugin (with caching)
    /// </summary>
    private async Task<CompiledPlugin?> GetOrCompilePluginAsync(
        string pluginId,
        CancellationToken cancellationToken)
    {
        // Check cache first
        if (_pluginCache.TryGetValue(pluginId, out var cached))
        {
            return cached;
        }

        // Get metadata
        var metadata = await _repository.GetByIdAsync(pluginId, cancellationToken);
        if (metadata == null || !metadata.Enabled)
        {
            _logger.LogWarning("Plugin not found or disabled: {PluginId}", pluginId);
            return null;
        }

        // Compile (with lock to prevent duplicate compilation)
        await _compilationLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check cache after acquiring lock
            if (_pluginCache.TryGetValue(pluginId, out cached))
            {
                return cached;
            }

            // Compile
            var compiled = await _compiler.CompileAsync(metadata);

            // Cache
            _pluginCache.TryAdd(pluginId, compiled);

            return compiled;
        }
        finally
        {
            _compilationLock.Release();
        }
    }

    public void Dispose()
    {
        ClearCache();
        _compilationLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
