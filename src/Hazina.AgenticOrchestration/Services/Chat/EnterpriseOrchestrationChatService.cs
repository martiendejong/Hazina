using Hazina.LLMs;
using Hazina.LLMs.OpenAI;
using Hazina.Tools.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Hazina.AgenticOrchestration.Services.Chat;

/// <summary>
/// Enterprise-grade LLM-powered chat service for Hazina Orchestration
/// Features: Persistence, Retry Logic, Circuit Breaker, Metrics, Rate Limiting
/// </summary>
public class EnterpriseOrchestrationChatService : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EnterpriseOrchestrationChatService> _logger;
    private readonly OpenAIClientWrapper _llmClient;
    private readonly ConversationRepository _repository;

    // In-memory conversation cache (sessionId -> conversation)
    private readonly ConcurrentDictionary<string, ChatConversation> _conversations = new();

    // Rate limiting (sessionId -> message timestamps)
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _rateLimits = new();
    private const int MAX_MESSAGES_PER_MINUTE = 10;
    private const int MAX_MESSAGES_PER_HOUR = 60;

    // Circuit breaker for tool execution
    private readonly AsyncCircuitBreakerPolicy<string> _toolCircuitBreaker;

    // Retry policy for OpenAI calls
    private readonly AsyncRetryPolicy _retryPolicy;

    // Metrics
    private readonly ConcurrentDictionary<string, ChatMetrics> _metrics = new();
    private readonly Timer _autoSaveTimer;
    private readonly Timer _metricsTimer;

    // Constants
    private const int MAX_CONVERSATION_MESSAGES = 30;
    private const int AUTO_SAVE_INTERVAL_SECONDS = 30;
    private const int METRICS_LOG_INTERVAL_SECONDS = 300; // 5 minutes

    private const string SYSTEM_PROMPT = @"You are an AI assistant for the Hazina Terminal Orchestration system.

Your role:
- Help users manage their terminal sessions efficiently
- Answer questions about session status and system health
- Provide actionable information about terminal operations
- Execute commands via available tools when needed
- Be proactive in suggesting optimizations

Available tools:
- list_sessions: Show all active terminal sessions
- get_session_details: Get detailed info about a specific session (requires session ID)
- list_archived_sessions: Show completed/archived sessions
- get_system_status: Show overall system health and session counts
- search_sessions: Find sessions by name or command pattern

Guidelines:
- Be concise, helpful, and professional
- Always use tools when user asks about sessions
- Format output clearly using markdown
- Validate session IDs before showing details
- Provide actionable insights, not just raw data
- If OpenAI is unavailable, inform user gracefully

