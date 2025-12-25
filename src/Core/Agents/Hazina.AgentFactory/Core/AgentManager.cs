using System.Text.Json;

using Newtonsoft.Json.Serialization;

using static Google.Apis.Requests.BatchRequest;

/// <summary>
/// AgentManager encapsulates all logic for agent and store initialization, configuration,
/// and provides interfaces for agent interaction. It loads configuration from paths provided
/// in the constructor and is the single point of ownership for DevGPT agents and stores,
/// so the rest of the application can treat it entirely as a black box for agent management.
/// </summary>
public class AgentManager
{
    private List<IDocumentStore> _stores;
    private List<DevGPTAgent> _agents;
    private List<DevGPTFlow> _flows;
    private string _storesJson;
    private string _agentsJson;
    private string _flowsJson;
    private bool _isContent;
    private readonly string _storesJsonPath;
    private readonly string _agentsJsonPath;
    private readonly string _flowsJsonPath;
    public readonly QuickAgentCreator _quickAgentCreator;

    // The interaction history with agents
    public List<DevGPTChatMessage> History { get; } = new List<DevGPTChatMessage>();

    /// <summary>
    /// All available stores, as loaded and constructed from stores.json
    /// </summary>
    public IReadOnlyList<IDocumentStore> Stores => _stores;
    /// <summary>
    /// All available agents, as loaded and constructed from agents.json
    /// </summary>
    public IReadOnlyList<DevGPTAgent> Agents => _agents;

    public IReadOnlyList<DevGPTFlow> Flows => _flows;

    /// <summary>
    /// Instantiates the AgentManager, loads configuration, and initializes all stores and agents.
    /// </summary>
    public AgentManager(string storesJsonPath, string agentsJsonPath, string flowsJsonPath, string openAIApiKey, string logFilePath, string googleProjectId = "")
    {
        _storesJsonPath = storesJsonPath ?? throw new ArgumentNullException(nameof(storesJsonPath));
        _agentsJsonPath = agentsJsonPath ?? throw new ArgumentNullException(nameof(agentsJsonPath));
        _flowsJsonPath = flowsJsonPath ?? throw new ArgumentNullException(nameof(flowsJsonPath));

        var openAIConfig = new OpenAIConfig(openAIApiKey);
        var llmClient = new OpenAIClientWrapper(openAIConfig);
        var agentFactory = new AgentFactory(openAIApiKey, logFilePath, googleProjectId);
        agentFactory.Messages = History; // History now lives in AgentManager
        _quickAgentCreator = new QuickAgentCreator(agentFactory, llmClient);
    }

    // New: allow injecting a custom LLM client (e.g., ClaudeClientWrapper)
    public AgentManager(string storesJsonPath, string agentsJsonPath, string flowsJsonPath, ILLMClient llmClient, string openAIApiKey, string logFilePath, string googleProjectId = "")
    {
        _storesJsonPath = storesJsonPath ?? throw new ArgumentNullException(nameof(storesJsonPath));
        _agentsJsonPath = agentsJsonPath ?? throw new ArgumentNullException(nameof(agentsJsonPath));
        _flowsJsonPath = flowsJsonPath ?? throw new ArgumentNullException(nameof(flowsJsonPath));

        var agentFactory = new AgentFactory(openAIApiKey, logFilePath, googleProjectId);
        agentFactory.Messages = History;
        _quickAgentCreator = new QuickAgentCreator(agentFactory, llmClient);
    }

    public AgentManager(string storesJson, string agentsJson, string flowsJson, string openAIApiKey, string logFilePath, bool isContent, string googleProjectId = "")
    {
        _storesJson = storesJson;
        _agentsJson = agentsJson;
        _flowsJson = flowsJson;
        _isContent = isContent;

        var openAIConfig = new OpenAIConfig(openAIApiKey);
        var llmClient = new OpenAIClientWrapper(openAIConfig);
        var agentFactory = new AgentFactory(openAIApiKey, logFilePath, googleProjectId);
        agentFactory.Messages = History; // History now lives in AgentManager
        _quickAgentCreator = new QuickAgentCreator(agentFactory, llmClient);
    }

    /// <summary>
    /// Loads and initializes all document stores and agents from provided configuration files.
    /// </summary>
    public async Task LoadStoresAndAgents()
    {
        var loader = new StoresAndAgentsAndFlowLoader(_quickAgentCreator);
        if (!_isContent)
        {
            await loader.LoadFiles(_storesJsonPath, _agentsJsonPath, _flowsJsonPath);
        }
        else
        {
            await loader.LoadFromText(_storesJson, _agentsJson, _flowsJson);
        }
        _agents = loader._agents;
        _stores = loader._stores;
        _flows = loader._flows;
    }

    public DevGPTAgent GetAgent(string name)
    {
        return _agents.FirstOrDefault(a => a.Name == name);
    }
    public DevGPTFlow GetFlow(string name)
    {
        return _flows.FirstOrDefault(a => a.Name == name);
    }

    /// <summary>
    /// Interactively communicates with a specified agent through the console,
    /// using the agent's context and configuration.
    /// </summary>
    public async Task InteractiveUserLoop(string agentName = null)
    {
        DevGPTAgent agent;
        if (string.IsNullOrEmpty(agentName))
        {
            agent = _agents.FirstOrDefault();
            if (agent == null) throw new InvalidOperationException("No agents loaded.");
        }
        else
        {
            agent = GetAgent(agentName);
            if (agent == null) throw new InvalidOperationException($"Agent not found: {agentName}");
        }
        while (true)
        {
            Console.WriteLine("Geef een instructie (of 'exit' om te stoppen):");
            var input = Console.ReadLine();
            if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
                break;
            var cancel = new CancellationToken();
            var response = await agent.Generator.GetResponse<IsReadyResult>(input, cancel, History, true, true, agent.Tools, null);
            while (!response.Result.IsTheUserRequestProperlyHandledAndFinished)
            {
                response = await agent.Generator.GetResponse<IsReadyResult>("Continue handling the user request: " + input, cancel, History, true, true, agent.Tools, null);
                Console.WriteLine(response.Result.Message);
            }
            History.Add(new DevGPTChatMessage { Role = DevGPTMessageRole.Assistant, Text = response.Result.Message });
        }
    }

    public async Task<string> SendMessage(string input, CancellationToken cancel, string agentName = null)
    {
        DevGPTAgent agent;
        if (string.IsNullOrEmpty(agentName))
        {
            agent = _agents.FirstOrDefault();
            if (agent == null) throw new InvalidOperationException("No agents loaded.");
        }
        else
        {
            agent = GetAgent(agentName);
            if (agent == null) throw new InvalidOperationException($"Agent not found: {agentName}");
        }

        await AddHistory(input);

        var response = await agent.Generator.GetResponse<IsReadyResult>(input, cancel, History, true, true, agent.Tools, null);

        await AddHistory(response.Result.Message);

        return response.Result.Message;
    }

    public async Task<string> SendMessage_Flow(string input, CancellationToken cancel, string flowName = null)
    {
        await AddHistory(input);

        var response = await _quickAgentCreator.AgentFactory.CallFlow(flowName, input, "REGULAR", cancel);
        return response;
    }

    private async Task AddHistory(string input)
    {
        var historyStore = Stores.FirstOrDefault(s => s.Name.ToLower() == "history");
        if (historyStore != null)
        {
            var key = $"{DateTime.Now.ToString("yy_MM_dd_HH_mm")}_input";
            await historyStore.Store(key, input);
        }
        History.Add(new DevGPTChatMessage { Role = DevGPTMessageRole.Assistant, Text = input });
    }
}
