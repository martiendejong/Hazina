using System.Diagnostics.CodeAnalysis;
using Hazina.AI.ContextEngineering.Configuration;

namespace Hazina.AI.ContextEngineering.Orchestration;

/// <summary>
/// Main interface for the Context Engineering system
/// </summary>
/// <remarks>
/// This is an experimental API and may change in future versions without notice.
/// </remarks>
[Experimental("HAZ002")]
public interface IContextEngine
{
    /// <summary>
    /// Retrieve and assemble context for a query
    /// </summary>
    /// <param name="query">Query text</param>
    /// <param name="config">Configuration to use</param>
    /// <param name="queryEmbedding">Optional pre-computed query embedding</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Assembled context string ready for LLM</returns>
    Task<string> GetContextAsync(
        string query,
        ContextEngineConfig config,
        float[]? queryEmbedding = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve and assemble context using default configuration
    /// </summary>
    /// <param name="query">Query text</param>
    /// <param name="queryEmbedding">Optional pre-computed query embedding</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Assembled context string ready for LLM</returns>
    Task<string> GetContextAsync(
        string query,
        float[]? queryEmbedding = null,
        CancellationToken cancellationToken = default);
}
