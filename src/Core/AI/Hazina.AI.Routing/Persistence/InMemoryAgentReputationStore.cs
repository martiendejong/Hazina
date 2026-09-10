using Hazina.AI.Routing.Interfaces;
using Hazina.AI.Routing.Models;
using System.Collections.Concurrent;

namespace Hazina.AI.Routing.Persistence;

/// <summary>
/// In-memory implementation of agent reputation storage.
/// Default for local development and testing. Data is lost on app restart.
/// </summary>
public class InMemoryAgentReputationStore : IAgentReputationStore
{
    private readonly ConcurrentDictionary<string, AgentReputation> _store = new();

    public Task<AgentReputation?> GetAsync(AgentType agentType, TaskCategory category)
    {
        var key = GetKey(agentType, category);
        _store.TryGetValue(key, out var reputation);
        return Task.FromResult(reputation);
    }

    public Task SaveAsync(AgentReputation reputation)
    {
        var key = GetKey(reputation.AgentType, reputation.Category);
        _store[key] = reputation;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<AgentReputation>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<AgentReputation>>(_store.Values.ToList());
    }

    private static string GetKey(AgentType agentType, TaskCategory category)
        => $"{agentType}:{category}";
}
