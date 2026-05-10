using Hazina.AI.Routing.Models;

namespace Hazina.AI.Routing.Interfaces;

/// <summary>
/// Multi-agent delegation service that fans out queries to multiple LLM agents
/// with differentiated perspectives and synthesizes their results.
/// </summary>
public interface IPolymathDelegationService
{
    /// <summary>
    /// Analyzes a query by delegating to multiple agents with different perspectives,
    /// then synthesizes their responses using the specified strategy.
    /// </summary>
    /// <param name="query">The query or task to analyze.</param>
    /// <param name="agentCount">Number of agents to use (capped at MaxAgents).</param>
    /// <param name="strategy">How to synthesize the multiple agent responses.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Synthesized result from multiple agents.</returns>
    Task<PolymathResult> AnalyzeAsync(
        string query,
        int agentCount,
        SynthesisStrategy strategy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Maximum number of concurrent agents allowed (cost guardrail).
    /// </summary>
    int MaxAgents { get; }
}
