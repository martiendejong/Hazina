using Hazina.LLMs;

namespace Hazina.AgentFactory.Services.Tools;

/// <summary>
/// Interface for tool registration operations.
/// </summary>
public interface IToolRegistrationService
{
    void AddStoreTools(
        IEnumerable<(IDocumentStore Store, bool Write)> stores,
        IToolsContext tools,
        IEnumerable<string> functions,
        IEnumerable<string> agents,
        IEnumerable<string> flows,
        string caller);
}
