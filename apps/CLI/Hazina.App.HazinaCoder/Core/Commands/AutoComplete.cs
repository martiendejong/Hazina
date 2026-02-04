namespace Hazina.App.HazinaCoder.Core.Commands;

/// <summary>
/// Provides auto-completion for commands and arguments.
/// Improvement #19: Auto-complete system
/// </summary>
public class AutoComplete
{
    private readonly List<CompletionItem> _completions = new();
    private readonly CommandAliases _aliases;

    public AutoComplete(CommandAliases aliases)
    {
        _aliases = aliases;
        LoadBuiltInCompletions();
    }

    private void LoadBuiltInCompletions()
    {
        // Commands
        _completions.Add(new CompletionItem("/help", "Show help information"));
        _completions.Add(new CompletionItem("/quit", "Exit HazinaCoder"));
        _completions.Add(new CompletionItem("/clear", "Clear screen"));
        _completions.Add(new CompletionItem("/history", "Show command history"));
        _completions.Add(new CompletionItem("/stats", "Show statistics"));
        _completions.Add(new CompletionItem("/config", "Show configuration"));
        _completions.Add(new CompletionItem("/reset", "Reset conversation"));
        _completions.Add(new CompletionItem("/save", "Save conversation"));
        _completions.Add(new CompletionItem("/load", "Load conversation"));
        _completions.Add(new CompletionItem("/provider", "Change LLM provider"));
        _completions.Add(new CompletionItem("/model", "Change model"));
        _completions.Add(new CompletionItem("/verbose", "Toggle verbose mode"));

        // Common arguments
        _completions.Add(new CompletionItem("--provider", "Specify LLM provider"));
        _completions.Add(new CompletionItem("--model", "Specify model"));
        _completions.Add(new CompletionItem("--verbose", "Enable verbose output"));
        _completions.Add(new CompletionItem("--help", "Show help"));
    }

    public List<CompletionItem> GetCompletions(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return _completions.Where(c => c.Value.StartsWith('/')).ToList();
        }

        var lower = input.ToLower();
        return _completions
            .Where(c => c.Value.ToLower().StartsWith(lower))
            .OrderBy(c => c.Value)
            .ToList();
    }
}

public class CompletionItem
{
    public string Value { get; set; }
    public string Description { get; set; }

    public CompletionItem(string value, string description)
    {
        Value = value;
        Description = description;
    }
}
