namespace Hazina.App.HazinaCoder.Core.Commands;

/// <summary>
/// Command templates and batch execution.
/// Improvement #21: Interactive mode improvements
/// Improvement #22: Batch command execution
/// Improvement #23: Command chaining
/// Improvement #24: Command templates
/// </summary>
public class CommandTemplates
{
    private readonly Dictionary<string, CommandTemplate> _templates = new();

    public CommandTemplates()
    {
        LoadDefaultTemplates();
    }

    private void LoadDefaultTemplates()
    {
        _templates["quick-start"] = new CommandTemplate
        {
            Name = "quick-start",
            Description = "Quick start workflow",
            Commands = new[]
            {
                "/config show",
                "/provider auto",
                "Please help me with code review"
            }
        };

        _templates["debug-session"] = new CommandTemplate
        {
            Name = "debug-session",
            Description = "Start debugging session",
            Commands = new[]
            {
                "/verbose on",
                "/provider anthropic",
                "Analyze error logs and suggest fixes"
            }
        };
    }

    public async Task ExecuteBatchAsync(string[] commands, Func<string, Task> executor)
    {
        foreach (var cmd in commands)
        {
            Console.WriteLine($"> {cmd}");
            await executor(cmd);
        }
    }

    public async Task ExecuteTemplateAsync(string templateName, Func<string, Task> executor)
    {
        if (!_templates.TryGetValue(templateName, out var template))
        {
            Console.WriteLine($"Template not found: {templateName}");
            return;
        }

        Console.WriteLine($"Executing template: {template.Name}");
        await ExecuteBatchAsync(template.Commands, executor);
    }
}

public class CommandTemplate
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] Commands { get; set; } = Array.Empty<string>();
}
