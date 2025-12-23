using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

/// <summary>
/// Parser and serializer for AgentConfig in .devgpt plain text format.
/// </summary>
public static class DevGPTAgentConfigParser
{
    /// <summary>
    /// Serialize a list of AgentConfig into .devgpt format.
    /// </summary>
    public static string Serialize(IEnumerable<AgentConfig> agents)
    {
        var sb = new StringBuilder();
        foreach (var agent in agents)
        {
            sb.AppendLine($"Name: {agent.Name}");
            sb.AppendLine($"Description: {agent.Description}");
            sb.AppendLine($"Prompt: {EscapeMultiline(agent.Prompt)}");
            sb.AppendLine($"Stores: {string.Join(",", agent.Stores?.Select(ToStoreRefString) ?? Array.Empty<string>())}");
            sb.AppendLine($"Functions: {string.Join(",", agent.Functions ?? new List<string>())}");
            sb.AppendLine($"CallsAgents: {string.Join(",", agent.CallsAgents ?? new List<string>())}");
            sb.AppendLine($"CallsFlows: {string.Join(",", agent.CallsFlows ?? new List<string>())}");
            sb.AppendLine($"ExplicitModify: {agent.ExplicitModify}");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Parse a .devgpt agent config string into a list of AgentConfig objects.
    /// </summary>
    public static List<AgentConfig> Parse(string input)
    {
        var agents = new List<AgentConfig>();
        AgentConfig current = null;
        foreach (var line in input.Split(["\r\n", "\n"], StringSplitOptions.TrimEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                if (current != null)
                {
                    agents.Add(current);
                    current = null;
                }
                continue;
            }
            if (current == null) current = new AgentConfig();
            var sepIdx = trimmed.IndexOf(':');
            if (sepIdx < 0) continue;
            var key = trimmed.Substring(0, sepIdx).Trim();
            var value = trimmed.Substring(sepIdx + 1).Trim();
            switch (key)
            {
                case "Name": current.Name = value; break;
                case "Description": current.Description = value; break;
                case "Prompt": current.Prompt = UnescapeMultiline(value); break;
                case "Stores":
                    // Parse to List<StoreRef>
                    current.Stores = string.IsNullOrWhiteSpace(value)
                        ? new List<StoreRef>()
                        : value.Split(',').Select(ParseStoreRef).Where(r => r != null).ToList();
                    break;
                case "Functions":
                    current.Functions = string.IsNullOrWhiteSpace(value) ? new List<string>() : value.Split(',').Select(x=> x.Trim()).Where(x => x != "").ToList();
                    break;
                case "CallsAgents":
                    current.CallsAgents = string.IsNullOrWhiteSpace(value) ? new List<string>() : value.Split(',').Select(x=> x.Trim()).Where(x => x != "").ToList();
                    break;
                case "CallsFlows":
                    current.CallsFlows = string.IsNullOrWhiteSpace(value) ? new List<string>() : value.Split(',').Select(x => x.Trim()).Where(x => x != "").ToList();
                    break;
                case "ExplicitModify":
                    bool explicitModify;
                    current.ExplicitModify = bool.TryParse(value, out explicitModify) && explicitModify;
                    break;
            }
        }
        if (current != null && !agents.Contains(current)) agents.Add(current);
        return agents;
    }

    // Helper for escaping/unescaping multiline values (Prompt)
    private static string EscapeMultiline(string value)
    {
        return value?.Replace("\n", "\\n").Replace("\r", "") ?? string.Empty;
    }
    private static string UnescapeMultiline(string value) => value?.Replace("\\n", "\n") ?? string.Empty;

    // Serializes StoreRef to flat format (Name|Write)
    private static string ToStoreRefString(StoreRef sr) => sr == null ? string.Empty : $"{sr.Name}|{sr.Write}";
    // Parses StoreRef from a string. Format: Name|Write, fallback: just Name (Write=false)
    private static StoreRef ParseStoreRef(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry)) return null;
        var parts = entry.Split('|');
        if (parts.Length == 2)
            return new StoreRef { Name = parts[0].Trim(), Write = bool.TryParse(parts[1], out var w) && w };
        return new StoreRef { Name = entry.Trim(), Write = false };
    }

