namespace Hazina.App.HazinaCoder.Core.Commands;

/// <summary>
/// Enhanced command parser with support for complex command syntax.
/// Improvement #16: Enhanced command parser
/// </summary>
public class CommandParser
{
    /// <summary>
    /// Parses a command string into structured command object
    /// </summary>
    public ParsedCommand Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ParsedCommand { IsEmpty = true };
        }

        var trimmed = input.Trim();

        // Check if it's a special command (starts with /)
        if (trimmed.StartsWith('/'))
        {
            return ParseSpecialCommand(trimmed);
        }

        // Regular prompt
        return new ParsedCommand
        {
            Type = CommandType.Prompt,
            RawInput = input,
            Content = input
        };
    }

    private ParsedCommand ParseSpecialCommand(string input)
    {
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var commandName = parts[0][1..]; // Remove leading /
        var args = parts.Length > 1 ? parts[1] : string.Empty;

        var command = new ParsedCommand
        {
            Type = CommandType.Special,
            RawInput = input,
            Command = commandName.ToLower(),
            Arguments = ParseArguments(args)
        };

        return command;
    }

    private Dictionary<string, string> ParseArguments(string args)
    {
        var result = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(args))
        {
            return result;
        }

        // Support both --key=value and --key value formats
        var tokens = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string? currentKey = null;

        foreach (var token in tokens)
        {
            if (token.StartsWith("--"))
            {
                var keyValue = token[2..];
                var equalIndex = keyValue.IndexOf('=');

                if (equalIndex > 0)
                {
                    var key = keyValue[..equalIndex];
                    var value = keyValue[(equalIndex + 1)..];
                    result[key] = value;
                    currentKey = null;
                }
                else
                {
                    currentKey = keyValue;
                }
            }
            else if (token.StartsWith('-') && token.Length == 2)
            {
                currentKey = token[1..];
            }
            else if (currentKey != null)
            {
                result[currentKey] = token;
                currentKey = null;
            }
            else
            {
                // Positional argument
                result[$"arg{result.Count}"] = token;
            }
        }

        // Flag without value
        if (currentKey != null)
        {
            result[currentKey] = "true";
        }

        return result;
    }
}

public class ParsedCommand
{
    public bool IsEmpty { get; set; }
    public CommandType Type { get; set; }
    public string RawInput { get; set; } = "";
    public string Command { get; set; } = "";
    public string Content { get; set; } = "";
    public Dictionary<string, string> Arguments { get; set; } = new();

    public string GetArgument(string key, string defaultValue = "")
    {
        return Arguments.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public bool HasFlag(string key)
    {
        return Arguments.ContainsKey(key) && Arguments[key] == "true";
    }
}

public enum CommandType
{
    Prompt,
    Special
}
