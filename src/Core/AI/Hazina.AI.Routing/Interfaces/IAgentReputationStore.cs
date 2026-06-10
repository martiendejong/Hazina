using Hazina.AI.Routing.Models;

namespace Hazina.AI.Routing.Interfaces;

/// <summary>
/// Abstraction for agent reputation persistence.
/// Implementations include in-memory (default) and EF Core (database).
/// </summary>
public interface IAgentReputationStore
{
    Task<AgentReputation?> GetAsync(AgentType agentType, TaskCategory category);
    Task SaveAsync(AgentReputation reputation);
    Task<IEnumerable<AgentReputation>> GetAllAsync();
}
