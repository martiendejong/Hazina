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

    // ============================================
    // Version Management & Updates
    // ============================================

    /// <summary>
    /// Update an existing plugin with new source code (creates new version)
    /// </summary>
    public async Task<string> UpdatePluginAsync(
        string pluginId,
        string newSourceCode,
        string? changeDescription = null,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating plugin: {PluginId}", pluginId);

        // Get current plugin metadata
        var currentMetadata = await _repository.GetByIdAsync(pluginId, cancellationToken);
        if (currentMetadata == null)
        {
            throw new InvalidOperationException($"Plugin not found: {pluginId}");
        }

        // Get current active version (or create initial version)
        var currentVersion = await _repository.GetActiveVersionAsync(pluginId, cancellationToken);
        var nextVersionNumber = (currentVersion?.Version ?? 0) + 1;

        // Create new version entry
        var newVersion = new PluginVersion
        {
            VersionId = Guid.NewGuid().ToString(),
            PluginId = pluginId,
            Version = nextVersionNumber,
            SourceCode = newSourceCode,
            ChangeDescription = changeDescription,
            CreatedBy = updatedBy ?? "AI",
            IsActive = true  // Will be set as active
        };

        // Save new version
        await _repository.SaveVersionAsync(newVersion, cancellationToken);

        // Set as active version (deactivates old versions)
        await _repository.SetActiveVersionAsync(pluginId, nextVersionNumber, cancellationToken);

        // Update plugin metadata with new source code
        var updatedMetadata = new PluginMetadata
        {
            Id = currentMetadata.Id,
            Name = currentMetadata.Name,
            Version = $"{currentMetadata.Version.Split('.')[0]}.{nextVersionNumber}.0",
            Description = currentMetadata.Description,
            SourceCode = newSourceCode,
            CreatedBy = currentMetadata.CreatedBy,
            CreatedAt = currentMetadata.CreatedAt,
            Enabled = currentMetadata.Enabled,
            Tags = currentMetadata.Tags,
            AdditionalMetadata = currentMetadata.AdditionalMetadata
        };

        await _repository.SaveAsync(updatedMetadata, cancellationToken);

        // Invalidate cache and recompile
        await RecompilePluginAsync(pluginId, updatedMetadata, cancellationToken);

        _logger.LogInformation("Plugin updated successfully: {PluginId}, Version: {Version}",
            pluginId, nextVersionNumber);

        return newVersion.VersionId;
    }

    /// <summary>
    /// Rollback a plugin to a previous version
    /// </summary>
    public async Task<bool> RollbackPluginAsync(
        string pluginId,
        int targetVersionNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rolling back plugin {PluginId} to version {Version}",
            pluginId, targetVersionNumber);

        // Get target version
        var targetVersion = await _repository.GetVersionAsync(pluginId, targetVersionNumber, cancellationToken);
        if (targetVersion == null)
        {
            _logger.LogWarning("Target version not found: {PluginId} v{Version}",
                pluginId, targetVersionNumber);
            return false;
        }

        // Get current plugin metadata
        var currentMetadata = await _repository.GetByIdAsync(pluginId, cancellationToken);
        if (currentMetadata == null)
        {
            _logger.LogWarning("Plugin not found: {PluginId}", pluginId);
            return false;
        }

        // Set target version as active
        await _repository.SetActiveVersionAsync(pluginId, targetVersionNumber, cancellationToken);

        // Update plugin metadata with rolled-back source code
        var rolledBackMetadata = new PluginMetadata
        {
            Id = currentMetadata.Id,
            Name = currentMetadata.Name,
            Version = $"{currentMetadata.Version.Split('.')[0]}.{targetVersionNumber}.0",
            Description = currentMetadata.Description,
            SourceCode = targetVersion.SourceCode,
            CreatedBy = currentMetadata.CreatedBy,
            CreatedAt = currentMetadata.CreatedAt,
            Enabled = currentMetadata.Enabled,
            Tags = currentMetadata.Tags,
            AdditionalMetadata = currentMetadata.AdditionalMetadata
        };

        await _repository.SaveAsync(rolledBackMetadata, cancellationToken);

        // Invalidate cache and recompile
        await RecompilePluginAsync(pluginId, rolledBackMetadata, cancellationToken);

        _logger.LogInformation("Plugin rolled back successfully: {PluginId} to version {Version}",
            pluginId, targetVersionNumber);

        return true;
    }

    /// <summary>
    /// Get version history for a plugin
    /// </summary>
    public async Task<List<PluginVersion>> GetPluginVersionsAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetVersionsAsync(pluginId, cancellationToken);
    }

    /// <summary>
    /// Recompile a plugin and update cache (used after update/rollback)
    /// </summary>
    private async Task RecompilePluginAsync(
        string pluginId,
        PluginMetadata metadata,
        CancellationToken cancellationToken)
    {
        await _compilationLock.WaitAsync(cancellationToken);
        try
        {
            // Remove old compiled version from cache
            if (_pluginCache.TryRemove(pluginId, out var oldCompiled))
            {
                oldCompiled.Dispose();
            }

            // Compile new version
            var newCompiled = await _compiler.CompileAsync(metadata);

            // Update cache
            _pluginCache.TryAdd(pluginId, newCompiled);

            _logger.LogInformation("Plugin recompiled and cache updated: {PluginId}", pluginId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recompile plugin: {PluginId}", pluginId);
            throw;
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
