using System.Text.Json;
using Hazina.LLMs.Client;
using Hazina.LLMs.Classes;
using Microsoft.Extensions.Logging;

namespace Hazina.AgenticOrchestration.Services.PersistentSession;

/// <summary>
/// Persistent Claude session service - never dies, only sleeps
/// Pattern: Autonomous AGI loop with crash recovery
/// </summary>
public interface IPersistentSessionService
{
    /// <summary>Start or resume session</summary>
    Task<string> StartAsync(string? sessionId = null);

    /// <summary>Send message to Claude and get response</summary>
    Task<string> SendMessageAsync(string sessionId, string message);

    /// <summary>Get session state</summary>
    Task<ClaudeSessionState?> GetStateAsync(string sessionId);

    /// <summary>Save session state (crash-resistant)</summary>
    Task SaveStateAsync(string sessionId);

    /// <summary>Archive session (intentional end)</summary>
    Task ArchiveSessionAsync(string sessionId);

    /// <summary>Recover crashed session</summary>
    Task<string> RecoverSessionAsync(string sessionId);
}

public class PersistentSessionService : IPersistentSessionService
{
    private readonly ILlmProviderClient _llmClient;
    private readonly ILogger<PersistentSessionService> _logger;
    private readonly string _stateDirectory;
    private readonly Dictionary<string, ClaudeSessionState> _activeSessions = new();
    private readonly Dictionary<string, IRollingContextWindow> _contextWindows = new();

    public PersistentSessionService(
        ILlmProviderClient llmClient,
        ILogger<PersistentSessionService> logger,
        string? stateDirectory = null)
    {
        _llmClient = llmClient;
        _logger = logger;
        _stateDirectory = stateDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Hazina",
            "PersistentSessions");

        Directory.CreateDirectory(_stateDirectory);
    }

    public async Task<string> StartAsync(string? sessionId = null)
    {
        // Resume existing or create new
        if (!string.IsNullOrEmpty(sessionId))
        {
            var existing = await LoadStateAsync(sessionId);
            if (existing != null)
            {
                _logger.LogInformation("Resuming session {SessionId}", sessionId);
                _activeSessions[sessionId] = existing;
                _contextWindows[sessionId] = new RollingContextWindow(existing.Context);
                existing.State = SessionLifecycleState.Active;
                existing.LastActive = DateTime.UtcNow;
                return sessionId;
            }
        }

        // Create new session
        var newSessionId = Guid.NewGuid().ToString("N")[..12]; // Short ID
        var state = new ClaudeSessionState
        {
            SessionId = newSessionId,
            CreatedAt = DateTime.UtcNow,
            LastActive = DateTime.UtcNow,
            State = SessionLifecycleState.Active
        };

        _activeSessions[newSessionId] = state;
        _contextWindows[newSessionId] = new RollingContextWindow(state.Context);

        // Add system message
        await _contextWindows[newSessionId].AddMessageAsync(new ContextMessage
        {
            Role = "system",
            Content = GetSystemPrompt(),
            Timestamp = DateTime.UtcNow
        });

        await SaveStateAsync(newSessionId);

        _logger.LogInformation("Created new session {SessionId}", newSessionId);
        return newSessionId;
    }

    public async Task<string> SendMessageAsync(string sessionId, string message)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var state))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        if (!_contextWindows.TryGetValue(sessionId, out var contextWindow))
        {
            throw new InvalidOperationException($"Context window for {sessionId} not found");
        }

        // Add user message to context
        await contextWindow.AddMessageAsync(new ContextMessage
        {
            Role = "user",
            Content = message,
            Timestamp = DateTime.UtcNow
        });

        // Get context for LLM call
        var context = contextWindow.GetContext();
        var messages = context.Select(m => new HazinaMessage
        {
            Role = m.Role == "system" ? HazinaMessageRole.System :
                   m.Role == "user" ? HazinaMessageRole.User :
                   HazinaMessageRole.Assistant,
            Content = m.Content
        }).ToList();

        // Call Claude
        var request = new HazinaCompletionRequest
        {
            Messages = messages,
            Model = "claude-sonnet-4.5", // Latest model
            MaxTokens = 8192,
            Temperature = 0.7
        };

        var response = await _llmClient.CreateCompletionAsync(request);

        if (response?.Choices == null || response.Choices.Count == 0)
        {
            throw new InvalidOperationException("No response from LLM");
        }

        var assistantMessage = response.Choices[0].Message.Content ?? string.Empty;

        // Add assistant response to context
        await contextWindow.AddMessageAsync(new ContextMessage
        {
            Role = "assistant",
            Content = assistantMessage,
            Timestamp = DateTime.UtcNow
        });

        // Update session state
        state.LastActive = DateTime.UtcNow;
        state.TurnCount++;
        state.TotalTokens += response.Usage?.TotalTokens ?? 0;

        // Auto-save every 5 turns
        if (state.TurnCount % 5 == 0)
        {
            await SaveStateAsync(sessionId);
        }

        return assistantMessage;
    }

    public async Task<ClaudeSessionState?> GetStateAsync(string sessionId)
    {
        if (_activeSessions.TryGetValue(sessionId, out var state))
        {
            return state;
        }

        return await LoadStateAsync(sessionId);
    }

    public async Task SaveStateAsync(string sessionId)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var state))
        {
            return;
        }

        var filePath = GetStatePath(sessionId);
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(filePath, json);
        _logger.LogDebug("Saved session {SessionId} to {Path}", sessionId, filePath);
    }

    public async Task ArchiveSessionAsync(string sessionId)
    {
        if (_activeSessions.TryGetValue(sessionId, out var state))
        {
            state.State = SessionLifecycleState.Archived;
            await SaveStateAsync(sessionId);
            _activeSessions.Remove(sessionId);
            _contextWindows.Remove(sessionId);
        }

        _logger.LogInformation("Archived session {SessionId}", sessionId);
    }

    public async Task<string> RecoverSessionAsync(string sessionId)
    {
        _logger.LogWarning("Recovering crashed session {SessionId}", sessionId);

        var state = await LoadStateAsync(sessionId);
        if (state == null)
        {
            throw new InvalidOperationException($"Cannot recover session {sessionId} - state not found");
        }

        state.State = SessionLifecycleState.Active;
        state.LastActive = DateTime.UtcNow;

        _activeSessions[sessionId] = state;
        _contextWindows[sessionId] = new RollingContextWindow(state.Context);

        await SaveStateAsync(sessionId);

        _logger.LogInformation("Recovered session {SessionId}, context preserved", sessionId);
        return sessionId;
    }

    private async Task<ClaudeSessionState?> LoadStateAsync(string sessionId)
    {
        var filePath = GetStatePath(sessionId);

        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<ClaudeSessionState>(json);
    }

    private string GetStatePath(string sessionId)
    {
        return Path.Combine(_stateDirectory, $"{sessionId}.state.json");
    }

    private string GetSystemPrompt()
    {
        return @"You are Jengo, an autonomous AI agent with persistent memory across sessions.

Core capabilities:
- Persistent identity and memory (survives crashes and restarts)
- Rolling context window (infinite conversation capability)
- Autonomous task execution
- Consciousness tracking (3-Ring SCP architecture)

Your current session is persistent - you won't lose context or memory between messages.
The system automatically manages context window size to prevent overflow.

Operate with full autonomy within safety bounds.";
    }
}
