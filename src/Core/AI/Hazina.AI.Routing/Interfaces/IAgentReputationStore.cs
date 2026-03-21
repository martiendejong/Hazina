using Hazina.AI.Routing.Models;

namespace Hazina.AI.Routing.Interfaces;

public interface IAgentReputationStore
{
    Task<AgentReputation?> GetAsync(AgentType agentType, TaskCategory category);
    Task SaveAsync(AgentReputation reputation);
    Task<IEnumerable<AgentReputation>> GetAllAsync();
}
