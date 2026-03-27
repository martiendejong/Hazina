using Hazina.AI.Routing.Interfaces;
using Hazina.AI.Routing.Models;
using Microsoft.EntityFrameworkCore;

namespace Hazina.AI.Routing.Persistence;

/// <summary>
/// EF Core implementation of agent reputation storage.
/// Persists reputation data to a database for survival across app restarts.
/// </summary>
public class EfAgentReputationStore : IAgentReputationStore
{
    private readonly AgentRoutingDbContext _context;

    public EfAgentReputationStore(AgentRoutingDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AgentReputation?> GetAsync(AgentType agentType, TaskCategory category)
    {
        return await _context.AgentReputations
            .FirstOrDefaultAsync(r => r.AgentType == agentType && r.Category == category);
    }

    public async Task SaveAsync(AgentReputation reputation)
    {
        var existing = await _context.AgentReputations
            .FirstOrDefaultAsync(r => r.AgentType == reputation.AgentType && r.Category == reputation.Category);

        if (existing != null)
        {
            existing.TotalTasks = reputation.TotalTasks;
            existing.Successes = reputation.Successes;
            existing.Failures = reputation.Failures;
            existing.SuccessRate = reputation.SuccessRate;
            existing.AvgTurns = reputation.AvgTurns;
            existing.TrustScore = reputation.TrustScore;
            existing.FailurePatterns = reputation.FailurePatterns;
            existing.LastUpdated = reputation.LastUpdated;
            existing.Notes = reputation.Notes;
        }
        else
        {
            _context.AgentReputations.Add(reputation);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AgentReputation>> GetAllAsync()
    {
        return await _context.AgentReputations.ToListAsync();
    }
}
