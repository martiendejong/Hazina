// Sample 02 – Creating an Agent with Custom Tools
//
// Shows how to build a custom AgentTool, register it with an Agent,
// and run a task that triggers tool calls.
//
// Prerequisites (NuGet):
//   dotnet add package Hazina.AI.Agents
//   dotnet add package Hazina.AI.Providers

using Hazina.AI.Agents.Core;
using Hazina.AI.Agents.Tools;
using Hazina.AI.Providers.Core;

// ── 1. Define a custom tool ────────────────────────────────────────────────
public class WeatherTool : AgentTool
{
    public WeatherTool()
    {
        Name        = "get_weather";
        Description = "Returns the current weather for a given city.";

        Parameters["city"] = new ToolParameter
        {
            Type        = "string",
            Description = "The name of the city (e.g., 'Amsterdam').",
            Required    = true
        };
    }

    public override Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateArguments(arguments, out var error))
            return Task.FromResult(new ToolResult { Success = false, Error = error });

        var city = arguments["city"].ToString()!;

        // In a real implementation this would call a weather API.
        var weather = $"It is 18°C and partly cloudy in {city}.";

        return Task.FromResult(new ToolResult
        {
            Success  = true,
            Output   = weather,
            Metadata = { ["city"] = city, ["unit"] = "celsius" }
        });
    }
}

// ── 2. Bootstrap orchestrator ─────────────────────────────────────────────
// (See sample 01 for full provider setup – abbreviated here for brevity)
var orchestrator = new ProviderOrchestrator();
orchestrator.RegisterProvider("openai", openAiClient, openAiMetadata);

// ── 3. Create the agent and register the tool ──────────────────────────────
var agent = new Agent(
    name:        "WeatherBot",
    description: "An assistant that can look up current weather conditions.",
    orchestrator: orchestrator,
    config: new AgentConfig { MaxToolIterations = 5 });

agent.RegisterTool(new WeatherTool());

// ── 4. Execute a task ─────────────────────────────────────────────────────
var result = await agent.ExecuteAsync(
    "What is the weather like in Amsterdam and Berlin today?",
    cancellationToken: CancellationToken.None);

if (result.Success)
    Console.WriteLine($"Agent answer: {result.Result}");
else
    Console.WriteLine($"Agent error: {result.Error}");

Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F1}s");

// ── 5. Inspect conversation history ───────────────────────────────────────
foreach (var message in agent.GetConversationHistory())
{
    Console.WriteLine($"[{message.Role}] {message.Content[..Math.Min(80, message.Content.Length)]}...");
}
