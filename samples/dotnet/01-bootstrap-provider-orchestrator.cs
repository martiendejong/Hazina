// Sample 01 – Bootstrapping the ProviderOrchestrator
//
// Shows how to register multiple LLM providers (OpenAI + Anthropic), set routing
// priorities, configure cost budgets, and send your first chat message.
//
// Prerequisites (NuGet):
//   dotnet add package Hazina.AI.Providers
//   dotnet add package Hazina.LLMs.OpenAI   (or Hazina.LLMs.Anthropic)

using Hazina.AI.Providers.Core;
using Hazina.LLMs;
using Hazina.LLMs.Classes;
using Microsoft.Extensions.Logging;

// ── 1. Create the orchestrator ─────────────────────────────────────────────
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var logger = loggerFactory.CreateLogger<ProviderOrchestrator>();

var orchestrator = new ProviderOrchestrator(logger);

// ── 2. Register providers ──────────────────────────────────────────────────
// OpenAI – highest priority (10), supports tools + vision
var openAiClient = new OpenAIClientWrapper(
    new OpenAIConfig(apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")!, model: "gpt-4o"));

orchestrator.RegisterProvider("openai-gpt4o", openAiClient, new ProviderMetadata
{
    Name        = "openai-gpt4o",
    DisplayName = "OpenAI GPT-4o",
    Type        = ProviderType.OpenAI,
    Priority    = 10,   // Lower = preferred
    Capabilities = new ProviderCapabilities
    {
        SupportsChat     = true,
        SupportsStreaming = true,
        SupportsTools    = true,
        SupportsVision   = true,
        MaxContextWindow = 128_000
    },
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens  = 0.005m,
        OutputCostPer1KTokens = 0.015m,
        Currency              = "USD"
    }
});

// Anthropic Claude – fallback (priority 50)
var claudeClient = new ClaudeClientWrapper(
    new ClaudeConfig(apiKey: Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!, model: "claude-3-5-sonnet-20241022"));

orchestrator.RegisterProvider("anthropic-claude", claudeClient, new ProviderMetadata
{
    Name        = "anthropic-claude",
    DisplayName = "Anthropic Claude 3.5 Sonnet",
    Type        = ProviderType.Anthropic,
    Priority    = 50,
    Capabilities = new ProviderCapabilities
    {
        SupportsChat     = true,
        SupportsStreaming = true,
        SupportsTools    = true,
        MaxContextWindow = 200_000
    },
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens  = 0.003m,
        OutputCostPer1KTokens = 0.015m,
        Currency              = "USD"
    }
});

// ── 3. Set cost budget (optional) ─────────────────────────────────────────
orchestrator.SetBudget("openai-gpt4o",       limit: 10.00m, period: BudgetPeriod.Daily);
orchestrator.SetBudget("anthropic-claude",   limit:  5.00m, period: BudgetPeriod.Daily);
orchestrator.AddBudgetAlert("openai-gpt4o",  thresholdPercentage: 80, message: "OpenAI budget at 80%");

// ── 4. Send a simple chat message ─────────────────────────────────────────
var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.System, Text = "You are a helpful assistant." },
    new() { Role = HazinaMessageRole.User,   Text = "What is the capital of France?" }
};

var response = await orchestrator.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    toolsContext: null,
    images: null,
    cancel: CancellationToken.None);

Console.WriteLine($"Answer  : {response.Result}");
Console.WriteLine($"Tokens  : {response.TokenUsage?.TotalTokens}");
Console.WriteLine($"Cost    : ${orchestrator.GetTotalCost():F4}");
