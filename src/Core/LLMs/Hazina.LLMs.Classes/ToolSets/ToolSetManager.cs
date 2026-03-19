using System.Collections.Concurrent;
using System.Text.Json;
using Hazina.LLMs.Classes.Providers;

namespace Hazina.LLMs.Classes.ToolSets;

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

    /// <summary>
    /// Register a tool set
    /// </summary>
    public bool RegisterToolSet(ToolSet toolSet)
    {
        if (toolSet == null)
            throw new ArgumentNullException(nameof(toolSet));

        if (string.IsNullOrWhiteSpace(toolSet.Id))
            throw new ArgumentException("ToolSet must have a valid Id", nameof(toolSet));

        return _toolSets.TryAdd(toolSet.Id, toolSet);
    }

    /// <summary>
    /// Unregister a tool set
    /// </summary>
    public bool UnregisterToolSet(string toolSetId)
    {
        return _toolSets.TryRemove(toolSetId, out _);
    }

    /// <summary>
    /// Get a tool set by ID
    /// </summary>
    public ToolSet? GetToolSet(string toolSetId)
    {
        return _toolSets.TryGetValue(toolSetId, out var toolSet) ? toolSet : null;
    }

    /// <summary>
    /// Get all registered tool sets
    /// </summary>
    public IReadOnlyList<ToolSet> GetAllToolSets()
    {
        return _toolSets.Values.ToList();
    }

    /// <summary>
    /// Get tool sets by category
    /// </summary>
    public IReadOnlyList<ToolSet> GetToolSetsByCategory(string category)
    {
        return _toolSets.Values
            .Where(ts => ts.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Get tool sets by tag
    /// </summary>
    public IReadOnlyList<ToolSet> GetToolSetsByTag(string tag)
    {
        return _toolSets.Values
            .Where(ts => ts.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Opt-in a user to a tool set
    /// </summary>
    public bool OptIn(string userId, string toolSetId)
    {
        if (!_toolSets.ContainsKey(toolSetId))
            return false;

        var userOptIns = _userOptIns.GetOrAdd(userId, _ => new HashSet<string>());

        lock (userOptIns)
        {
            return userOptIns.Add(toolSetId);
        }
    }

    /// <summary>
    /// Opt-out a user from a tool set
    /// </summary>
    public bool OptOut(string userId, string toolSetId)
    {
        if (!_userOptIns.TryGetValue(userId, out var userOptIns))
            return false;

        lock (userOptIns)
        {
            return userOptIns.Remove(toolSetId);
        }
    }

    /// <summary>
    /// Check if a user has opted into a tool set
    /// </summary>
    public bool IsOptedIn(string userId, string toolSetId)
    {
        if (!_userOptIns.TryGetValue(userId, out var userOptIns))
        {
            // Check if tool set is enabled by default
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

    /// <summary>
    /// Get all tool sets a user has opted into
    /// </summary>
    public IReadOnlyList<ToolSet> GetUserToolSets(string userId)
    {
        var optedInSets = new List<ToolSet>();

        // Add default-enabled tool sets
        optedInSets.AddRange(_toolSets.Values.Where(ts => ts.EnabledByDefault));

        // Add explicitly opted-in tool sets
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

    /// <summary>
    /// Get all tools available to a user based on their opt-ins
    /// </summary>
    public async Task<IReadOnlyList<HazinaChatTool>> GetUserToolsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (_providerRegistry == null)
            return Array.Empty<HazinaChatTool>();

        var userToolSets = GetUserToolSets(userId);
        var toolNames = new HashSet<string>();

        // Collect all tool names from user's tool sets
        foreach (var toolSet in userToolSets)
        {
            foreach (var toolName in toolSet.ToolNames)
            {
                toolNames.Add(toolName);
            }
        }

        // Get tools from providers
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

    /// <summary>
    /// Export tool set configuration to JSON
    /// </summary>
    public string ExportToJson(string toolSetId)
    {
        if (!_toolSets.TryGetValue(toolSetId, out var toolSet))
            throw new KeyNotFoundException($"ToolSet '{toolSetId}' not found");

        return JsonSerializer.Serialize(toolSet, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Import tool set configuration from JSON
    /// </summary>
    public bool ImportFromJson(string json)
    {
        try
        {
            var toolSet = JsonSerializer.Deserialize<ToolSet>(json);
            if (toolSet != null)
            {
                return RegisterToolSet(toolSet);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get statistics about tool set usage
    /// </summary>
    public ToolSetStats GetStats()
    {
        var stats = new ToolSetStats
        {
            TotalToolSets = _toolSets.Count,
            TotalUsers = _userOptIns.Count,
            ToolSetsByCategory = new Dictionary<string, int>(),
            OptInsByToolSet = new Dictionary<string, int>()
        };

        foreach (var toolSet in _toolSets.Values)
        {
            // Count by category
            if (!stats.ToolSetsByCategory.ContainsKey(toolSet.Category))
            {
                stats.ToolSetsByCategory[toolSet.Category] = 0;
            }
            stats.ToolSetsByCategory[toolSet.Category]++;

            // Count opt-ins
            var optInCount = _userOptIns.Values.Count(userOptIns =>
            {
                lock (userOptIns)
                {
                    return userOptIns.Contains(toolSet.Id);
                }
            });

            stats.OptInsByToolSet[toolSet.Id] = optInCount;
        }

        return stats;
    }

    /// <summary>
    /// Validate tool set dependencies
    /// </summary>
    public ToolSetValidationResult ValidateToolSet(string toolSetId)
    {
        if (!_toolSets.TryGetValue(toolSetId, out var toolSet))
        {
            return ToolSetValidationResult.Failure($"ToolSet '{toolSetId}' not found");
        }

        var result = new ToolSetValidationResult { IsValid = true };

        // Check dependencies
        foreach (var dep in toolSet.Dependencies)
        {
            if (!_toolSets.ContainsKey(dep))
            {
                result.IsValid = false;
                result.Errors.Add($"Missing dependency: '{dep}'");
            }
        }

        // Check for circular dependencies
        if (HasCircularDependency(toolSetId, new HashSet<string>()))
        {
            result.IsValid = false;
            result.Errors.Add($"Circular dependency detected for '{toolSetId}'");
        }

        return result;
    }

    private bool HasCircularDependency(string toolSetId, HashSet<string> visited)
    {
        if (visited.Contains(toolSetId))
            return true;

        visited.Add(toolSetId);

        if (_toolSets.TryGetValue(toolSetId, out var toolSet))
        {
            foreach (var dep in toolSet.Dependencies)
            {
                if (HasCircularDependency(dep, new HashSet<string>(visited)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>
/// Interface for tool set management
/// </summary>
public interface IToolSetManager
{
    bool RegisterToolSet(ToolSet toolSet);
    bool UnregisterToolSet(string toolSetId);
    ToolSet? GetToolSet(string toolSetId);
    IReadOnlyList<ToolSet> GetAllToolSets();
    IReadOnlyList<ToolSet> GetToolSetsByCategory(string category);
    IReadOnlyList<ToolSet> GetToolSetsByTag(string tag);
    bool OptIn(string userId, string toolSetId);
    bool OptOut(string userId, string toolSetId);
    bool IsOptedIn(string userId, string toolSetId);
    IReadOnlyList<ToolSet> GetUserToolSets(string userId);
    Task<IReadOnlyList<HazinaChatTool>> GetUserToolsAsync(string userId, CancellationToken cancellationToken = default);
    string ExportToJson(string toolSetId);
    bool ImportFromJson(string json);
    ToolSetStats GetStats();
    ToolSetValidationResult ValidateToolSet(string toolSetId);
}

/// <summary>
/// Statistics about tool set usage
/// </summary>
public class ToolSetStats
{
    public int TotalToolSets { get; set; }
    public int TotalUsers { get; set; }
    public Dictionary<string, int> ToolSetsByCategory { get; set; } = new();
    public Dictionary<string, int> OptInsByToolSet { get; set; } = new();
}

/// <summary>
/// Result of tool set validation
/// </summary>
public class ToolSetValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ToolSetValidationResult Success() => new() { IsValid = true };

    public static ToolSetValidationResult Failure(string error)
    {
        return new ToolSetValidationResult
        {
            IsValid = false,
            Errors = new List<string> { error }
        };
    }
}
