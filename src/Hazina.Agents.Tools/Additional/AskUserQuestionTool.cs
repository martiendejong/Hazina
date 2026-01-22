using System.Text;
using System.Text.Json;
using Spectre.Console;

namespace Hazina.Agents.Tools.Additional;

/// <summary>
/// Tool for asking the user questions during execution (like Claude Code's AskUserQuestion)
/// </summary>
public static class AskUserQuestionTool
{
    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "ask_user",
            description: "Ask the user a question when you need clarification, want to validate assumptions, or need a decision. Present options for the user to choose from.",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "question",
                    Type = "string",
                    Required = true,
                    Description = "The question to ask the user. Should be clear and specific."
                },
                new()
                {
                    Name = "options",
                    Type = "array",
                    Required = false,
                    Description = "Array of option objects with 'label' and 'description' fields. If provided, user selects from these. Max 4 options."
                },
                new()
                {
                    Name = "allow_text",
                    Type = "boolean",
                    Required = false,
                    Description = "Allow free-text input in addition to options (default: true)"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    if (!root.TryGetProperty("question", out var questionElement))
                        return "Error: question parameter is required";

                    var question = questionElement.GetString() ?? "";
                    if (string.IsNullOrWhiteSpace(question))
                        return "Error: question cannot be empty";

                    bool allowText = true;
                    if (root.TryGetProperty("allow_text", out var allowTextElement))
                    {
                        allowText = allowTextElement.GetBoolean();
                    }

                    var options = new List<(string Label, string Description)>();
                    if (root.TryGetProperty("options", out var optionsElement))
                    {
                        foreach (var opt in optionsElement.EnumerateArray())
                        {
                            var label = opt.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
                            var desc = opt.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(label))
                            {
                                options.Add((label, desc));
                            }
                        }
                    }

                    // Display the question
                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new Rule("[yellow]Question from Assistant[/]").RuleStyle("yellow"));
                    AnsiConsole.MarkupLine($"[white]{Markup.Escape(question)}[/]");
                    AnsiConsole.WriteLine();

                    string answer;

                    if (options.Count > 0)
                    {
                        // Show options for selection
                        var choices = new List<string>();
                        for (int i = 0; i < options.Count; i++)
                        {
                            var (label, desc) = options[i];
                            var choiceText = string.IsNullOrEmpty(desc)
                                ? label
                                : $"{label} - {desc}";
                            choices.Add(choiceText);
                            AnsiConsole.MarkupLine($"  [cyan]{i + 1}.[/] {Markup.Escape(label)}");
                            if (!string.IsNullOrEmpty(desc))
                            {
                                AnsiConsole.MarkupLine($"     [dim]{Markup.Escape(desc)}[/]");
                            }
                        }

                        if (allowText)
                        {
                            AnsiConsole.MarkupLine($"  [cyan]{options.Count + 1}.[/] Other (type custom response)");
                        }

                        AnsiConsole.WriteLine();
                        AnsiConsole.Markup("[green]Your choice (number or text): [/]");
                        var input = Console.ReadLine()?.Trim() ?? "";

                        // Check if it's a number selection
                        if (int.TryParse(input, out int choice))
                        {
                            if (choice >= 1 && choice <= options.Count)
                            {
                                answer = options[choice - 1].Label;
                            }
                            else if (allowText && choice == options.Count + 1)
                            {
                                AnsiConsole.Markup("[green]Enter your response: [/]");
                                answer = Console.ReadLine()?.Trim() ?? "";
                            }
                            else
                            {
                                answer = input; // Treat as text
                            }
                        }
                        else
                        {
                            answer = input; // Free text
                        }
                    }
                    else
                    {
                        // No options, just free text
                        AnsiConsole.Markup("[green]Your response: [/]");
                        answer = Console.ReadLine()?.Trim() ?? "";
                    }

                    AnsiConsole.Write(new Rule().RuleStyle("dim"));
                    AnsiConsole.WriteLine();

                    return $"User's answer: {answer}";
                }
                catch (Exception ex)
                {
                    return $"Error asking user: {ex.Message}";
                }
            }
        );
    }
}
