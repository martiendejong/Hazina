using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AI.PromptManagement.Safety;

/// <summary>
/// Storage interface for safety configurations
/// </summary>
public interface ISafetyConfigStore
{
    /// <summary>
    /// Get safety configuration for a prompt
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Safety configuration or null if not found</returns>
    Task<SafetyConfig?> GetConfigAsync(
        string promptId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save safety configuration
    /// </summary>
    /// <param name="config">Safety configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveConfigAsync(
        SafetyConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete safety configuration
    /// </summary>
    /// <param name="promptId">Prompt ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteConfigAsync(
        string promptId,
        CancellationToken cancellationToken = default);
}
