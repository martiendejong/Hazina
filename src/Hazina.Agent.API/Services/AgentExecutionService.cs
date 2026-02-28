using System.Runtime.CompilerServices;
using System.Diagnostics;
using Hazina.Agent.API.Models;
using OpenAI.Chat;

namespace Hazina.Agent.API.Services;

public class AgentExecutionService : IAgentExecutionService
{
    private readonly ISessionLogger _sessionLogger;
    private readonly IStateSyncService _stateSyncService;
    private readonly ILogger<AgentExecutionService> _logger;
    private readonly OpenAI.OpenAIClient _openAiClient;

    public AgentExecutionService(
        ISessionLogger sessionLogger,
        IStateSyncService stateSyncService,
        ILogger<AgentExecutionService> logger,
        OpenAI.OpenAIClient openAiClient)
    {
        _sessionLogger = sessionLogger;
        _stateSyncService = stateSyncService;
        _logger = logger;
        _openAiClient = openAiClient;
    }

    public async IAsyncEnumerable<AgentEvent> ExecuteAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var sessionId = await _sessionLogger.CreateSessionAsync(request, ct);

        _logger.LogInformation("Session {SessionId} started for instruction: {Instruction}",
            sessionId, request.Instruction);

        var model = request.Model ?? "gpt-4o";
        var chatClient = _openAiClient.GetChatClient(model);

        var messages = new List<ChatMessage>
        {
            new UserChatMessage(request.Instruction)
        };

        var chatOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = request.Options?.MaxTokens ?? 4000,
            Temperature = (float)(request.Options?.Temperature ?? 0.7)
        };

        var finalContent = string.Empty;

        // Stream from OpenAI
        await foreach (var update in chatClient.CompleteChatStreamingAsync(messages, chatOptions, ct))
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    finalContent += contentPart.Text;

                    var agentEvent = new AgentEvent
                    {
                        Type = "output",
                        Data = new OutputData { Content = contentPart.Text }
                    };

                    await _sessionLogger.LogEventAsync(sessionId, agentEvent, ct);
                    yield return agentEvent;
                }
            }
        }

        sw.Stop();

        // Complete event
        var completeData = new CompleteData
        {
            SessionId = sessionId,
            FinalResult = finalContent,
            ToolsUsed = 0,
            Duration = sw.Elapsed.TotalSeconds,
            Provider = "openai",
            Model = model
        };

        await _sessionLogger.LogCompleteAsync(sessionId, completeData, ct);

        var completeEvent = new AgentEvent
        {
            Type = "complete",
            Data = completeData
        };

        await _sessionLogger.LogEventAsync(sessionId, completeEvent, ct);

        _logger.LogInformation("Session {SessionId} completed in {Duration}s",
            sessionId, sw.Elapsed.TotalSeconds);

        yield return completeEvent;

        // Publish learning event (fire and forget)
        _ = Task.Run(async () =>
        {
            try
            {
                var identity = await _stateSyncService.GetIdentityAsync(ct);
                var learningEvent = new LearningEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.UtcNow,
                    AgentId = identity.AgentId,
                    SessionId = sessionId,
                    EventType = "insight",
                    Data = new
                    {
                        Instruction = request.Instruction,
                        Duration = completeData.Duration,
                        Model = completeData.Model,
                        ResultLength = finalContent.Length
                    },
                    Confidence = 0.7
                };

                await _stateSyncService.PublishLearningEventAsync(learningEvent, ct);
                _logger.LogInformation("Published learning event for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish learning event for session {SessionId}", sessionId);
            }
        }, ct);
    }
}
