using Hazina.Agent.API.Models;

namespace Hazina.Agent.API.Services;

public interface IAgentExecutionService
{
    IAsyncEnumerable<AgentEvent> ExecuteAsync(
        AgentRequest request,
        CancellationToken ct = default);
}
