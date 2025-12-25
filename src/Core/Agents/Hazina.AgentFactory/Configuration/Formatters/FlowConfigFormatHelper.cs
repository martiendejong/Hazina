using System.Text.Json;

public static class FlowConfigFormatHelper
{
    /// <summary>
    /// Auto-detects the agent config format (json or .Hazina) and parses agent definitions.
    /// If JSON is detected, parses using System.Text.Json. Otherwise, attempts .Hazina parse logic.
    /// </summary>
    public static List<FlowConfig> AutoDetectAndParse(string content)
    {
        if (IsLikelyJson(content))
        {
            try
            {
                return JsonSerializer.Deserialize<List<FlowConfig>>(content);
            }
            catch
            {
                // Fall through to .Hazina attempt
            }
        }
        // Fallback: Try .Hazina format
        try
        {
            return HazinaFlowConfigParser.Parse(content);
        }
        catch (Exception ex)
        {
            throw new Exception("Could not auto-detect the agent config format (JSON/.Hazina). Parse error: " + ex.Message);
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
