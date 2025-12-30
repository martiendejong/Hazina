namespace Hazina.Agents.Coding;

/// <summary>
/// Registry of allowed tools with strict validation.
/// Hard-rejects any unknown tool name.
/// </summary>
public class ToolRegistry
{
    private static readonly HashSet<string> AllowedTools = new()
    {
        "read_file",
        "apply_diff",
        "run",
        "git_status",
        "git_diff"
    };

    /// <summary>
    /// Validate that a tool name is allowed
    /// </summary>
    public static bool IsToolAllowed(string toolName)
    {
        return AllowedTools.Contains(toolName);
    }

    /// <summary>
    /// Validate all actions in a plan
    /// </summary>
    public static (bool Valid, string? Error) ValidateActions(List<ToolAction> actions)
    {
        foreach (var action in actions)
        {
            if (!IsToolAllowed(action.Tool))
            {
                return (false, $"Unknown tool: {action.Tool}. Allowed tools: {string.Join(", ", AllowedTools)}");
            }

            var validationError = ValidateToolParameters(action);
            if (validationError != null)
            {
                return (false, validationError);
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Validate tool-specific parameters
    /// </summary>
    private static string? ValidateToolParameters(ToolAction action)
    {
        switch (action.Tool)
        {
            case "read_file":
                if (string.IsNullOrWhiteSpace(action.Path))
                {
                    return "read_file requires 'path' parameter";
                }
                break;

            case "apply_diff":
                if (string.IsNullOrWhiteSpace(action.Path))
                {
                    return "apply_diff requires 'path' parameter";
                }
                if (string.IsNullOrWhiteSpace(action.Diff))
                {
                    return "apply_diff requires 'diff' parameter";
                }
                break;

            case "run":
                if (string.IsNullOrWhiteSpace(action.Command))
                {
                    return "run requires 'command' parameter";
                }
                if (IsDestructiveCommand(action.Command))
                {
                    return $"Destructive command not allowed: {action.Command}";
                }
                break;

            case "git_status":
            case "git_diff":
                break;

            default:
                return $"Unknown tool: {action.Tool}";
        }

        return null;
    }

    /// <summary>
    /// Check if a command is destructive (safety constraint)
    /// </summary>
    private static bool IsDestructiveCommand(string command)
    {
        var lowerCommand = command.ToLowerInvariant();

        var dangerousPatterns = new[]
        {
            "rm -rf",
            "del /s",
            "del /q",
            "remove-item -recurse",
            "format ",
            "diskpart",
            "rd /s",
            "rmdir /s",
            "$env:",
            "set-executionpolicy",
            "invoke-webrequest",
            "invoke-restmethod",
            "wget",
            "curl",
            "net user",
            "net localgroup"
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (lowerCommand.Contains(pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get list of allowed tools
    /// </summary>
    public static IReadOnlySet<string> GetAllowedTools()
    {
        return AllowedTools;
    }
}
