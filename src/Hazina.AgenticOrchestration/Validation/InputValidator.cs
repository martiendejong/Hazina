using System.Text.RegularExpressions;

namespace Hazina.AgenticOrchestration.Validation;

/// <summary>
/// Security validation helper to prevent injection attacks
/// </summary>
public static class InputValidator
{
    private static readonly Regex AgentNameRegex = new(@"^[a-zA-Z0-9_-]{1,50}$", RegexOptions.Compiled);
    private static readonly Regex TaskIdRegex = new(@"^[a-zA-Z0-9_-]{1,200}$", RegexOptions.Compiled);

    /// <summary>
    /// Validates agent name format to prevent command injection
    /// </summary>
    public static bool IsValidAgentName(string? agentName)
    {
        return !string.IsNullOrWhiteSpace(agentName) && AgentNameRegex.IsMatch(agentName);
    }

    /// <summary>
    /// Validates task ID format
    /// </summary>
    public static bool IsValidTaskId(string? taskId)
    {
        return !string.IsNullOrWhiteSpace(taskId) && TaskIdRegex.IsMatch(taskId);
    }

    /// <summary>
    /// Escapes shell argument for defense in depth (though validation is primary defense)
    /// </summary>
    public static string EscapeShellArgument(string argument)
    {
        // Escape backslashes and quotes
        return argument.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Validates that a path is within allowed root directory (prevents path traversal)
    /// </summary>
    public static bool IsPathSafe(string path, string allowedRoot)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var fullRoot = Path.GetFullPath(allowedRoot);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
