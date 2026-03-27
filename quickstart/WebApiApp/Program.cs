// =============================================================================
// Hazina QuickStart: ASP.NET Core Web API
// =============================================================================
//
// What this demonstrates:
//   - HazinaBootstrap.AddHazina() for one-call DI registration
//   - Multi-provider setup (OpenAI primary, Anthropic fallback)
//   - Minimal API endpoints that proxy to Hazina agents
//   - Streaming response endpoint
//
// Prerequisites:
//   - Set environment variables OPENAI_API_KEY and/or ANTHROPIC_API_KEY
//
// Run: dotnet run
// Endpoints:
//   POST /chat          - Send a message, get a full response
//   POST /chat/stream   - Send a message, get a streamed response
//   GET  /health        - Health check with provider status
// =============================================================================

using Hazina.AgentFactory.Core;
using Hazina.AI.FluentAPI.Bootstrap;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Add Hazina - single-call bootstrap
//    Registers IProviderOrchestrator, AgentManager, and all supporting services.
// ---------------------------------------------------------------------------
builder.Services.AddHazina(options =>
{
    // Primary provider: OpenAI
    var openAiKey = builder.Configuration["OPENAI_API_KEY"]
        ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    if (!string.IsNullOrEmpty(openAiKey))
    {
        options.OpenAiApiKey = openAiKey;
        options.OpenAiModel = "gpt-4o-mini";  // Cost-effective default
    }

    // Optional fallback: Anthropic Claude
    var anthropicKey = builder.Configuration["ANTHROPIC_API_KEY"]
        ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    if (!string.IsNullOrEmpty(anthropicKey))
    {
        options.AnthropicApiKey = anthropicKey;
        options.AnthropicModel = "claude-3-5-haiku-latest";
    }

    // AgentManager settings
    options.AgentLogPath = Path.Combine(AppContext.BaseDirectory, "hazina-api.log");
    options.AgentsJson = """
    [
      {
        "name": "assistant",
        "description": "API assistant agent",
        "prompt": "You are a helpful AI assistant integrated into a web API. Answer questions clearly. Be concise - API responses should be focused and direct.",
        "stores": [],
        "functions": []
      }
    ]
    """;
});

// ---------------------------------------------------------------------------
// 2. Standard ASP.NET Core setup
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// ---------------------------------------------------------------------------
// 3. Endpoints
// ---------------------------------------------------------------------------

// POST /chat - Full response (simple request/response)
app.MapPost("/chat", async (ChatRequest request, IHazinaService hazina) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest("Message cannot be empty.");

    var response = await hazina.ChatAsync(request.Message, request.ConversationId);

    return Results.Ok(new ChatResponse(
        Message: response.Result,
        ConversationId: response.ConversationId,
        TokensUsed: response.TokenUsage?.TotalTokens ?? 0,
        Cost: response.TokenUsage?.TotalCost ?? 0m));
})
.WithName("Chat")
.WithSummary("Send a message to the Hazina assistant")
.WithDescription("Returns a full response. For streaming, use POST /chat/stream.");

// POST /chat/stream - Streaming response
app.MapPost("/chat/stream", async (ChatRequest request, IHazinaService hazina, HttpResponse httpResponse) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        httpResponse.StatusCode = 400;
        return;
    }

    httpResponse.ContentType = "text/event-stream";
    httpResponse.Headers.CacheControl = "no-cache";

    await foreach (var chunk in hazina.StreamChatAsync(request.Message, request.ConversationId))
    {
        var data = $"data: {chunk}\n\n";
        await httpResponse.WriteAsync(data, Encoding.UTF8);
        await httpResponse.Body.FlushAsync();
    }

    await httpResponse.WriteAsync("data: [DONE]\n\n", Encoding.UTF8);
})
.WithName("ChatStream")
.WithSummary("Send a message and receive a streaming SSE response");

// GET /health - Provider health + agent status
app.MapGet("/health", (IHazinaService hazina) =>
{
    var status = hazina.GetStatus();
    return Results.Ok(status);
})
.WithName("Health")
.WithSummary("Hazina health check with provider status");

app.Run();

// ---------------------------------------------------------------------------
// Request/Response models
// ---------------------------------------------------------------------------
record ChatRequest(string Message, string? ConversationId = null);
record ChatResponse(string Message, string? ConversationId, long TokensUsed, decimal Cost);
