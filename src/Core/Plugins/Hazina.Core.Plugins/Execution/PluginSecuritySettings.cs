namespace Hazina.Core.Plugins.Execution;

/// <summary>
/// Security and resource limits for plugin execution
/// </summary>
public class PluginSecuritySettings
{
    /// <summary>
    /// Maximum execution time in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum memory usage in bytes (not currently enforced)
    /// </summary>
    public long MaxMemoryBytes { get; set; } = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Whether to stop batch execution on first failure
    /// </summary>
    public bool StopOnFirstFailure { get; set; } = false;

    /// <summary>
    /// Whether to allow file system access (future feature)
    /// </summary>
    public bool AllowFileSystemAccess { get; set; } = false;

    /// <summary>
    /// Whether to allow network access (future feature)
    /// </summary>
    public bool AllowNetworkAccess { get; set; } = true;

    /// <summary>
    /// Maximum number of concurrent plugin executions
    /// </summary>
    public int MaxConcurrentExecutions { get; set; } = 10;
}
