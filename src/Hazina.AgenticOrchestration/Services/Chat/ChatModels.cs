namespace Hazina.AgenticOrchestration.Services.Chat;

/// <summary>
/// Chat response model (shared between OrchestrationChatService and EnterpriseOrchestrationChatService)
/// </summary>
public class ChatResponse
{
    public bool Success { get; set; }
    public string ResponseMessage { get; set; } = string.Empty;
    public int TotalTokensUsed { get; set; }
    public int ToolCallsExecuted { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorDetails { get; set; }

    public static ChatResponse Error(string message, string? details = null)
    {
        return new ChatResponse
        {
            Success = false,
            ErrorMessage = message,
            ErrorDetails = details
        };
    }
}

/// <summary>
/// Chat metrics per session
/// </summary>
public class ChatMetrics
{
    public string SessionId { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public int TotalTokens { get; set; }
    public int TotalToolCalls { get; set; }
    public long TotalDurationMs { get; set; }
    public long AverageDurationMs { get; set; }
    public DateTime LastMessageAt { get; set; }
}

/// <summary>
/// Service health status
/// </summary>
public class ChatServiceHealth
{
    public bool IsHealthy { get; set; }
    public int ActiveConversations { get; set; }
    public double TotalStorageMB { get; set; }
    public string CircuitBreakerState { get; set; } = string.Empty;
    public int TotalMessages { get; set; }
    public int TotalTokens { get; set; }
    public int TotalToolCalls { get; set; }
}
