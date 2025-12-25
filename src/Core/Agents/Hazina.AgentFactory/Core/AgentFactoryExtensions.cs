using Hazina.LLMs;

/// <summary>
/// Extension methods for AgentFactory to support Semantic Kernel
/// </summary>
public static class AgentFactoryExtensions
{
    /// <summary>
    /// Create an agent using Semantic Kernel client
    /// </summary>
    public static async Task<HazinaAgent> CreateAgentWithSemanticKernel(
        this AgentFactory factory,
        string name,
        string systemPrompt,
        IEnumerable<(IDocumentStore Store, bool Write)> stores,
        IEnumerable<string> functions,
        IEnumerable<string> agents,
        IEnumerable<string> flows,
        SemanticKernelConfig config,
        bool isCoder = false)
    {
        var llmClient = new SemanticKernelClientWrapper(config);
        return await factory.CreateAgentWithCustomClient(
            name,
            systemPrompt,
            stores,
            functions,
            agents,
            flows,
            llmClient,
            isCoder);
    }

    /// <summary>
    /// Create an unregistered agent using Semantic Kernel client
    /// </summary>
    public static async Task<HazinaAgent> CreateUnregisteredAgentWithSemanticKernel(
        this AgentFactory factory,
        string name,
        string systemPrompt,
        IEnumerable<(IDocumentStore Store, bool Write)> stores,
        IEnumerable<string> functions,
        IEnumerable<string> agents,
        IEnumerable<string> flows,
        SemanticKernelConfig config,
        bool isCoder = false)
    {
        var llmClient = new SemanticKernelClientWrapper(config);
        return await factory.CreateUnregisteredAgentWithCustomClient(
            name,
            systemPrompt,
            stores,
            functions,
            agents,
            flows,
            llmClient,
            isCoder);
    }

    /// <summary>
    /// Create an agent with a custom ILLMClient implementation
    /// </summary>
    public static async Task<HazinaAgent> CreateAgentWithCustomClient(
        this AgentFactory factory,
        string name,
        string systemPrompt,
        IEnumerable<(IDocumentStore Store, bool Write)> stores,
        IEnumerable<string> functions,
        IEnumerable<string> agents,
        IEnumerable<string> flows,
        ILLMClient llmClient,
        bool isCoder = false)
    {
        var agent = await factory.CreateUnregisteredAgentWithCustomClient(
            name,
            systemPrompt,
            stores,
            functions,
            agents,
            flows,
            llmClient,
            isCoder);

        factory.Agents[name] = agent;
        return agent;
    }

    /// <summary>
    /// Create an unregistered agent with a custom ILLMClient implementation
    /// </summary>
    public static async Task<HazinaAgent> CreateUnregisteredAgentWithCustomClient(
        this AgentFactory factory,
        string name,
        string systemPrompt,
        IEnumerable<(IDocumentStore Store, bool Write)> stores,
        IEnumerable<string> functions,
        IEnumerable<string> agents,
        IEnumerable<string> flows,
        ILLMClient llmClient,
        bool isCoder = false)
    {
        var tools = new ToolsContext();
        tools.SendMessage = (string id, string agent, string output) =>
        {
            // Intentionally left as no-op; UI may override.
        };

        // Use reflection to call AddStoreTools (it's private)
        var addStoreToolsMethod = typeof(AgentFactory).GetMethod(
            "AddStoreTools",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (addStoreToolsMethod != null)
        {
            addStoreToolsMethod.Invoke(factory, new object[] { stores, tools, functions, agents, flows, name });
        }

        var tempStores = stores.Skip(1).Select(s => s.Store as IDocumentStore).ToList();
        var generator = new DocumentGenerator(
            stores.First().Store,
            new List<HazinaChatMessage>()
            {
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.System,
                    Text = systemPrompt
                }
            },
            llmClient,
            tempStores);

        var agent = new HazinaAgent(name, generator, tools, isCoder);
        return agent;
    }
}
