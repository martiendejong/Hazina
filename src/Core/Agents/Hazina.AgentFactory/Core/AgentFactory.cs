using System.Text.Json;

using Hazina.AgentFactory.Services.BigQuery;
using Hazina.AgentFactory.Services.Email;
using Hazina.AgentFactory.Services.Execution;
using Hazina.AgentFactory.Services.Tools;
using Hazina.LLMs;
using Hazina.LLMs.OpenAI;

namespace Hazina.AgentFactory.Core;

/// <summary>
/// Factory for creating and managing Hazina agents and flows.
/// Refactored from 1,088 lines to follow Single Responsibility Principle.
///
/// Responsibilities (now focused):
/// - Agent creation and registration
/// - Flow creation and registration
/// - Configuration management
///
/// Delegated to services:
/// - Email operations → EmailService
/// - BigQuery operations → BigQueryService
/// - Tool registration → ToolRegistrationService
/// - Agent/Flow execution → AgentExecutionService
/// </summary>
public class AgentFactory
{
    #region Configuration

    // Use lowercase fields for backwards compatibility with existing code
    public List<StoreConfig> storesConfig = new();
    public List<AgentConfig> agentsConfig = new();
    public List<FlowConfig> flowsConfig = new();

    public string OpenAiApiKey;
    public string GoogleProjectId;
    public string LogFilePath;

    public bool WriteMode = false;
    public List<HazinaChatMessage> Messages = new();

    public Dictionary<string, HazinaAgent> Agents = new();
    public Dictionary<string, HazinaFlow> Flows = new();

    #endregion

    #region Services

    public IEmailService EmailService { get; }
    public IBigQueryService? BigQueryService { get; }
    public IToolRegistrationService ToolRegistrationService { get; private set; } = null!;
    public IAgentExecutionService? ExecutionService { get; private set; }

    #endregion

    #region Google Credentials

    private readonly string? _googleAccountJson;
    private readonly string? _googleAccountProjectId;

    #endregion

    public AgentFactory(string openAIApiKey, string logFilePath, string googleProjectId = "")
    {
        OpenAiApiKey = openAIApiKey;
        GoogleProjectId = googleProjectId;
        LogFilePath = logFilePath;

        // Initialize Google credentials
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var credentialsPath = Path.Combine(basePath, "googleaccount.json");

        if (File.Exists(credentialsPath))
        {
            try
            {
                _googleAccountJson = File.ReadAllText(credentialsPath);
                using var doc = JsonDocument.Parse(_googleAccountJson);
                _googleAccountProjectId = doc.RootElement.GetProperty("project_id").GetString();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not load google account file");
                Console.WriteLine(ex.Message);
            }
        }

        // Initialize services
        EmailService = new EmailService(new EmailSettings());

        if (!string.IsNullOrEmpty(_googleAccountProjectId) && !string.IsNullOrEmpty(_googleAccountJson))
        {
            BigQueryService = new BigQueryService(_googleAccountProjectId, _googleAccountJson);
        }

        InitializeToolRegistrationService();
    }

    private void InitializeToolRegistrationService()
    {
        ToolRegistrationService = new ToolRegistrationService(
            storesConfig,
            agentsConfig,
            flowsConfig,
            EmailService,
            BigQueryService,
            CallAgent,
            CallFlow,
            (name, query, caller, cancel) => CallCoderAgent(name, query, caller, cancel),
            CallCustomAgent,
            () => WriteMode);
    }

    public void InitializeExecutionService()
    {
        ExecutionService = new AgentExecutionService(
            Agents,
            Flows,
            Messages,
            () => WriteMode,
            value => WriteMode = value);
    }

    #region Agent Execution (delegates to ExecutionService or direct implementation for backwards compatibility)

    public async Task<string> CallAgent(string name, string query, string caller, CancellationToken cancel)
    {
        if (ExecutionService != null)
            return await ExecutionService.CallAgentAsync(name, query, caller, cancel);

        // Fallback for backwards compatibility during transition
        return await CallAgentDirect(name, query, caller, cancel);
    }

    public async Task<string> CallFlow(string name, string query, string caller, CancellationToken cancel)
    {
        if (ExecutionService != null)
            return await ExecutionService.CallFlowAsync(name, query, caller, cancel);

        return await CallFlowDirect(name, query, caller, cancel);
    }

