public class QuickAgentCreator
{
    public QuickAgentCreator(AgentFactory f, ILLMClient client)
    {
        AgentFactory = f;
        Client = client;
    }

    public AgentFactory AgentFactory { get; set; }
    public ILLMClient Client { get; set; }

    /// <summary>
    /// Centralized agent creation method.
    /// </summary>
    public async Task<HazinaAgent> Create(
        string name,
        string systemPrompt,
        IEnumerable<(IDocumentStore Store, bool Write)> stores,
        IEnumerable<string> functions = null,
        IEnumerable<string> agents = null,
        IEnumerable<string> flows = null,
        bool isCoder = false)
    {
        if (agents == null) agents = new List<string>();
        if (flows == null) flows = new List<string>();
        if (functions == null) functions = new List<string>();
        return await AgentFactory.CreateAgent(name, systemPrompt, stores, functions, agents, flows, isCoder);
    }

    public HazinaFlow CreateFlow(string name, List<string> agents)
    {
        return AgentFactory.CreateFlow(name, agents);
    }

    /// <summary>
    /// Creates a document store for code and agent memory.
    /// </summary>
    public DocumentStore CreateStore(StorePaths paths, string name)
    {
        var specPath = Path.Combine(paths.RootFolder, "embeddings.spec");
        var embeddingsSpec = File.Exists(specPath) ? File.ReadAllText(specPath).Trim() : paths.EmbeddingsFile;
        var embeddingStore = EmbeddingStoreFactory.CreateFromSpec(embeddingsSpec, Client);
        var textSpecPath = Path.Combine(paths.RootFolder, "textstore.spec");
        var chunksSpecPath = Path.Combine(paths.RootFolder, "chunkstore.spec");
        var textSpec = File.Exists(textSpecPath) ? File.ReadAllText(textSpecPath).Trim() : paths.RootFolder;
        var chunksSpec = File.Exists(chunksSpecPath) ? File.ReadAllText(chunksSpecPath).Trim() : paths.ChunksFile;
        var textStore = StoreFactory.CreateTextStore(textSpec);
        var chunkStore = StoreFactory.CreateChunkStore(chunksSpec);
        var metadataStore = StoreFactory.CreateMetadataStore(textSpec);
        var store = new DocumentStore(embeddingStore, textStore, chunkStore, metadataStore, Client);
        store.Name = name;
        return store;
    }

    public async Task<DocumentStore> CreateStoreAsync(StorePaths paths, string name)
    {
        var store = CreateStore(paths, name);
        await store.UpdateEmbeddings();
        return store;
    }
}
