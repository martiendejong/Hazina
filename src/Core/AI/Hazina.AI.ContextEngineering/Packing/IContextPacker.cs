using Hazina.AI.ContextEngineering.Configuration;
using Hazina.AI.ContextEngineering.Models;

namespace Hazina.AI.ContextEngineering.Packing;

/// <summary>
/// Interface for packing retrieval results into final context
/// </summary>
public interface IContextPacker
{
    /// <summary>
    /// Pack retrieval results into a formatted context string
    /// </summary>
    /// <param name="results">Retrieval results to pack</param>
    /// <param name="query">Original query</param>
    /// <param name="policy">Packing policy to use</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Formatted context string</returns>
    Task<string> PackAsync(
        List<RetrievalResult> results,
        string query,
        PackingPolicy policy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimate token count for a given text
    /// </summary>
    /// <param name="text">Text to estimate</param>
    /// <returns>Estimated token count</returns>
    int EstimateTokens(string text);
}
