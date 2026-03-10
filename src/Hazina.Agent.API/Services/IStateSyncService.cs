using Hazina.Agent.API.Models;

namespace Hazina.Agent.API.Services;

public interface IStateSyncService
{
    Task<AgentIdentity> GetIdentityAsync(CancellationToken ct = default);
    Task SyncStateAsync(CancellationToken ct = default);
    Task PublishLearningEventAsync(LearningEvent learningEvent, CancellationToken ct = default);
    Task<List<LearningEvent>> GetNewLearningEventsAsync(DateTime since, CancellationToken ct = default);
    Task<bool> HasConflictsAsync(CancellationToken ct = default);
    Task ResolveConflictsAsync(CancellationToken ct = default);
}
