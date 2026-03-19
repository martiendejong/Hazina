// =============================================================================
// Hazina QuickStart: Minimal Console App
// =============================================================================
//
// This is the fastest path to a working Hazina agent.
// No DI container, no config files - just code.
//
// What this demonstrates:
//   - Bootstrapping AgentManager with a single LLM provider
//   - Loading agent configuration from JSON files
//   - Running a simple question-answer loop
//
// Prerequisites:
//   - Set environment variable OPENAI_API_KEY (or ANTHROPIC_API_KEY)
//   - Optionally create agents.json and stores.json (see below for minimal examples)
//
// Run: dotnet run
// =============================================================================

using Hazina.AgentFactory.Core;

// ---------------------------------------------------------------------------
// 1. Configuration - grab API key from environment
// ---------------------------------------------------------------------------
var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException(
        "Set the OPENAI_API_KEY environment variable before running this sample.");

// Log path for agent activity (creates if missing)
var logPath = Path.Combine(AppContext.BaseDirectory, "hazina-agent.log");

// ---------------------------------------------------------------------------
// 2. Bootstrap AgentManager
//    The AgentManager is the single entry point for all agent interactions.
//    It owns stores, agents, flows, and conversation history.
//
//    Minimal bootstrap uses inline JSON so you don't need config files.
//    For production, use file-based overload: new AgentManager(storesPath, agentsPath, flowsPath, ...)
// ---------------------------------------------------------------------------

// Minimal stores configuration - no external document store for this quickstart
const string storesJson = "[]";

// One agent: a general-purpose assistant
const string agentsJson = """
[
  {
    "name": "assistant",
    "description": "General-purpose Hazina assistant",
    "prompt": "You are a helpful assistant. Answer questions clearly and concisely. If you don't know something, say so.",
    "stores": [],
    "functions": []
  }
]
""";

// No multi-agent flows for this quickstart
const string flowsJson = "[]";

Console.WriteLine("Initializing Hazina AgentManager...");

var manager = new AgentManager(
    storesJson: storesJson,
    agentsJson: agentsJson,
    flowsJson: flowsJson,
    openAIApiKey: openAiKey,
    logFilePath: logPath,
    isContent: true);  // isContent=true means we pass JSON strings, not file paths

// Load stores and agents (async initialization)
await manager.LoadStoresAndAgents();

Console.WriteLine($"Ready. Agents loaded: {manager.Agents.Count}");
Console.WriteLine("Type a message and press Enter. Type 'exit' to quit.");
Console.WriteLine(new string('-', 60));

// ---------------------------------------------------------------------------
// 3. Get the agent
// ---------------------------------------------------------------------------
var agent = manager.GetAgent("assistant")
    ?? throw new InvalidOperationException(
        "Agent 'assistant' not found. Check agentsJson configuration.");

// ---------------------------------------------------------------------------
// 4. Interactive loop
// ---------------------------------------------------------------------------
while (true)
{
    Console.Write("\nYou: ");
    var userInput = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(userInput))
        continue;

    if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Goodbye.");
        break;
    }

    Console.Write("Agent: ");

    // Send message to agent - conversation history is maintained automatically
    // by the AgentManager across turns
    var response = await agent.Generator.GetResponse(
        message: userInput,
        cancellationToken: CancellationToken.None,
        history: manager.History,
        addRelevantDocuments: false,
        addFilesList: false,
        toolsContext: agent.Tools);

    Console.WriteLine(response.Result);

    // Optional: show token usage / cost
    if (response.TokenUsage != null)
    {
        Console.WriteLine($"  [tokens: {response.TokenUsage.TotalTokens} | cost: ${response.TokenUsage.TotalCost:F4}]");
    }
}
