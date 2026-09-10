namespace Hazina.AI.Routing.Models;

/// <summary>
/// Strategy for synthesizing results from multiple agents.
/// </summary>
public enum SynthesisStrategy
{
    /// <summary>Select the result that appears most frequently across agents.</summary>
    Consensus,

    /// <summary>Select the single result with the highest self-reported confidence.</summary>
    Best,

    /// <summary>Combine all agent findings into a unified response.</summary>
    Merge
}

/// <summary>
/// Result from a polymath delegation (multi-agent analysis).
/// </summary>
public class PolymathResult
{
    /// <summary>The synthesized final answer after applying the strategy.</summary>
    public required string SynthesizedResult { get; set; }

    /// <summary>Individual responses from each agent.</summary>
    public required List<AgentResponse> AgentResponses { get; set; }

    /// <summary>Strategy that was used for synthesis.</summary>
    public required SynthesisStrategy Strategy { get; set; }

    /// <summary>Number of agents that participated.</summary>
    public int AgentCount => AgentResponses.Count;

    /// <summary>Total token usage across all agents.</summary>
    public int TotalInputTokens { get; set; }

    /// <summary>Total output tokens across all agents.</summary>
    public int TotalOutputTokens { get; set; }
}

/// <summary>
/// Individual agent response within a polymath delegation.
/// </summary>
public class AgentResponse
{
    /// <summary>Zero-based index of this agent in the delegation.</summary>
    public required int AgentIndex { get; set; }

    /// <summary>The perspective/role this agent was given.</summary>
    public required string Perspective { get; set; }

    /// <summary>The agent's response text.</summary>
    public required string Response { get; set; }

    /// <summary>Input tokens used by this agent.</summary>
    public int InputTokens { get; set; }

    /// <summary>Output tokens used by this agent.</summary>
    public int OutputTokens { get; set; }
}