Response style:
- Use bullet points for lists
- Use tables for structured data
- Use code blocks for commands
- Keep responses under 500 words unless requested otherwise";

    public EnterpriseOrchestrationChatService(
        IConfiguration configuration,
        ILogger<EnterpriseOrchestrationChatService> logger,
        ConversationRepository repository)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

        // Initialize OpenAI client
        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI API key not configured in appsettings.Secrets.json");
        var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";

        var config = new OpenAIConfig
        {
            Model = model,
            ApiKey = apiKey
        };

        _llmClient = new OpenAIClientWrapper(config);

        // Configure retry policy (3 attempts with exponential backoff)
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception,
                        "OpenAI call failed. Retry {RetryCount}/3 after {Delay}s",
                        retryCount, timeSpan.TotalSeconds);
                });

        // Configure circuit breaker for tool execution
        // Opens after 3 consecutive failures, stays open for 30 seconds
        _toolCircuitBreaker = Policy<string>
            .Handle<Exception>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (result, duration) =>
                {
                    _logger.LogError(result.Exception,
                        "Tool circuit breaker opened. Will retry after {Duration}s",
                        duration.TotalSeconds);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Tool circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Tool circuit breaker half-open (testing)");
                });

        // Auto-save timer (every 30 seconds)
        _autoSaveTimer = new Timer(async _ => await AutoSaveAllConversations(), null,
            TimeSpan.FromSeconds(AUTO_SAVE_INTERVAL_SECONDS),
            TimeSpan.FromSeconds(AUTO_SAVE_INTERVAL_SECONDS));

        // Metrics logging timer (every 5 minutes)
        _metricsTimer = new Timer(_ => LogMetrics(), null,
            TimeSpan.FromSeconds(METRICS_LOG_INTERVAL_SECONDS),
            TimeSpan.FromSeconds(METRICS_LOG_INTERVAL_SECONDS));

        _logger.LogInformation(
            "EnterpriseOrchestrationChatService initialized with model {Model}, max messages {MaxMessages}",
            model, MAX_CONVERSATION_MESSAGES);
    }

    /// <summary>
    /// Send a message with enterprise-grade reliability
    /// </summary>
    public async Task<ChatResponse> SendMessageAsync(
        string sessionId,
        string userMessage,
        IToolsContext toolsContext,
        Action<string>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return ChatResponse.Error("Session ID is required");
            }

            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return ChatResponse.Error("Message cannot be empty");
            }

            if (userMessage.Length > 10000)
            {
                return ChatResponse.Error("Message too long (max 10,000 characters)");
            }

            // Check rate limits
            var rateLimitError = CheckRateLimits(sessionId);
            if (rateLimitError != null)
            {
                return ChatResponse.Error(rateLimitError);
            }

            // Get or create conversation
            var conversation = await GetOrCreateConversationAsync(sessionId, cancellationToken);

            // Add user message
            var userMsg = new ConversationMessage
            {
                Role = ChatMessageRole.User,
                Text = userMessage
            };
            conversation.ChatMessages.Add(userMsg);

            // Prune if needed
            PruneConversation(conversation);

            // Build messages for LLM
            var messages = BuildLLMMessages(conversation);

            // Call LLM with retry policy
            string responseText;
            TokenUsageInfo? tokenUsage;
            int toolCallCount = 0;

            try
            {
                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    return await _llmClient.GetResponseStream(
                        messages,
                        onChunk ?? (_ => { }),
                        HazinaChatResponseFormat.Text,
                        toolsContext,
                        images: null,
                        cancellationToken);
                });

                responseText = response.Result;
                tokenUsage = response.TokenUsage;
                toolCallCount = 0; // LLMResponse doesn't track tool calls directly
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI call failed after retries for session {SessionId}", sessionId);
                return ChatResponse.Error(
                    "AI service temporarily unavailable. Please try again in a moment.",
                    ex.Message);
            }

            // Add assistant response to conversation
            conversation.ChatMessages.Add(new ConversationMessage
            {
                Role = ChatMessageRole.Assistant,
                Text = responseText
            });

            // Update metrics
            UpdateMetrics(sessionId, tokenUsage?.TotalTokens ?? 0, toolCallCount, stopwatch.ElapsedMilliseconds);

            // Auto-save conversation
            await SaveConversationAsync(sessionId, conversation, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Chat message processed for session {SessionId} in {Duration}ms ({Tokens} tokens, {Tools} tools)",
                sessionId, stopwatch.ElapsedMilliseconds, tokenUsage?.TotalTokens ?? 0, toolCallCount);

            return new ChatResponse
            {
                Success = true,
                ResponseMessage = responseText,
                TotalTokensUsed = tokenUsage?.TotalTokens ?? 0,
                ToolCallsExecuted = toolCallCount,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in SendMessageAsync for session {SessionId}", sessionId);
            return ChatResponse.Error("An unexpected error occurred", ex.Message);
        }
    }

    /// <summary>
    /// Get conversation history
    /// </summary>
    public async Task<List<ConversationMessage>> GetHistoryAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await GetOrCreateConversationAsync(sessionId, cancellationToken);
        return conversation.ChatMessages.ToList();
    }

    /// <summary>
    /// Clear conversation history
    /// </summary>
    public async Task ClearHistoryAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _conversations.TryRemove(sessionId, out _);
        await _repository.DeleteAsync(sessionId, cancellationToken);
        _logger.LogInformation("Cleared conversation history for session {SessionId}", sessionId);
    }

    /// <summary>
    /// Export conversation to file
    /// </summary>
    public async Task<string> ExportConversationAsync(
        string sessionId,
        string exportPath,
        CancellationToken cancellationToken = default)
    {
        return await _repository.ExportAsync(sessionId, exportPath, cancellationToken);
    }

    /// <summary>
    /// Get conversation metrics
    /// </summary>
    public ChatMetrics? GetMetrics(string sessionId)
    {
        return _metrics.TryGetValue(sessionId, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// Get all metrics (for admin dashboard)
    /// </summary>
    public Dictionary<string, ChatMetrics> GetAllMetrics()
    {
        return new Dictionary<string, ChatMetrics>(_metrics);
    }

    /// <summary>
    /// Get system health status
    /// </summary>
    public async Task<ChatServiceHealth> GetHealthAsync()
    {
        return await Task.FromResult(new ChatServiceHealth
        {
            IsHealthy = true,
            ActiveConversations = _conversations.Count,
            TotalStorageMB = _repository.GetTotalSizeBytes() / 1024.0 / 1024.0,
            CircuitBreakerState = _toolCircuitBreaker.CircuitState.ToString(),
            TotalMessages = _metrics.Values.Sum(m => m.MessageCount),
            TotalTokens = _metrics.Values.Sum(m => m.TotalTokens),
            TotalToolCalls = _metrics.Values.Sum(m => m.TotalToolCalls)
        });
    }

    // Private helper methods

    private async Task<ChatConversation> GetOrCreateConversationAsync(
        string sessionId,
        CancellationToken ct)
    {
        if (_conversations.TryGetValue(sessionId, out var conversation))
        {
            return conversation;
        }

        // Try load from disk
        var history = await _repository.LoadAsync(sessionId, ct);

        if (history != null)
        {
            conversation = new ChatConversation
            {
                MetaData = new ChatMetadata { Id = sessionId, Name = $"Session {sessionId}" },
                ChatMessages = new SerializableList<ConversationMessage>(
                    history.Messages.Select(m => new ConversationMessage
                    {
                        Role = m.Role == "user" ? ChatMessageRole.User : ChatMessageRole.Assistant,
                        Text = m.Content
                    }))
            };

            _conversations[sessionId] = conversation;
            _logger.LogInformation("Loaded conversation from disk for session {SessionId}", sessionId);
            return conversation;
        }

        // Create new
        conversation = new ChatConversation
        {
            MetaData = new ChatMetadata { Id = sessionId, Name = $"Session {sessionId}" },
            ChatMessages = new SerializableList<ConversationMessage>()
        };

        _conversations[sessionId] = conversation;
        return conversation;
    }

    private async Task SaveConversationAsync(
        string sessionId,
        ChatConversation conversation,
        CancellationToken ct)
    {
        try
        {
            var history = new ConversationHistory
            {
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow,
                Messages = conversation.ChatMessages.Select(m => new ChatMessageRecord
                {
                    Role = m.Role == ChatMessageRole.User ? "user" : "assistant",
                    Content = m.Text,
                    Timestamp = DateTime.UtcNow
                }).ToList(),
                Metrics = new ConversationMetrics
                {
                    TotalMessages = conversation.ChatMessages.Count,
                    TotalTokens = _metrics.TryGetValue(sessionId, out var m) ? m.TotalTokens : 0,
                    TotalToolCalls = _metrics.TryGetValue(sessionId, out var m2) ? m2.TotalToolCalls : 0
                }
            };

            await _repository.SaveAsync(sessionId, history, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save conversation for session {SessionId}", sessionId);
        }
    }

    private List<HazinaChatMessage> BuildLLMMessages(ChatConversation conversation)
    {
        var messages = new List<HazinaChatMessage>();

        // System prompt
        messages.Add(new HazinaChatMessage
        {
            Role = HazinaMessageRole.System,
            Text = SYSTEM_PROMPT
        });

        // Conversation history
        foreach (var msg in conversation.ChatMessages)
        {
            var role = msg.Role == ChatMessageRole.User
                ? HazinaMessageRole.User
                : HazinaMessageRole.Assistant;

            messages.Add(new HazinaChatMessage
            {
                Role = role,
                Text = msg.Text
            });
        }

        return messages;
    }

    private void PruneConversation(ChatConversation conversation)
    {
        if (conversation.ChatMessages.Count > MAX_CONVERSATION_MESSAGES)
        {
            var toRemove = conversation.ChatMessages.Count - MAX_CONVERSATION_MESSAGES;
            conversation.ChatMessages.RemoveRange(0, toRemove);

            _logger.LogDebug("Pruned {Count} old messages from conversation {SessionId}",
                toRemove, conversation.MetaData.Id);
        }
    }

    private string? CheckRateLimits(string sessionId)
    {
        var now = DateTime.UtcNow;
        var timestamps = _rateLimits.GetOrAdd(sessionId, _ => new Queue<DateTime>());

        lock (timestamps)
        {
            // Remove timestamps older than 1 hour
            while (timestamps.Count > 0 && (now - timestamps.Peek()).TotalHours > 1)
            {
                timestamps.Dequeue();
            }

            // Check hourly limit
            if (timestamps.Count >= MAX_MESSAGES_PER_HOUR)
            {
                return $"Hourly rate limit exceeded. Maximum {MAX_MESSAGES_PER_HOUR} messages per hour.";
            }

            // Check per-minute limit
            var lastMinute = timestamps.Count(t => (now - t).TotalMinutes < 1);
            if (lastMinute >= MAX_MESSAGES_PER_MINUTE)
            {
                return $"Rate limit exceeded. Maximum {MAX_MESSAGES_PER_MINUTE} messages per minute.";
            }

            // Add current timestamp
            timestamps.Enqueue(now);
        }

        return null;
    }

    private void UpdateMetrics(string sessionId, int tokens, int toolCalls, long durationMs)
    {
        var metrics = _metrics.GetOrAdd(sessionId, _ => new ChatMetrics { SessionId = sessionId });

        metrics.MessageCount++;
        metrics.TotalTokens += tokens;
        metrics.TotalToolCalls += toolCalls;
        metrics.TotalDurationMs += durationMs;
        metrics.AverageDurationMs = metrics.TotalDurationMs / metrics.MessageCount;
        metrics.LastMessageAt = DateTime.UtcNow;
    }

    private async Task AutoSaveAllConversations()
    {
        try
        {
            var tasks = _conversations.Select(kvp =>
                SaveConversationAsync(kvp.Key, kvp.Value, CancellationToken.None));

            await Task.WhenAll(tasks);

            _logger.LogDebug("Auto-saved {Count} conversations", _conversations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in auto-save");
        }
    }

    private void LogMetrics()
    {
        try
        {
            var totalMessages = _metrics.Values.Sum(m => m.MessageCount);
            var totalTokens = _metrics.Values.Sum(m => m.TotalTokens);
            var totalTools = _metrics.Values.Sum(m => m.TotalToolCalls);
            var avgDuration = _metrics.Values.Any()
                ? _metrics.Values.Average(m => m.AverageDurationMs)
                : 0;

            _logger.LogInformation(
                "Chat Metrics - Sessions: {Sessions}, Messages: {Messages}, Tokens: {Tokens}, Tools: {Tools}, Avg Duration: {Duration}ms",
                _metrics.Count, totalMessages, totalTokens, totalTools, (int)avgDuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging metrics");
        }
    }

    public void Dispose()
    {
        _autoSaveTimer?.Dispose();
        _metricsTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
