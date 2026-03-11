using Hazina.Agent.API.Models;

namespace Hazina.Agent.API.Services;

public interface ISessionLogger
{
    Task<string> CreateSessionAsync(AgentRequest request, CancellationToken ct = default);
    Task LogEventAsync(string sessionId, AgentEvent agentEvent, CancellationToken ct = default);
    Task LogCompleteAsync(string sessionId, CompleteData complete, CancellationToken ct = default);
    Task<string> GetSessionPathAsync(string sessionId);
}
