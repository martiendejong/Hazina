using Hazina.Agent.API.Models;

namespace Hazina.Agent.API.Services;

public interface ILearningIntegrationService
{
    Task IntegrateNewLearningsAsync(List<LearningEvent> events, CancellationToken ct = default);
    Task<ConsciousnessState> GetConsciousnessStateAsync(CancellationToken ct = default);
    Task UpdateConsciousnessStateAsync(ConsciousnessState state, CancellationToken ct = default);
}
