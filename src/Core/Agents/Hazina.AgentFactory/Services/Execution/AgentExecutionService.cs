using Hazina.AgentFactory.Core;
using Hazina.LLMs;

namespace Hazina.AgentFactory.Services.Execution;

/// <summary>
/// Service responsible for agent and flow execution.
/// Extracted from AgentFactory to follow Single Responsibility Principle.
/// </summary>
public class AgentExecutionService : IAgentExecutionService
{
    private readonly Dictionary<string, HazinaAgent> _agents;
    private readonly Dictionary<string, HazinaFlow> _flows;
    private readonly List<HazinaChatMessage> _messages;
    private readonly Func<bool> _getWriteMode;
    private readonly Action<bool> _setWriteMode;

    private const string WriteModeText = "\nYou are now in write mode. You cannot call any other {agent}_write tools or write file tools in this mode. The file modifications need to be included in your response.";
    private const string CoderModeText = "\nALL YOUR MODIFICATIONS MUST ALWAYS SUPPLY THE WHOLE FILE. NEVER leave anything out and NEVER replace it with something like /* the rest of the code goes here */ or /* the rest of the code stays the same */";

    public AgentExecutionService(
        Dictionary<string, HazinaAgent> agents,
        Dictionary<string, HazinaFlow> flows,
        List<HazinaChatMessage> messages,
        Func<bool> getWriteMode,
        Action<bool> setWriteMode)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _flows = flows ?? throw new ArgumentNullException(nameof(flows));
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
        _getWriteMode = getWriteMode ?? throw new ArgumentNullException(nameof(getWriteMode));
        _setWriteMode = setWriteMode ?? throw new ArgumentNullException(nameof(setWriteMode));
    }

    public async Task<string> CallAgentAsync(string name, string query, string caller, CancellationToken cancel)
    {
        var agent = _agents[name];
        var id = Guid.NewGuid().ToString();
        agent.Tools.SendMessage(id, name, query);

        Guid messageId = Guid.NewGuid();
        var message = new HazinaChatMessage
        {
            MessageId = messageId,
            Role = HazinaMessageRole.Assistant,
            Text = $"{caller}: {query}",
            AgentName = name,
            FunctionName = string.Empty,
            FlowName = string.Empty,
            Response = string.Empty
        };
        _messages.Add(message);

        var responseObj = await agent.Generator.GetResponse(query + (_getWriteMode() ? WriteModeText : ""), cancel, null, true, true, agent.Tools, null);
        string response = responseObj.Result;

        // Find the message by MessageId and update Response
        var storedMsg = _messages.FirstOrDefault(m => m.MessageId == messageId);
        if (storedMsg != null) storedMsg.Response = response;

        // Log reply as new message entry for history
        var replyMsg = new HazinaChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = HazinaMessageRole.Assistant,
            Text = $"{name}: {response}",
            AgentName = name,
            FunctionName = string.Empty,
            FlowName = string.Empty,
            Response = response
        };
        _messages.Add(replyMsg);
        agent.Tools.SendMessage(id, name, response);

        return response;
    }

    public async Task<string> CallFlowAsync(string name, string query, string caller, CancellationToken cancel)
    {
        var flow = _flows[name];
        foreach (var agent in flow.CallsAgents)
        {
            if (_agents[agent].IsCoder && !_getWriteMode())
            {
                _setWriteMode(true);
                query = await CallCoderAgentAsync(agent, query, caller, cancel, flow.Name);
                _setWriteMode(false);
            }
            else
            {
                query = await CallAgentWithMetaAsync(agent, query, caller, string.Empty, flow.Name, cancel);
            }
        }
        return query;
    }

    public async Task<string> CallCoderAgentAsync(string name, string query, string caller, CancellationToken cancel, string flowName = "")
    {
        var agent = _agents[name];
        var id = Guid.NewGuid().ToString();
        agent.Tools.SendMessage(id, name, query);

        Guid messageId = Guid.NewGuid();
        var message = new HazinaChatMessage
        {
            MessageId = messageId,
            Role = HazinaMessageRole.Assistant,
            Text = $"{caller}: {query}",
            AgentName = name,
            FunctionName = "CodeModify",
            FlowName = flowName,
            Response = string.Empty
        };
        _messages.Add(message);

        var responseObj = await agent.Generator.UpdateStore(query + WriteModeText + CoderModeText, cancel, null, true, true, agent.Tools, null);
        string response = responseObj.Result;

        var storedMsg = _messages.FirstOrDefault(m => m.MessageId == messageId);
        if (storedMsg != null) storedMsg.Response = response;

        var replyMsg = new HazinaChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = HazinaMessageRole.Assistant,
            Text = $"{name}: {response}",
            AgentName = name,
            FunctionName = "CodeModify",
            FlowName = flowName,
            Response = response
        };
        _messages.Add(replyMsg);
        agent.Tools.SendMessage(id, name, response);

        return response;
    }

    public async Task<string> CallAgentWithMetaAsync(string name, string query, string caller, string functionName, string flowName, CancellationToken cancel)
    {
        var agent = _agents[name];
        var id = Guid.NewGuid().ToString();
        agent.Tools.SendMessage(id, name, query);

        Guid messageId = Guid.NewGuid();
        var message = new HazinaChatMessage
        {
            MessageId = messageId,
            Role = HazinaMessageRole.Assistant,
            Text = $"{caller}: {query}",
            AgentName = name,
            FunctionName = functionName ?? string.Empty,
            FlowName = flowName ?? string.Empty,
            Response = string.Empty
        };
        _messages.Add(message);

        var responseObj = await agent.Generator.GetResponse(query + (_getWriteMode() ? WriteModeText : ""), cancel, null, true, true, agent.Tools, null);
        string response = responseObj.Result;

        var storedMsg = _messages.FirstOrDefault(m => m.MessageId == messageId);
        if (storedMsg != null) storedMsg.Response = response;

        var replyMsg = new HazinaChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = HazinaMessageRole.Assistant,
            Text = $"{name}: {response}",
            AgentName = name,
            FunctionName = functionName ?? string.Empty,
            FlowName = flowName ?? string.Empty,
            Response = response
        };
        _messages.Add(replyMsg);
        agent.Tools.SendMessage(id, name, response);

        return response;
    }
}
