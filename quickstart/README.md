# Hazina QuickStart Templates

Two minimal, runnable projects that demonstrate Hazina bootstrapping patterns.

---

## Templates

### 1. MinimalConsoleApp

**What it shows:** Bootstrap `AgentManager` directly in a console app — no DI, no config files.

**Good for:** CLI tools, scripts, experiments, non-web scenarios.

**Files:**
- `MinimalConsoleApp/Program.cs` — Fully commented, single-file bootstrap
- `MinimalConsoleApp/MinimalConsoleApp.csproj`

**Run:**
```bash
cd MinimalConsoleApp
export OPENAI_API_KEY=sk-...
dotnet run
```

**Key pattern:**
```csharp
var manager = new AgentManager(
    storesJson: "[]",
    agentsJson: "[{ \"name\": \"assistant\", \"prompt\": \"...\", \"stores\": [], \"functions\": [] }]",
    flowsJson:  "[]",
    openAIApiKey: openAiKey,
    logFilePath: "hazina.log",
    isContent: true);   // true = inline JSON, not file paths

await manager.LoadStoresAndAgents();

var agent = manager.GetAgent("assistant");
var response = await agent.Generator.GetResponse(
    message: userInput,
    cancel: CancellationToken.None,
    history: manager.History,
    addRelevantDocuments: false,
    addFilesList: false,
    toolsContext: agent.Tools);
```

---

### 2. WebApiApp

**What it shows:** ASP.NET Core minimal API with `HazinaBootstrap.AddHazina()` — one-call DI registration.

**Good for:** REST APIs, microservices, anything that needs SSE streaming.

**Files:**
- `WebApiApp/Program.cs` — Multi-provider setup, `/chat` and `/chat/stream` endpoints
- `WebApiApp/WebApiApp.csproj`

**Run:**
```bash
cd WebApiApp
export OPENAI_API_KEY=sk-...
dotnet run
```

**Endpoints:**
| Method | Path | Description |
|--------|------|-------------|
| POST | `/chat` | Full response (JSON) |
| POST | `/chat/stream` | Server-Sent Events stream |
| GET | `/health` | Provider + agent status |

**Example request:**
```bash
curl -X POST http://localhost:5000/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What is Hazina?"}'
```

**Key pattern:**
```csharp
// Program.cs — one call registers everything
builder.Services.AddHazina(options =>
{
    options.OpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    options.AnthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    options.AgentsJson = """
    [{ "name": "assistant", "prompt": "You are helpful.", "stores": [], "functions": [] }]
    """;
});

// In endpoint handler — inject IHazinaService
app.MapPost("/chat", async (ChatRequest req, IHazinaService hazina) =>
{
    var response = await hazina.ChatAsync(req.Message);
    return Results.Ok(response);
});
```

---

## HazinaBootstrap

`HazinaBootstrap.AddHazina()` is an `IServiceCollection` extension defined in `Hazina.AI.FluentAPI`.

It registers:
- `IProviderOrchestrator` — multi-provider LLM routing (OpenAI + Anthropic with automatic failover)
- `AgentManager` — manages agents, stores, flows, conversation history
- `IHazinaService` — high-level service for chat + streaming

**Options reference:**
```csharp
builder.Services.AddHazina(options =>
{
    // LLM providers (set at least one)
    options.OpenAiApiKey     = "sk-...";
    options.OpenAiModel      = "gpt-4o-mini";          // default
    options.AnthropicApiKey  = "sk-ant-...";
    options.AnthropicModel   = "claude-3-5-haiku-latest"; // default

    // Agent configuration — inline JSON or file paths
    options.AgentsJson       = "[...]";                // inline (default: "[]")
    options.AgentsJsonPath   = "agents.json";          // file path (overrides AgentsJson)
    options.StoresJson       = "[...]";
    options.StoresJsonPath   = "stores.json";
    options.FlowsJson        = "[...]";
    options.FlowsJsonPath    = "flows.json";

    // Misc
    options.AgentLogPath     = "hazina.log";
    options.DefaultProvider  = ProviderPreference.OpenAiFirst; // OpenAiFirst | AnthropicFirst | FirstAvailable
});
```

---

## Agent Configuration JSON

The agent configuration format used by `AgentManager`:

```json
[
  {
    "name": "assistant",
    "description": "General-purpose assistant",
    "prompt": "You are a helpful AI assistant. Answer clearly and concisely.",
    "stores": [],
    "functions": []
  }
]
```

For agents with RAG (document retrieval), add store references:

```json
[
  {
    "name": "codebase-agent",
    "description": "Answers questions about this codebase",
    "prompt": "You are a code assistant. Answer based on the indexed codebase.",
    "stores": [{ "name": "code", "write": false }],
    "functions": ["read_file", "list_files"]
  }
]
```

See `Hazina.AgentFactory/README.md` for the full configuration reference.

---

## Provider Selection

When both OpenAI and Anthropic keys are configured, Hazina uses priority-based routing:
- OpenAI is tried first (lower priority number = higher priority)
- Anthropic is the automatic fallback if OpenAI fails

Change the behavior with `options.DefaultProvider`:
- `ProviderPreference.OpenAiFirst` — OpenAI primary, Anthropic fallback
- `ProviderPreference.AnthropicFirst` — Anthropic primary, OpenAI fallback
- `ProviderPreference.FirstAvailable` — whichever key is set (default)

---

## From Project References to NuGet

These templates use project references for development. When publishing your app,
replace project references with NuGet packages:

```xml
<PackageReference Include="Hazina.AgentFactory" Version="2.0.0" />
<PackageReference Include="Hazina.AI.FluentAPI" Version="1.0.0" />
<PackageReference Include="Hazina.LLMs.OpenAI" Version="1.0.0" />
<PackageReference Include="Hazina.LLMs.Anthropic" Version="1.0.0" />
```

---

## Related

- `templates/quickstart/` — Code-only `.cs.template` snippets (no project files)
- `Hazina.AgentFactory/README.md` — Full AgentManager documentation
- `src/Core/AI/Hazina.AI.FluentAPI/README.md` — FluentAPI documentation
- `samples/workflows/` — More advanced workflow examples
