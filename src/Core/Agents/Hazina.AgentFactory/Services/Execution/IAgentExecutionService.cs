namespace Hazina.AgentFactory.Services.Execution;

/// <summary>
/// Interface for agent and flow execution operations.
/// </summary>
public interface IAgentExecutionService
{
    Task<string> CallAgentAsync(string name, string query, string caller, CancellationToken cancel);
    Task<string> CallFlowAsync(string name, string query, string caller, CancellationToken cancel);
    Task<string> CallCoderAgentAsync(string name, string query, string caller, CancellationToken cancel, string flowName = "");
    Task<string> CallAgentWithMetaAsync(string name, string query, string caller, string functionName, string flowName, CancellationToken cancel);
}
