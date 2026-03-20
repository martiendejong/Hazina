using Hazina.LLMs;
using Hazina.LLMs.Registry;
using Hazina.LLMs.Registry.Factory;
using Hazina.Terminal.ChatAgent;
using Spectre.Console;
using System.Text.Json;

// Display welcome banner
AnsiConsole.Clear();
AnsiConsole.Write(
    new FigletText("Hazina Chat")
        .LeftJustified()
        .Color(Color.Blue));

AnsiConsole.MarkupLine("[dim]Terminal Chat Agent with LLM Integration[/]");
AnsiConsole.MarkupLine("[dim]Type /help for commands[/]\n");

// Load configuration
var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
ChatConfig config;

if (File.Exists(configPath))
{
    var json = await File.ReadAllTextAsync(configPath);
    config = JsonSerializer.Deserialize<ChatConfig>(json) ?? new ChatConfig();
}
else
{
    config = new ChatConfig();
    AnsiConsole.MarkupLine("[yellow]No appsettings.json found, using defaults[/]");
}

// Initialize LLM client
var registry = new ProviderRegistry(); // Built-in providers loaded in constructor

var factory = new ProviderFactory(registry);
ILLMClient llmClient;

try
{
    llmClient = string.IsNullOrEmpty(config.ProviderId)
        ? factory.CreateDefaultClient()
        : factory.CreateClient(config.ProviderId);

    AnsiConsole.MarkupLine($"[green]✓[/] Connected to LLM provider");
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Failed to initialize LLM client: {ex.Message}[/]");
    AnsiConsole.MarkupLine("[yellow]Please configure LLM providers in Hazina settings[/]");
    return;
}

// Initialize context manager
var contextManager = new ContextManager(config.MaxContextMessages, config.SystemPrompt);

// Main chat loop
while (true)
{
    // Get user input
    var userInput = AnsiConsole.Ask<string>("[blue]You:[/]");

    if (string.IsNullOrWhiteSpace(userInput))
        continue;

    // Handle commands
    if (userInput.StartsWith("/"))
    {
        var command = userInput.ToLower().Trim();

        if (command == "/exit" || command == "/quit")
        {
            AnsiConsole.MarkupLine("[dim]Goodbye![/]");
            break;
        }
        else if (command == "/clear")
        {
            contextManager.Clear();
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[green]✓[/] Context cleared");
            continue;
        }
        else if (command == "/context")
        {
            ShowContext(contextManager);
            continue;
        }
        else if (command == "/help")
        {
            ShowHelp();
            continue;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Unknown command: {command}[/]");
            AnsiConsole.MarkupLine("[dim]Type /help for available commands[/]");
            continue;
        }
    }

    // Add user message to context
    contextManager.AddUserMessage(userInput);

    // Get response from LLM with streaming
    AnsiConsole.Markup("[green]Assistant:[/] ");

    try
    {
        var messages = contextManager.GetMessages();
        var response = await llmClient.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            toolsContext: null,
            images: null,
            cancel: CancellationToken.None);

        // Display response (would be streamed in production)
        var responseText = response.Result ?? "";

        // Simulate streaming effect
        foreach (var chunk in SplitIntoChunks(responseText, 5))
        {
            AnsiConsole.Markup(chunk);
            await Task.Delay(10); // Small delay for visual effect
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        // Add assistant response to context
        contextManager.AddAssistantMessage(responseText);

        // Show token usage if available
        if (response.TokenUsage != null && config.ShowTokenUsage)
        {
            AnsiConsole.MarkupLine(
                $"[dim]Tokens: {response.TokenUsage.InputTokens} in, " +
                $"{response.TokenUsage.OutputTokens} out, " +
                $"{response.TokenUsage.TotalTokens} total[/]");
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
    }
}

static void ShowHelp()
{
    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("[blue]Command[/]")
        .AddColumn("[blue]Description[/]");

    table.AddRow("/help", "Show this help message");
    table.AddRow("/clear", "Clear conversation context");
    table.AddRow("/context", "Show current context window");
    table.AddRow("/exit or /quit", "Exit the chat");

    AnsiConsole.Write(table);
}

static void ShowContext(ContextManager contextManager)
{
    var messages = contextManager.GetMessages();

    AnsiConsole.MarkupLine($"[blue]Context Window:[/] {messages.Count} messages");
    AnsiConsole.WriteLine();

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("[blue]Role[/]")
        .AddColumn("[blue]Message[/]", column => column.Width = 80);

    foreach (var msg in messages)
    {
        var roleColor = "white";
        if (msg.Role == HazinaMessageRole.System)
            roleColor = "yellow";
        else if (msg.Role == HazinaMessageRole.User)
            roleColor = "blue";
        else if (msg.Role == HazinaMessageRole.Assistant)
            roleColor = "green";

        var truncatedText = msg.Text.Length > 100
            ? msg.Text.Substring(0, 100) + "..."
            : msg.Text;

        table.AddRow(
            $"[{roleColor}]{msg.Role.Role}[/]",
            truncatedText.EscapeMarkup());
    }

    AnsiConsole.Write(table);
}

static IEnumerable<string> SplitIntoChunks(string text, int chunkSize)
{
    for (int i = 0; i < text.Length; i += chunkSize)
    {
        yield return text.Substring(i, Math.Min(chunkSize, text.Length - i));
    }
}
