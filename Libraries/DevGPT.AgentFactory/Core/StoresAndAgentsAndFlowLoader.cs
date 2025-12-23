public class StoresAndAgentsAndFlowLoader
{
    public List<IDocumentStore> _stores = new List<IDocumentStore>();
    public List<DevGPTAgent> _agents = new List<DevGPTAgent>();
    public List<DevGPTFlow> _flows = new List<DevGPTFlow>();

    public readonly QuickAgentCreator _quickAgentCreator;

    public StoresAndAgentsAndFlowLoader(QuickAgentCreator quickAgentCreator)
    {
        _quickAgentCreator = quickAgentCreator;
    }

    public async Task LoadFiles(string _storesJsonPath, string _agentsJsonPath, string _flowsJsonPath)
    {
        string storesJson;
        string agentsJson;
        string flowsJson;
        if (!File.Exists(_storesJsonPath))
            throw new FileNotFoundException("Could not find stores configuration.", _storesJsonPath);
        if (!File.Exists(_agentsJsonPath))
            throw new FileNotFoundException("Could not find agents configuration.", _agentsJsonPath);
        if (!File.Exists(_flowsJsonPath))
            throw new FileNotFoundException("Could not find agents configuration.", _flowsJsonPath);
        storesJson = File.ReadAllText(_storesJsonPath);
        agentsJson = File.ReadAllText(_agentsJsonPath);
        flowsJson = File.ReadAllText(_flowsJsonPath);

        await LoadFromText(storesJson, agentsJson, flowsJson);
    }

    public async Task LoadFromText(string storesJson, string agentsjson, string flowsjson)
    {
        // ---- FORMAT AUTO-DETECTION (support both JSON/.devgpt) ----
        var storesConfig = StoreConfigFormatHelper.AutoDetectAndParse(storesJson) ?? new List<StoreConfig>();
        var agentsConfig = AgentConfigFormatHelper.AutoDetectAndParse(agentsjson) ?? new List<AgentConfig>();
        var flowsConfig = FlowConfigFormatHelper.AutoDetectAndParse(flowsjson) ?? new List<FlowConfig>();
        _quickAgentCreator.AgentFactory.storesConfig = storesConfig;
        _quickAgentCreator.AgentFactory.agentsConfig = agentsConfig;
        _quickAgentCreator.AgentFactory.flowsConfig = flowsConfig;
        // ----------------------------------------------------------

        // FIX: Always clear the current store/agent lists to avoid duplications
        _stores = new List<IDocumentStore>();
        _agents = new List<DevGPTAgent>();
        _flows = new List<DevGPTFlow>();

        // Create all document stores
        foreach (var sc in storesConfig)
        {
            var store = _quickAgentCreator.CreateStore(new StorePaths(sc.Path), sc.Name) as IDocumentStore;
            await AddFiles(store, sc.Path, sc.FileFilters, sc.SubDirectory, sc.ExcludePattern);
            _stores.Add(store);
        }

        // Create all agents and set up their communication/roles
        foreach (var ac in agentsConfig)
        {
            // Improved: Check each agent store reference (robust error)
            var agentStores = ac.Stores.Select(acs =>
            {
                var found = _stores.FirstOrDefault(s => s.Name == acs.Name);
                if (found == null)
                {
                    throw new InvalidOperationException($"Agent '{ac.Name}' requires store '{acs.Name}' but it was not found in store config.");
                }
                return (found, acs.Write);
            }).ToList();

            // Always await agent creation
            var agent = await _quickAgentCreator.Create(
                ac.Name,
                $"Jouw naam: {ac.Name}\nJouw Rol: {ac.Description}\nInstructie: {ac.Prompt}",
                agentStores,
                ac.Functions,
                ac.CallsAgents,
                ac.CallsFlows,
                ac.ExplicitModify
            );
            _agents.Add(agent);
        }

        foreach(var fc in flowsConfig)
        {
            var flow = _quickAgentCreator.CreateFlow(fc.Name, fc.CallsAgents);
            _flows.Add(flow);
        }
    }

    public async Task AddFiles(IDocumentStore store, string path, string[] fileFilters, string subDirectory = "", string[] excludePattern = null)
    {
        var dir = subDirectory == "" ? new DirectoryInfo(path) : new DirectoryInfo(Path.Combine(path, subDirectory));

        var filesParts = new List<FileInfo[]>();
        foreach (var item in fileFilters)
        {
            filesParts.Add(dir.GetFiles(item, SearchOption.AllDirectories));
        }
        var files = filesParts.SelectMany(f => f).ToList();
        files = files.Where(file =>
        {
            // Use Path.GetRelativePath for platform-correct relative file paths
            var relPath = Path.GetRelativePath(path, file.FullName);
            return excludePattern == null || !excludePattern.Any(dir => MatchPattern(relPath, dir));
        }).ToList();

        foreach (var file in files)
        {
            var relPath = Path.GetRelativePath(path, file.FullName);
            if (excludePattern == null || !excludePattern.Any(dir => MatchPattern(relPath, dir)))
            {
                await store.Embed(relPath);
            }
        }
    }

    public bool MatchPattern(string text, string pattern)
    {
        if (pattern.StartsWith("*"))
            return text.StartsWith(pattern, StringComparison.InvariantCultureIgnoreCase);
        return text.Contains(pattern, StringComparison.InvariantCultureIgnoreCase);
    }
}