    public async Task<string> CallCoderAgent(string name, string query, string caller, CancellationToken cancel, string flowName = "")
    {
        if (ExecutionService != null)
            return await ExecutionService.CallCoderAgentAsync(name, query, caller, cancel, flowName);

        return await CallCoderAgentDirect(name, query, caller, cancel, flowName);
    }

    public async Task<string> CallAgentWithMeta(string name, string query, string caller, string functionName, string flowName, CancellationToken cancel)
    {
        if (ExecutionService != null)
            return await ExecutionService.CallAgentWithMetaAsync(name, query, caller, functionName, flowName, cancel);

        return await CallAgentWithMetaDirect(name, query, caller, functionName, flowName, cancel);
    }

    #endregion

    #region Agent/Flow Creation

    public async Task<HazinaAgent> CreateUnregisteredAgent(
        string name,
        string systemPrompt,
        IEnumerable<(IDocumentStore Store, bool Write)> stores,
        IEnumerable<string> functions,
        IEnumerable<string> agents,
        IEnumerable<string> flows,
        bool isCoder = false,
        string model = "")
    {
        var config = new OpenAIConfig(OpenAiApiKey);
        if (!string.IsNullOrWhiteSpace(model))
            config.Model = model;

        var llmClient = new OpenAIClientWrapper(config);
        var tools = new ToolsContext();
        tools.SendMessage = (string id, string agent, string output) => { };

        ToolRegistrationService.AddStoreTools(stores, tools, functions, agents, flows, name);

        var tempStores = stores.Skip(1).Select(s => s.Store).ToList();
        var generator = new DocumentGenerator(
            stores.First().Store,
            new List<HazinaChatMessage> { new() { Role = HazinaMessageRole.System, Text = systemPrompt } },
            llmClient,
            OpenAiApiKey,
            LogFilePath,
            tempStores);

        return new HazinaAgent(name, generator, tools, isCoder);
    }

    public async Task<HazinaAgent> CreateAgent(
        string name,
        string systemPrompt,
        IEnumerable<(IDocumentStore Store, bool Write)> stores,
        IEnumerable<string> functions,
        IEnumerable<string> agents,
        IEnumerable<string> flows,
        bool isCoder = false)
    {
        var agent = await CreateUnregisteredAgent(name, systemPrompt, stores, functions, agents, flows, isCoder);
        Agents[name] = agent;
        return agent;
    }

    public HazinaFlow CreateFlow(string name, List<string> callsAgents)
    {
        var flow = new HazinaFlow(name, callsAgents);
        Flows[name] = flow;
        return flow;
    }

    #endregion

    #region Custom Agent (for backwards compatibility)

    private async Task<string> CallCustomAgent(string systemPrompt, string store, string instruction, bool isCoder, CancellationToken cancel)
    {
        var storeConfig = storesConfig.FirstOrDefault(s => s.Name.Equals(store, StringComparison.OrdinalIgnoreCase));
        if (storeConfig == null) return "Store not found";

        var openAIConfig = new OpenAIConfig(OpenAiApiKey);
        var llmClient = new OpenAIClientWrapper(openAIConfig);
        var creator = new QuickAgentCreator(this, llmClient);
        var loader = new StoresAndAgentsAndFlowLoader(creator);

        var dstore = creator.CreateStore(new StorePaths(storeConfig.Path), storeConfig.Name) as IDocumentStore;
        await loader.AddFiles(dstore, storeConfig.Path, storeConfig.FileFilters, storeConfig.SubDirectory, storeConfig.ExcludePattern);

        var agent = await CreateUnregisteredAgent("unregistered", systemPrompt, [(dstore, true)], ["custom"], [], [], isCoder);

        instruction = "de functies custom_agent_getstores, custom_agent_run en custom_agent_write kunnen gebruikt worden om de stores in te zien, custom agents aan te roepen voor een response, of om wijzigingen te maken. " + instruction;

        const string writeModeText = "\nYou are now in write mode. You cannot call any other {agent}_write tools or write file tools in this mode. The file modifications need to be included in your response.";
        const string coderModeText = "\nALL YOUR MODIFICATIONS MUST ALWAYS SUPPLY THE WHOLE FILE. NEVER leave anything out and NEVER replace it with something like /* the rest of the code goes here */ or /* the rest of the code stays the same */";

        string response;
        if (isCoder)
        {
            WriteMode = true;
            try
            {
                var responseObj = await agent.Generator.UpdateStore(instruction + writeModeText + coderModeText, cancel, null, true, true, agent.Tools, null);
                response = responseObj.Result;
            }
            finally
            {
                WriteMode = false;
            }
        }
        else
        {
            var responseObj = await agent.Generator.GetResponse(instruction, cancel, Messages);
            response = responseObj.Result;
        }
        return response;
    }

