// Sample 04 – Streaming Responses and Tool Context
//
// Shows how to:
//   a) Stream LLM responses token-by-token for real-time UI updates
//   b) Wire up token tracking via IToolsContext.OnTokensUsed
//   c) Use ShouldContinue to keep the agentic loop running past a single tool call
//
// Prerequisites (NuGet):
//   dotnet add package Hazina.LLMs.Client
//   dotnet add package Hazina.AI.Providers

using Hazina.LLMs;
using Hazina.AI.Providers.Core;

// ── 1. Build a ToolsContext with hooks ────────────────────────────────────
var toolsCtx = new ToolsContext
{
    ProjectId      = "my-project-id",
    MaxContinuations = 3,

    // Track token usage
    OnTokensUsed = (model, inputTokens, outputTokens, operationId) =>
        Console.WriteLine($"[tokens] {model}: {inputTokens} in / {outputTokens} out (op={operationId})"),

    // Log each tool execution
    OnToolExecuted = (toolName, toolResult, turn) =>
        Console.WriteLine($"[turn {turn}] Tool '{toolName}' returned: {toolResult[..Math.Min(80, toolResult.Length)]}"),

    // Keep the loop going after a non-tool response if the task seems unfinished
    ShouldContinue = (response, turn) =>
    {
        if (turn >= 3) return false;                              // Hard stop after 3 extra turns
        return response.Contains("I still need to", StringComparison.OrdinalIgnoreCase);
    },

    ContinuationPrompt = "Please continue with the next step."
};

// Register a tool on the context
toolsCtx.Add(new HazinaChatTool
{
    Name        = "list_files",
    Description = "Lists files in a given directory.",
    Parameters  = new HazinaToolParameters
    {
        Properties = new Dictionary<string, HazinaToolProperty>
        {
            ["path"] = new() { Type = "string", Description = "The directory path." }
        },
        Required = ["path"]
    }
});

// ── 2. Stream a response ──────────────────────────────────────────────────
// (Assumes orchestrator is set up per sample 01)
var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.System, Text = "You are a helpful coding assistant." },
    new() { Role = HazinaMessageRole.User,   Text = "List the files in /src and explain each one." }
};

Console.Write("Streaming: ");

var streamResult = await orchestrator.GetResponseStream(
    messages,
    onChunkReceived: chunk => Console.Write(chunk),   // Print each token as it arrives
    HazinaChatResponseFormat.Text,
    toolsContext: toolsCtx,
    images: null,
    cancel: CancellationToken.None);

Console.WriteLine();
Console.WriteLine($"[Done] Total tokens: {streamResult.TokenUsage?.TotalTokens}");

// ── 3. Strongly-typed structured response ─────────────────────────────────
// Use GetResponse<T> when you want the LLM to return JSON that maps to a C# type.
var typedResponse = await orchestrator.GetResponse<MyCodeReview>(
    messages,
    toolsContext: null,
    images: null,
    cancel: CancellationToken.None);

if (typedResponse.Result is { } review)
{
    Console.WriteLine($"Score   : {review.Score}");
    Console.WriteLine($"Summary : {review.Summary}");
    foreach (var issue in review.Issues)
        Console.WriteLine($"  - [{issue.Severity}] {issue.Message}");
}

// ── Supporting types ──────────────────────────────────────────────────────
public class MyCodeReview : ChatResponse<MyCodeReview>
{
    public int Score { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<CodeIssue> Issues { get; set; } = new();
}

public class CodeIssue
{
    public string Severity { get; set; } = "info";
    public string Message  { get; set; } = string.Empty;
}
