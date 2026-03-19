namespace Hazina.AgentFactory.Configuration.Validation;

/// <summary>
/// Validates AgentConfig objects and surfaces detailed diagnostics.
/// </summary>
public class AgentConfigValidator
{
    private readonly ConfigurationResult<AgentConfig> _result;
    private readonly AgentConfig _config;
    private readonly List<string>? _availableStores;
    private readonly List<string>? _availableFunctions;

    public AgentConfigValidator(
        AgentConfig config,
        List<string>? availableStores = null,
        List<string>? availableFunctions = null)
    {
        _config = config;
        _result = new ConfigurationResult<AgentConfig> { Value = config };
        _availableStores = availableStores;
        _availableFunctions = availableFunctions;
    }

    /// <summary>
    /// Validates the agent configuration and returns detailed diagnostics.
    /// </summary>
    public ConfigurationResult<AgentConfig> Validate()
    {
        ValidateRequiredFields();
        ValidateStores();
        ValidateFunctions();
        ValidateCallsAgents();
        ValidateCallsFlows();
        ValidatePrompt();

        return _result;
    }

    private void ValidateRequiredFields()
    {
        if (string.IsNullOrWhiteSpace(_config.Name))
        {
            _result.AddError("Name", "Agent name is required", "AGENT001");
        }

        if (string.IsNullOrWhiteSpace(_config.Prompt))
        {
            _result.AddError("Prompt", "Agent prompt is required", "AGENT002");
        }
    }

    private void ValidateStores()
    {
        if (_config.Stores == null || !_config.Stores.Any())
        {
            _result.AddInfo("Stores", "No stores configured for this agent", "AGENT003");
            return;
        }

        // Check for duplicate store references
        var duplicates = _config.Stores
            .GroupBy(s => s.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var dup in duplicates)
        {
            _result.AddError("Stores", $"Duplicate store reference: {dup}", "AGENT004");
        }

        // Validate against available stores if provided
        if (_availableStores != null)
        {
            foreach (var storeRef in _config.Stores)
            {
                if (!_availableStores.Contains(storeRef.Name))
                {
                    _result.AddError("Stores", $"Store '{storeRef.Name}' not found in available stores", "AGENT005");
                }

                // Warn about write access
                if (storeRef.Write)
                {
                    _result.AddWarning("Stores", $"Write access granted to store '{storeRef.Name}', ensure this is intentional", "AGENT006");
                }
            }
        }
    }

    private void ValidateFunctions()
    {
        if (_config.Functions == null || !_config.Functions.Any())
        {
            _result.AddInfo("Functions", "No functions configured for this agent", "AGENT007");
            return;
        }

        // Check for duplicates
        var duplicates = _config.Functions
            .GroupBy(f => f)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var dup in duplicates)
        {
            _result.AddWarning("Functions", $"Duplicate function reference: {dup}", "AGENT008");
        }

