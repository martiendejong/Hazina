namespace Hazina.Brain.Api;

/// <summary>
/// Query for recalling relevant memories (episodes + facts) for a given prompt.
/// This is the primary retrieval API - returns context to augment LLM prompts.
/// </summary>
/// <param name="StoreId">Store identifier - required for multi-tenant isolation</param>
/// <param name="AgentId">Optional agent identifier - filters to specific agent's memories</param>
/// <param name="UserId">Optional user identifier - filters to specific user's memories</param>
/// <param name="Prompt">The user's current query/input to find relevant context for</param>
/// <param name="EpisodesTopK">Number of most relevant episodes to return (default: 8)</param>
/// <param name="FactsTopK">Number of most relevant facts to return (default: 16)</param>
public sealed record RecallQuery(
    string StoreId,
    string? AgentId,
    string? UserId,
    string Prompt,
    int EpisodesTopK = 8,
    int FactsTopK = 16
);
