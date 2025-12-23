using System;
using System.Collections.Generic;
using System.Text.Json;

public static class AgentConfigFormatHelper
{
    /// <summary>
    /// Auto-detects the agent config format (json or .devgpt) and parses agent definitions.
    /// If JSON is detected, parses using System.Text.Json. Otherwise, attempts .devgpt parse logic.
    /// </summary>
    public static List<AgentConfig> AutoDetectAndParse(string content)
    {
        if(IsLikelyJson(content))
        {
            try
            {
                return JsonSerializer.Deserialize<List<AgentConfig>>(content);
            }
            catch
            {
                // Fall through to .devgpt attempt
            }
        }
        // Fallback: Try .devgpt format
        try
        {
            return DevGPTAgentConfigParser.Parse(content);
        }
        catch(Exception ex)
        {
            throw new Exception("Could not auto-detect the agent config format (JSON/.devgpt). Parse error: " + ex.Message);
        }
    }

    /// <summary>
    /// Heuristic for whether the config is likely JSON.
    /// Accepts whitespace then expects [, { or ".
    /// </summary>
    public static bool IsLikelyJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        if (trimmed.StartsWith("[") || trimmed.StartsWith("{"))
            return true;
        // Simple check for JSON objects or arrays
        if (trimmed.StartsWith("\"") && trimmed.Contains(":") && trimmed.Contains("{"))
            return true;
        // Defensive: check for .devgpt typical prefix
        if (trimmed.StartsWith("Name:"))
            return false;
        return false;
    }
}
