using Hazina.AI.OpenCode.Models;

namespace Hazina.AI.OpenCode;

/// <summary>
/// Service for interacting with OpenCode multi-agent orchestration
/// </summary>
public interface IOpenCodeService
{
    /// <summary>
    /// Execute a task in the SaaS development context (client-manager, hazina, brand2boost)
    /// </summary>
    Task<string> ExecuteInSaasContextAsync(string task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a task in the WordPress context (artrevisionist)
    /// </summary>
    Task<string> ExecuteInWordPressContextAsync(string task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a read-only research/analysis task
    /// </summary>
    Task<string> ResearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a task on a specific agent
    /// </summary>
    Task<string> ExecuteAsync(string agentName, OpenCodeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream responses from an agent
    /// </summary>
    IAsyncEnumerable<string> StreamAsync(string agentName, OpenCodeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get status of all configured agents
    /// </summary>
    Task<List<AgentInfo>> GetAgentStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a specific agent is healthy
    /// </summary>
    Task<bool> IsAgentHealthyAsync(string agentName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Route a task to the most appropriate agent based on project context
    /// </summary>
    Task<string> SmartRouteAsync(string task, string? projectHint = null, CancellationToken cancellationToken = default);
}
