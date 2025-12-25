using System;
using System.Collections.Generic;
using System.Text.Json;

public static class StoreConfigFormatHelper
{
    /// <summary>
    /// Auto-detects the store config format (json or .Hazina) and parses store definitions.
    /// If JSON is detected, parses using System.Text.Json. Otherwise, attempts .Hazina parse logic.
    /// </summary>
    public static List<StoreConfig> AutoDetectAndParse(string content)
    {
        if(IsLikelyJson(content))
        {
            try
            {
                return JsonSerializer.Deserialize<List<StoreConfig>>(content);
            }
            catch
            {
                // Fall through to .Hazina attempt
            }
        }
        // Fallback: Try .Hazina format
        try
        {
            return HazinaStoreConfigParser.Parse(content);
        }
        catch(Exception ex)
        {
            throw new Exception("Could not auto-detect the store config format (JSON/.Hazina). Parse error: " + ex.Message);
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
        // Defensive: check for .Hazina typical prefix
        if (trimmed.StartsWith("Name:"))
            return false;
        return false;
    }
}
