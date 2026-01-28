using Hazina.AgenticOrchestration.Models;

namespace Hazina.AgenticOrchestration.Services;

public interface IClaudeInstanceManager
{
    Task<List<ClaudeInstance>> GetActiveInstancesAsync();
    Task<ClaudeInstance?> GetInstanceDetailsAsync(string sessionId);
}
