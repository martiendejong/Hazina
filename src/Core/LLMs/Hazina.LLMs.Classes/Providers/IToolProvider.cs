/// <summary>
/// Abstraction for tool providers that can register and manage tools dynamically.
/// Allows different tool sources (built-in, external API, plugins) to be integrated.
/// </summary>
public interface IToolProvider
{
    /// <summary>
    /// Unique identifier for this provider (e.g., "builtin", "external", "plugin:xyz")
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Human-readable name for this provider
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Description of what tools this provider offers
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Get all tools available from this provider
    /// </summary>
    Task<IReadOnlyList<HazinaChatTool>> GetToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific tool by name
    /// </summary>
    Task<HazinaChatTool?> GetToolAsync(string toolName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a tool is available from this provider
    /// </summary>
    Task<bool> HasToolAsync(string toolName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh/reload tools from the provider source
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get metadata about the provider's capabilities
    /// </summary>
    ToolProviderCapabilities GetCapabilities();
}

/// <summary>
/// Capabilities metadata for a tool provider
/// </summary>
public class ToolProviderCapabilities
{
    /// <summary>
    /// Whether this provider supports dynamic tool registration
    /// </summary>
    public bool SupportsDynamicRegistration { get; set; }

    /// <summary>
    /// Whether tools from this provider can be unregistered at runtime
    /// </summary>
    public bool SupportsUnregistration { get; set; }

    /// <summary>
    /// Whether this provider supports hot-reloading of tools
    /// </summary>
    public bool SupportsHotReload { get; set; }

    /// <summary>
    /// Whether this provider requires external configuration
    /// </summary>
    public bool RequiresConfiguration { get; set; }

    /// <summary>
    /// Maximum number of tools this provider can support (null = unlimited)
    /// </summary>
    public int? MaxTools { get; set; }

    /// <summary>
    /// Tags/categories for grouping providers
    /// </summary>
    public List<string> Tags { get; set; } = new();
}
