namespace Hazina.App.HazinaCoder.Core.Commands;

/// <summary>
/// Comprehensive help system with examples.
/// Improvement #20: Help system with examples
/// </summary>
public class HelpSystem
{
    private readonly Dictionary<string, HelpTopic> _topics = new();

    public HelpSystem()
    {
        LoadHelpTopics();
    }

    private void LoadHelpTopics()
    {
        _topics["help"] = new HelpTopic
        {
            Name = "help",
            Summary = "Display help information",
            Usage = "/help [topic]",
            Examples = new[]
            {
                "/help - Show all commands",
                "/help provider - Show provider help"
            }
        };

        _topics["provider"] = new HelpTopic
        {
            Name = "provider",
            Summary = "Change LLM provider",
            Usage = "/provider <name>",
            Description = "Switch between OpenAI, Anthropic, Ollama, or auto mode",
            Examples = new[]
            {
                "/provider anthropic - Use Claude",
                "/provider openai - Use GPT-4",
                "/provider auto - Auto-select best provider"
            }
        };

        _topics["config"] = new HelpTopic
        {
            Name = "config",
            Summary = "Configuration management",
            Usage = "/config [action]",
            Examples = new[]
            {
                "/config show - Display current configuration",
                "/config reload - Reload configuration",
                "/config validate - Validate configuration"
            }
        };
    }

    public string GetHelp(string? topic = null)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return GetAllCommandsHelp();
        }

        if (_topics.TryGetValue(topic.ToLower(), out var helpTopic))
        {
            return FormatHelpTopic(helpTopic);
        }

        return $"No help available for: {topic}\nUse /help to see all commands.";
    }

    private string GetAllCommandsHelp()
    {
        var help = "HazinaCoder Commands:\n\n";
        foreach (var topic in _topics.Values)
        {
            help += $"  {topic.Usage,-30} - {topic.Summary}\n";
        }
        return help;
    }

    private string FormatHelpTopic(HelpTopic topic)
    {
        var help = $"\n{topic.Name.ToUpper()}\n";
        help += $"  {topic.Summary}\n\n";
        help += $"Usage:\n  {topic.Usage}\n";

        if (!string.IsNullOrWhiteSpace(topic.Description))
        {
            help += $"\n{topic.Description}\n";
        }

        if (topic.Examples.Any())
        {
            help += "\nExamples:\n";
            foreach (var example in topic.Examples)
            {
                help += $"  {example}\n";
            }
        }

        return help;
    }
}

public class HelpTopic
{
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Usage { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] Examples { get; set; } = Array.Empty<string>();
}
