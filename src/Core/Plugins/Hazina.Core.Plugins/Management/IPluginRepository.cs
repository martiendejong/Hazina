using Hazina.Core.Plugins.Abstractions;

namespace Hazina.Core.Plugins.Management;

/// <summary>
/// Repository interface for storing and retrieving plugin metadata
/// Implementations can use database, file system, or other storage
/// </summary>
public interface IPluginRepository
{
    /// <summary>
    /// Save a new plugin or update existing
    /// </summary>
    Task<string> SaveAsync(PluginMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a plugin by ID
    /// </summary>
    Task<PluginMetadata?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a plugin by name
    /// </summary>
    Task<PluginMetadata?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all plugins
    /// </summary>
    Task<List<PluginMetadata>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get enabled plugins only
    /// </summary>
    Task<List<PluginMetadata>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get plugins by tag
    /// </summary>
    Task<List<PluginMetadata>> GetByTagAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a plugin
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enable/disable a plugin
    /// </summary>
    Task<bool> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default);

    // ============================================
    // Version Management
    // ============================================

    /// <summary>
    /// Save a new version of a plugin
    /// </summary>
    Task<string> SaveVersionAsync(PluginVersion version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all versions of a plugin (ordered by version number descending)
    /// </summary>
    Task<List<PluginVersion>> GetVersionsAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific version
    /// </summary>
    Task<PluginVersion?> GetVersionAsync(string pluginId, int versionNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the active version for a plugin
    /// </summary>
    Task<PluginVersion?> GetActiveVersionAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set a version as active (deactivates all other versions for this plugin)
    /// </summary>
    Task<bool> SetActiveVersionAsync(string pluginId, int versionNumber, CancellationToken cancellationToken = default);
}