    #endregion

    #region Direct Implementation (backwards compatibility during transition)

    private const string WriteModeText = "\nYou are now in write mode. You cannot call any other {agent}_write tools or write file tools in this mode. The file modifications need to be included in your response.";
    private const string CoderModeText = "\nALL YOUR MODIFICATIONS MUST ALWAYS SUPPLY THE WHOLE FILE. NEVER leave anything out and NEVER replace it with something like /* the rest of the code goes here */ or /* the rest of the code stays the same */";

    private async Task<string> CallAgentDirect(string name, string query, string caller, CancellationToken cancel)
    {
        var agent = Agents[name];
        var id = Guid.NewGuid().ToString();
        agent.Tools.SendMessage?.Invoke(id, name, query);
        Guid messageId = Guid.NewGuid();
        var message = new HazinaChatMessage
        {
            MessageId = messageId,
            Role = HazinaMessageRole.User,
            Text = $"{caller}: {query}",
            AgentName = name,
            FunctionName = string.Empty,
            FlowName = string.Empty,
            Response = string.Empty
        };
        Messages.Add(message);
        var responseObj = await agent.Generator.GetResponse(query + (WriteMode ? WriteModeText : ""), cancel, null, true, true, agent.Tools, null);
        string response = responseObj.Result;
        var storedMsg = Messages.FirstOrDefault(m => m.MessageId == messageId);
        if (storedMsg != null) storedMsg.Response = response;
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
        Messages.Add(replyMsg);
        agent.Tools.SendMessage?.Invoke(id, name, response);
        return response;
    }

    private async Task<string> CallFlowDirect(string name, string query, string caller, CancellationToken cancel)
    {
        var flow = Flows[name];
        foreach (var agent in flow.CallsAgents)
        {
            if (Agents[agent].IsCoder && !WriteMode)
            {
                WriteMode = true;
                query = await CallCoderAgentDirect(agent, query, caller, cancel, flow.Name);
                WriteMode = false;
            }
            else
            {
                query = await CallAgentWithMetaDirect(agent, query, caller, string.Empty, flow.Name, cancel);
            }
        }
        return query;
    }

    private async Task<string> CallCoderAgentDirect(string name, string query, string caller, CancellationToken cancel, string flowName = "")
    {
        var agent = Agents[name];
        var id = Guid.NewGuid().ToString();
        agent.Tools.SendMessage?.Invoke(id, name, query);
        Guid messageId = Guid.NewGuid();
        var message = new HazinaChatMessage
        {
            MessageId = messageId,
            Role = HazinaMessageRole.User,
            Text = $"{caller}: {query}",
            AgentName = name,
            FunctionName = "CodeModify",
            FlowName = flowName,
            Response = string.Empty
        };
        Messages.Add(message);
        var responseObj = await agent.Generator.UpdateStore(query + WriteModeText + CoderModeText, cancel, null, true, true, agent.Tools, null);
        string response = responseObj.Result;
        var storedMsg = Messages.FirstOrDefault(m => m.MessageId == messageId);
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
        Messages.Add(replyMsg);
        agent.Tools.SendMessage?.Invoke(id, name, response);
        return response;
    }

    private async Task<string> CallAgentWithMetaDirect(string name, string query, string caller, string functionName, string flowName, CancellationToken cancel)
    {
        var agent = Agents[name];
        var id = Guid.NewGuid().ToString();
        agent.Tools.SendMessage?.Invoke(id, name, query);
        Guid messageId = Guid.NewGuid();
        var message = new HazinaChatMessage
        {
            MessageId = messageId,
            Role = HazinaMessageRole.User,
            Text = $"{caller}: {query}",
            AgentName = name,
            FunctionName = functionName ?? string.Empty,
            FlowName = flowName ?? string.Empty,
            Response = string.Empty
        };
        Messages.Add(message);
        var responseObj = await agent.Generator.GetResponse(query + (WriteMode ? WriteModeText : ""), cancel, null, true, true, agent.Tools, null);
        string response = responseObj.Result;
        var storedMsg = Messages.FirstOrDefault(m => m.MessageId == messageId);
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
        Messages.Add(replyMsg);
        agent.Tools.SendMessage?.Invoke(id, name, response);
        return response;
    }

    #endregion
}
