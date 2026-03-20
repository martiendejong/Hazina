using System.Collections.Concurrent;

/// <summary>
/// Manages tool sets and their opt-in configuration
/// </summary>
public class ToolSetManager : IToolSetManager
{
    private readonly ConcurrentDictionary<string, ToolSet> _toolSets = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _userOptIns = new();
    private readonly IToolProviderRegistry? _providerRegistry;

    public ToolSetManager(IToolProviderRegistry? providerRegistry = null)
    {
        _providerRegistry = providerRegistry;
    }

    public bool RegisterToolSet(ToolSet toolSet)
    {
        if (toolSet == null) throw new ArgumentNullException(nameof(toolSet));
        return _toolSets.TryAdd(toolSet.Id, toolSet);
    }

    public bool UnregisterToolSet(string toolSetId)
    {
        return _toolSets.TryRemove(toolSetId, out _);
    }

    public ToolSet? GetToolSet(string toolSetId)
    {
        return _toolSets.TryGetValue(toolSetId, out var toolSet) ? toolSet : null;
    }

    public IReadOnlyList<ToolSet> GetAllToolSets()
    {
        return _toolSets.Values.ToList();
    }

    public bool OptIn(string userId, string toolSetId)
    {
        if (!_toolSets.ContainsKey(toolSetId)) return false;
        var userOptIns = _userOptIns.GetOrAdd(userId, _ => new HashSet<string>());
        lock (userOptIns)
        {
            return userOptIns.Add(toolSetId);
        }
    }

    public bool OptOut(string userId, string toolSetId)
    {
        if (!_userOptIns.TryGetValue(userId, out var userOptIns)) return false;
        lock (userOptIns)
        {
            return userOptIns.Remove(toolSetId);
        }
    }

    public bool IsOptedIn(string userId, string toolSetId)
    {
        if (!_userOptIns.TryGetValue(userId, out var userOptIns))
        {
            if (_toolSets.TryGetValue(toolSetId, out var toolSet))
            {
                return toolSet.EnabledByDefault;
            }
            return false;
        }

        lock (userOptIns)
        {
            return userOptIns.Contains(toolSetId);
        }
    }

    public IReadOnlyList<ToolSet> GetUserToolSets(string userId)
    {
        var optedInSets = new List<ToolSet>();
        optedInSets.AddRange(_toolSets.Values.Where(ts => ts.EnabledByDefault));

        if (_userOptIns.TryGetValue(userId, out var userOptIns))
        {
            lock (userOptIns)
            {
                foreach (var toolSetId in userOptIns)
                {
                    if (_toolSets.TryGetValue(toolSetId, out var toolSet))
                    {
                        optedInSets.Add(toolSet);
                    }
                }
            }
        }

        return optedInSets.Distinct().ToList();
    }

    public async Task<IReadOnlyList<HazinaChatTool>> GetUserToolsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (_providerRegistry == null) return Array.Empty<HazinaChatTool>();

        var userToolSets = GetUserToolSets(userId);
        var toolNames = new HashSet<string>();

        foreach (var toolSet in userToolSets)
        {
            foreach (var toolName in toolSet.ToolNames)
            {
                toolNames.Add(toolName);
            }
        }

        var tools = new List<HazinaChatTool>();
        var allTools = await _providerRegistry.GetAllToolsAsync(cancellationToken);

        foreach (var tool in allTools)
        {
            if (toolNames.Contains(tool.FunctionName))
            {
                tools.Add(tool);
            }
        }

        return tools;
    }
}

public interface IToolSetManager
{
    bool RegisterToolSet(ToolSet toolSet);
    bool UnregisterToolSet(string toolSetId);
    ToolSet? GetToolSet(string toolSetId);
    IReadOnlyList<ToolSet> GetAllToolSets();
    bool OptIn(string userId, string toolSetId);
    bool OptOut(string userId, string toolSetId);
    bool IsOptedIn(string userId, string toolSetId);
    IReadOnlyList<ToolSet> GetUserToolSets(string userId);
    Task<IReadOnlyList<HazinaChatTool>> GetUserToolsAsync(string userId, CancellationToken cancellationToken = default);
}
