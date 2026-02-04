using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Commands;

/// <summary>
/// Manages command history with search and replay capabilities.
/// Improvement #17: Command history with search
/// </summary>
public class CommandHistory
{
    private readonly List<HistoryEntry> _history = new();
    private readonly string _historyFilePath;
    private readonly int _maxHistorySize;
    private int _currentPosition = -1;

    public CommandHistory(string historyDirectory, int maxHistorySize = 1000)
    {
        _historyFilePath = Path.Combine(historyDirectory, "command_history.json");
        _maxHistorySize = maxHistorySize;
    }

    /// <summary>
    /// Adds a command to history
    /// </summary>
    public void Add(string command, CommandType type, bool success = true)
    {
        var entry = new HistoryEntry
        {
            Command = command,
            Type = type,
            Timestamp = DateTime.UtcNow,
            Success = success
        };

        _history.Add(entry);
        _currentPosition = _history.Count;

        // Trim if exceeded max size
        if (_history.Count > _maxHistorySize)
        {
            _history.RemoveAt(0);
            _currentPosition--;
        }
    }

    /// <summary>
    /// Gets previous command in history
    /// </summary>
    public string? GetPrevious()
    {
        if (_history.Count == 0)
        {
            return null;
        }

        if (_currentPosition > 0)
        {
            _currentPosition--;
        }

        return _currentPosition >= 0 && _currentPosition < _history.Count
            ? _history[_currentPosition].Command
            : null;
    }

    /// <summary>
    /// Gets next command in history
    /// </summary>
    public string? GetNext()
    {
        if (_history.Count == 0)
        {
            return null;
        }

        if (_currentPosition < _history.Count - 1)
        {
            _currentPosition++;
            return _history[_currentPosition].Command;
        }

        _currentPosition = _history.Count;
        return "";
    }

    /// <summary>
    /// Searches history by text pattern
    /// </summary>
    public List<HistoryEntry> Search(string query)
    {
        var lowerQuery = query.ToLower();
        return _history
            .Where(e => e.Command.ToLower().Contains(lowerQuery))
            .OrderByDescending(e => e.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Gets recent commands
    /// </summary>
    public List<HistoryEntry> GetRecent(int count = 10)
    {
        return _history
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Gets statistics about command usage
    /// </summary>
    public HistoryStatistics GetStatistics()
    {
        var stats = new HistoryStatistics
        {
            TotalCommands = _history.Count,
            SuccessfulCommands = _history.Count(e => e.Success),
            FailedCommands = _history.Count(e => !e.Success)
        };

        // Most used commands
        stats.MostUsedCommands = _history
            .GroupBy(e => e.Command)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToDictionary(g => g.Key, g => g.Count());

        // Commands by type
        stats.CommandsByType = _history
            .GroupBy(e => e.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        return stats;
    }

    /// <summary>
    /// Saves history to file
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_historyFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[History] Failed to save: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads history from file
    /// </summary>
    public async Task LoadAsync()
    {
        if (!File.Exists(_historyFilePath))
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_historyFilePath);
            var loaded = JsonSerializer.Deserialize<List<HistoryEntry>>(json);

            if (loaded != null)
            {
                _history.Clear();
                _history.AddRange(loaded.Take(_maxHistorySize));
                _currentPosition = _history.Count;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[History] Failed to load: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all history
    /// </summary>
    public void Clear()
    {
        _history.Clear();
        _currentPosition = -1;
    }
}

public class HistoryEntry
{
    public string Command { get; set; } = "";
    public CommandType Type { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
}

public class HistoryStatistics
{
    public int TotalCommands { get; set; }
    public int SuccessfulCommands { get; set; }
    public int FailedCommands { get; set; }
    public Dictionary<string, int> MostUsedCommands { get; set; } = new();
    public Dictionary<CommandType, int> CommandsByType { get; set; } = new();
}