    // Robust load from file (throws with diagnostics)
    public static List<AgentConfig> LoadFromFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Agent config file not found: {path}");
        string text = File.ReadAllText(path);
        try
        {
            return Parse(text);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse agent config file '{path}': {ex.Message}", ex);
        }
    }
    // Save list to .devgpt format file
    public static void SaveToFile(IEnumerable<AgentConfig> agents, string path)
    {
        string text = Serialize(agents);
        try
        {
            File.WriteAllText(path, text);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to write .devgpt agent config to file '{path}': {ex.Message}", ex);
        }
    }
    // Interop: JSON string to .devgpt file (for migrations/conversion)
    public static void ConvertJsonToDevGptFile(string json, string path)
    {
        try
        {
            var agents = JsonSerializer.Deserialize<List<AgentConfig>>(json);
            SaveToFile(agents, path);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed conversion from JSON to .devgpt: {ex.Message}", ex);
        }
    }
}


/// <summary>
/// Parser and serializer for AgentConfig in .devgpt plain text format.
/// </summary>
public static class DevGPTFlowConfigParser
{
    /// <summary>
    /// Serialize a list of AgentConfig into .devgpt format.
    /// </summary>
    public static string Serialize(IEnumerable<FlowConfig> agents)
    {
        var sb = new StringBuilder();
        foreach (var agent in agents)
        {
            sb.AppendLine($"Name: {agent.Name}");
            sb.AppendLine($"Description: {agent.Description}");
            sb.AppendLine($"CallsAgents: {string.Join(",", agent.CallsAgents ?? new List<string>())}");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Parse a .devgpt agent config string into a list of AgentConfig objects.
    /// </summary>
    public static List<FlowConfig> Parse(string input)
    {
        var agents = new List<FlowConfig>();
        FlowConfig current = null;
        foreach (var line in input.Split(["\r\n", "\n"], StringSplitOptions.TrimEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                if (current != null)
                {
                    agents.Add(current);
                    current = null;
                }
                continue;
            }
            if (current == null) current = new FlowConfig();
            var sepIdx = trimmed.IndexOf(':');
            if (sepIdx < 0) continue;
            var key = trimmed.Substring(0, sepIdx).Trim();
            var value = trimmed.Substring(sepIdx + 1).Trim();
            switch (key)
            {
                case "Name": current.Name = value; break;
                case "Description": current.Description = value; break;
                case "CallsAgents":
                    current.CallsAgents = string.IsNullOrWhiteSpace(value) ? new List<string>() : value.Split(',').Select(x => x.Trim()).Where(x => x != "").ToList();
                    break;
            }
        }
        if (current != null && !agents.Contains(current)) agents.Add(current);
        return agents;
    }

    // Helper for escaping/unescaping multiline values (Prompt)
    private static string EscapeMultiline(string value)
    {
        return value?.Replace("\n", "\\n").Replace("\r", "") ?? string.Empty;
    }
    private static string UnescapeMultiline(string value) => value?.Replace("\\n", "\n") ?? string.Empty;

    // Serializes StoreRef to flat format (Name|Write)
    private static string ToStoreRefString(StoreRef sr) => sr == null ? string.Empty : $"{sr.Name}|{sr.Write}";
    // Parses StoreRef from a string. Format: Name|Write, fallback: just Name (Write=false)
    private static StoreRef ParseStoreRef(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry)) return null;
        var parts = entry.Split('|');
        if (parts.Length == 2)
            return new StoreRef { Name = parts[0].Trim(), Write = bool.TryParse(parts[1], out var w) && w };
        return new StoreRef { Name = entry.Trim(), Write = false };
    }

    // Robust load from file (throws with diagnostics)
    public static List<FlowConfig> LoadFromFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Agent config file not found: {path}");
        string text = File.ReadAllText(path);
        try
        {
            return Parse(text);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse agent config file '{path}': {ex.Message}", ex);
        }
    }
    // Save list to .devgpt format file
    public static void SaveToFile(IEnumerable<FlowConfig> flows, string path)
    {
        string text = Serialize(flows);
        try
        {
            File.WriteAllText(path, text);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to write .devgpt agent config to file '{path}': {ex.Message}", ex);
        }
    }
    // Interop: JSON string to .devgpt file (for migrations/conversion)
    public static void ConvertJsonToDevGptFile(string json, string path)
    {
        try
        {
            var flows = JsonSerializer.Deserialize<List<FlowConfig>>(json);
            SaveToFile(flows, path);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed conversion from JSON to .devgpt: {ex.Message}", ex);
        }
    }
}
