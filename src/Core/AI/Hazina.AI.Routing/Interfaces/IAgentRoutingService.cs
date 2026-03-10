using Hazina.AI.Routing.Models;

namespace Hazina.AI.Routing.Interfaces;

/// <summary>
/// Intelligent AI agent routing and delegation service based on Transaction Cost Economics.
/// Provides cost-optimized agent selection, reputation tracking, and delegation decision support.
/// </summary>
public interface IAgentRoutingService
{
    /// <summary>
    /// Calculates delegation cost analysis for a given task.
    /// Returns recommendation: DELEGATE or DO_MYSELF based on TCE formula.
    /// </summary>
    /// <param name="taskDescription">Description of the task to be delegated</param>
    /// <param name="agentType">Type of agent being considered</param>
    /// <param name="category">Category of the task</param>
    /// <param name="criticality">Criticality level (0-10). How bad is failure?</param>
    /// <param name="verifiability">Verifiability level (0-10). Can results be verified?</param>
    /// <param name="selfEstimateTurns">Estimated turns if doing the task yourself</param>
    /// <returns>Delegation decision with cost breakdown and recommendation</returns>
    Task<DelegationDecision> CalculateDelegationCostAsync(
        string taskDescription,
        AgentType agentType,
        TaskCategory category,
        int criticality,
        int verifiability,
        double selfEstimateTurns);

    /// <summary>
    /// Updates agent reputation after task completion.
    /// Tracks success/failure, adjusts trust scores, and identifies failure patterns.
    /// </summary>
    /// <param name="agentType">Type of agent that executed the task</param>
    /// <param name="category">Category of the task</param>
    /// <param name="outcome">Outcome of the delegation (success/failure)</param>
    /// <param name="turnsUsed">Actual turns used to complete the task</param>
    /// <param name="failurePattern">Optional failure pattern description</param>
    /// <param name="notes">Optional notes about the execution</param>
    Task UpdateAgentReputationAsync(
        AgentType agentType,
        TaskCategory category,
        DelegationOutcome outcome,
        double turnsUsed,
        string? failurePattern = null,
        string? notes = null);

    /// <summary>
    /// Gets the best agent for a given task category and criticality level.
    /// Uses reputation data to select agent with highest trust score.
    /// </summary>
    /// <param name="category">Category of the task</param>
    /// <param name="criticalityLevel">Criticality level of the task</param>
    /// <returns>Recommended agent type</returns>
    Task<AgentType> GetBestAgentAsync(
        TaskCategory category,
        int criticalityLevel);

    /// <summary>
    /// Estimates cost for a task across all available agents.
    /// Returns cost estimates for each agent type to enable comparison.
    /// </summary>
    /// <param name="taskDescription">Description of the task</param>
    /// <param name="category">Category of the task</param>
    /// <param name="criticality">Criticality level</param>
    /// <param name="verifiability">Verifiability level</param>
    /// <param name="selfEstimateTurns">Self-execution time estimate</param>
    /// <returns>Dictionary of agent types to cost estimates</returns>
    Task<Dictionary<AgentType, CostEstimate>> EstimateTaskCostAsync(
        string taskDescription,
        TaskCategory category,
        int criticality,
        int verifiability,
        double selfEstimateTurns);

    /// <summary>
    /// Retrieves learned delegation patterns.
    /// Useful for understanding which tasks benefit from delegation.
    /// </summary>
    /// <param name="agentType">Optional filter by agent type</param>
    /// <param name="category">Optional filter by task category</param>
    /// <returns>Array of delegation patterns</returns>
    Task<DelegationPattern[]> GetLearnedPatternsAsync(
        AgentType? agentType = null,
        TaskCategory? category = null);

    /// <summary>
    /// Gets reputation statistics for an agent.
    /// </summary>
    /// <param name="agentType">Agent type</param>
    /// <param name="category">Task category</param>
    /// <returns>Reputation data including success rate, trust score, etc.</returns>
    Task<AgentReputation?> GetAgentReputationAsync(
        AgentType agentType,
        TaskCategory category);
}