        // Validate against available functions if provided
        if (_availableFunctions != null)
        {
            foreach (var func in _config.Functions)
            {
                if (!_availableFunctions.Contains(func))
                {
                    _result.AddWarning("Functions", $"Function '{func}' may not be available", "AGENT009");
                }
            }
        }
    }

    private void ValidateCallsAgents()
    {
        if (_config.CallsAgents == null || !_config.CallsAgents.Any())
            return;

        // Check for self-reference
        if (_config.CallsAgents.Contains(_config.Name))
        {
            _result.AddError("CallsAgents", "Agent cannot call itself (circular reference)", "AGENT010");
        }

        // Check for duplicates
        var duplicates = _config.CallsAgents
            .GroupBy(a => a)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var dup in duplicates)
        {
            _result.AddWarning("CallsAgents", $"Duplicate agent reference: {dup}", "AGENT011");
        }
    }

    private void ValidateCallsFlows()
    {
        if (_config.CallsFlows == null || !_config.CallsFlows.Any())
            return;

        // Check for duplicates
        var duplicates = _config.CallsFlows
            .GroupBy(f => f)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var dup in duplicates)
        {
            _result.AddWarning("CallsFlows", $"Duplicate flow reference: {dup}", "AGENT012");
        }
    }

    private void ValidatePrompt()
    {
        if (string.IsNullOrWhiteSpace(_config.Prompt))
            return;

        // Check prompt length
        if (_config.Prompt.Length < 20)
        {
            _result.AddWarning("Prompt", "Prompt is very short, consider providing more context", "AGENT013");
        }
        else if (_config.Prompt.Length > 10000)
        {
            _result.AddWarning("Prompt", "Prompt is very long (>10k chars), may impact performance", "AGENT014");
        }

        // Check for common placeholders
        var placeholders = new[] { "TODO", "TBD", "FIXME", "[REPLACE]", "PLACEHOLDER" };
        foreach (var placeholder in placeholders)
        {
            if (_config.Prompt.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                _result.AddWarning("Prompt", $"Prompt contains placeholder: {placeholder}", "AGENT015");
            }
        }
    }

    /// <summary>
    /// Validates all agent configurations.
    /// </summary>
    public static ConfigurationResult<List<AgentConfig>> ValidateAll(
        List<AgentConfig> agents,
        List<string>? availableStores = null,
        List<string>? availableFunctions = null)
    {
        var result = new ConfigurationResult<List<AgentConfig>> { Value = agents };

        if (agents == null || !agents.Any())
        {
            result.AddWarning("Agents", "No agents configured", "AGENT016");
            return result;
        }

        // Check for duplicate names
        var duplicateNames = agents
            .GroupBy(a => a.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var dupName in duplicateNames)
        {
            result.AddError("Agents", $"Duplicate agent name: {dupName}", "AGENT017");
        }

        // Validate each agent individually
        for (int i = 0; i < agents.Count; i++)
        {
            var agentResult = new AgentConfigValidator(agents[i], availableStores, availableFunctions).Validate();

            // Copy diagnostics with agent index prefix
            foreach (var error in agentResult.Errors)
            {
                error.Field = $"Agents[{i}].{error.Field}";
                result.Errors.Add(error);
            }

            foreach (var warning in agentResult.Warnings)
            {
                warning.Field = $"Agents[{i}].{warning.Field}";
                result.Warnings.Add(warning);
            }

            foreach (var info in agentResult.Infos)
            {
                info.Field = $"Agents[{i}].{info.Field}";
                result.Infos.Add(info);
            }
        }

        // Check for circular dependencies in CallsAgents
        DetectCircularDependencies(agents, result);

        return result;
    }

    private static void DetectCircularDependencies(List<AgentConfig> agents, ConfigurationResult<List<AgentConfig>> result)
    {
        var agentGraph = agents.ToDictionary(a => a.Name, a => a.CallsAgents ?? new List<string>());

        foreach (var agent in agents)
        {
            var visited = new HashSet<string>();
            var path = new List<string>();

            if (HasCircularDependency(agent.Name, agentGraph, visited, path))
            {
                result.AddError("Agents", $"Circular dependency detected: {string.Join(" -> ", path)}", "AGENT018");
            }
        }
    }

    private static bool HasCircularDependency(
        string currentAgent,
        Dictionary<string, List<string>> graph,
        HashSet<string> visited,
        List<string> path)
    {
        if (path.Contains(currentAgent))
            return true;

        if (visited.Contains(currentAgent))
            return false;

        visited.Add(currentAgent);
        path.Add(currentAgent);

        if (graph.TryGetValue(currentAgent, out var calls))
        {
            foreach (var calledAgent in calls)
            {
                if (graph.ContainsKey(calledAgent))
                {
                    if (HasCircularDependency(calledAgent, graph, visited, path))
                        return true;
                }
            }
        }

        path.Remove(currentAgent);
        return false;
    }
}
