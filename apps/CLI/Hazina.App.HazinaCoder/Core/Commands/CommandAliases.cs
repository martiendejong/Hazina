using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Commands;

/// <summary>
/// Manages command aliases and shortcuts.
/// Improvement #18: Alias system expansion
/// </summary>
public class CommandAliases
{
    private readonly Dictionary<string, string> _aliases = new();
    private readonly string _aliasesFilePath;

    public CommandAliases(string configDirectory)
    {
        _aliasesFilePath = Path.Combine(configDirectory, "aliases.json");
        LoadDefaultAliases();
    }

    /// <summary>
    /// Loads default built-in aliases
    /// </summary>
    private void LoadDefaultAliases()
    {
        _aliases["?"] = "/help";
        _aliases["h"] = "/help";
        _aliases["q"] = "/quit";
        _aliases["exit"] = "/quit";
        _aliases["cls"] = "/clear";
        _aliases["history"] = "/history";
        _aliases["stats"] = "/statistics";
        _aliases["config"] = "/show-config";
        _aliases["reset"] = "/reset-conversation";
        _aliases["save"] = "/save-conversation";
        _aliases["load"] = "/load-conversation";
    }

    /// <summary>
    /// Adds or updates an alias
    /// </summary>
    public void AddAlias(string alias, string command)
    {
        _aliases[alias.ToLower()] = command;
    }

    /// <summary>
    /// Removes an alias
    /// </summary>
    public bool RemoveAlias(string alias)
    {
        return _aliases.Remove(alias.ToLower());
    }

    /// <summary>
    /// Resolves an alias to its command
    /// </summary>
    public string Resolve(string input)
    {
        var trimmed = input.Trim();

        // Check if it's a special command
        if (trimmed.StartsWith('/'))
        {
            var parts = trimmed.Split(' ', 2);
            var commandName = parts[0][1..].ToLower();

            if (_aliases.TryGetValue(commandName, out var resolved))
            {
                // Preserve arguments
                if (parts.Length > 1)
                {
                    return $"{resolved} {parts[1]}";
                }
                return resolved;
            }
        }
        else
        {
            // Check if entire input is an alias
            if (_aliases.TryGetValue(trimmed.ToLower(), out var resolved))
            {
                return resolved;
            }
        }

        return input;
    }

    /// <summary>
    /// Gets all defined aliases
    /// </summary>
    public Dictionary<string, string> GetAllAliases()
    {
        return new Dictionary<string, string>(_aliases);
    }

    /// <summary>
    /// Checks if an alias exists
    /// </summary>
    public bool HasAlias(string alias)
    {
        return _aliases.ContainsKey(alias.ToLower());
    }

    /// <summary>
    /// Saves aliases to file
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            // Only save user-defined aliases (not defaults)
            var userAliases = _aliases
                .Where(kvp => !IsDefaultAlias(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var json = JsonSerializer.Serialize(userAliases, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_aliasesFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Aliases] Failed to save: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads aliases from file
    /// </summary>
    public async Task LoadAsync()
    {
        if (!File.Exists(_aliasesFilePath))
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_aliasesFilePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (loaded != null)
            {
                foreach (var kvp in loaded)
                {
                    _aliases[kvp.Key.ToLower()] = kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Aliases] Failed to load: {ex.Message}");
        }
    }

    private bool IsDefaultAlias(string alias)
    {
        var defaults = new[] { "?", "h", "q", "exit", "cls", "history", "stats", "config", "reset", "save", "load" };
        return defaults.Contains(alias.ToLower());
    }
}
