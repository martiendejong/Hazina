namespace Hazina.App.HazinaCoder.Core.Commands;

/// <summary>
/// Command palette with fuzzy search
/// Iteration 41: Enhanced CLI UX
/// </summary>
public class CommandPaletteWithFuzzy
{
    public record Command(string Name, string Description, Func<Task> Action);

    private readonly List<Command> _commands = new();

    public void Register(string name, string description, Func<Task> action)
    {
        _commands.Add(new Command(name, description, action));
    }

    public List<Command> FuzzySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return _commands;

        var queryLower = query.ToLowerInvariant();

        return _commands
            .Select(cmd => new
            {
                Command = cmd,
                Score = CalculateFuzzyScore(cmd.Name.ToLowerInvariant(), queryLower)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Command)
            .ToList();
    }

    private int CalculateFuzzyScore(string text, string query)
    {
        var score = 0;
        var queryIndex = 0;

        for (int i = 0; i < text.Length && queryIndex < query.Length; i++)
        {
            if (text[i] == query[queryIndex])
            {
                score += 10;
                queryIndex++;

                // Bonus for consecutive matches
                if (i > 0 && text[i - 1] == query[queryIndex - 1])
                    score += 5;
            }
        }

        // Penalty if query not fully matched
        if (queryIndex < query.Length)
            return 0;

        // Bonus for shorter texts (more precise match)
        score += Math.Max(0, 50 - text.Length);

        return score;
    }

    public async Task ExecuteAsync(string commandName)
    {
        var command = _commands.FirstOrDefault(c =>
            c.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase));

        if (command != null)
            await command.Action();
    }
}

/// <summary>
/// Auto-complete for CLI
/// Iteration 42: Auto-completion
/// </summary>
public class AutoCompleter
{
    private readonly List<string> _completions = new();

    public void AddCompletions(params string[] completions)
    {
        _completions.AddRange(completions);
    }

    public List<string> GetCompletions(string partial)
    {
        if (string.IsNullOrWhiteSpace(partial))
            return _completions;

        return _completions
            .Where(c => c.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c)
            .ToList();
    }

    public string GetBestCompletion(string partial)
    {
        var completions = GetCompletions(partial);
        return completions.FirstOrDefault() ?? partial;
    }
}
