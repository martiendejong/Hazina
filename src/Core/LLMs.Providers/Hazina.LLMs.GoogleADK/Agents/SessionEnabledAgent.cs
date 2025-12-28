using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Sessions;
using Hazina.LLMs.GoogleADK.Sessions.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Agents;

/// <summary>
/// LLM agent with session management capabilities
/// </summary>
public class SessionEnabledAgent : LlmAgent
{
    private readonly SessionManager _sessionManager;
    private Session? _currentSession;

    public Session? CurrentSession => _currentSession;

    public SessionEnabledAgent(
        string name,
        ILLMClient llmClient,
        SessionManager sessionManager,
        AgentContext? context = null,
        int maxHistorySize = 50) : base(name, llmClient, context, maxHistorySize)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
    }

    /// <summary>
    /// Start a new session
    /// </summary>
    public async Task<Session> StartSessionAsync(
        string? userId = null,
        SessionConfiguration? configuration = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        _currentSession = await _sessionManager.CreateSessionAsync(
            agentName: Name,
            userId: userId,
            configuration: configuration,
            cancellationToken: cancellationToken
        );

        if (metadata != null)
        {
            foreach (var kvp in metadata)
            {
                _currentSession.Metadata[kvp.Key] = kvp.Value;
            }
        }

        // Restore conversation history if any
        if (_currentSession.Messages.Any())
        {
            ClearHistory();
            foreach (var msg in _currentSession.Messages)
            {
                AddMessage(
                    new HazinaMessageRole { Role = msg.Role },
                    msg.Content
                );
            }
        }

        Context.Log(LogLevel.Information, "Started session {SessionId}", _currentSession.SessionId);
        return _currentSession;
    }

    /// <summary>
    /// Resume an existing session
    /// </summary>
    public async Task<Session?> ResumeSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        _currentSession = await _sessionManager.ResumeSessionAsync(sessionId, cancellationToken);

        if (_currentSession == null)
        {
            Context.Log(LogLevel.Warning, "Failed to resume session {SessionId}", sessionId);
            return null;
        }

        // Restore conversation history
        ClearHistory();
        foreach (var msg in _currentSession.Messages)
        {
            AddMessage(
                new HazinaMessageRole { Role = msg.Role },
                msg.Content
            );
        }

        Context.Log(LogLevel.Information, "Resumed session {SessionId}", sessionId);
        return _currentSession;
    }

    /// <summary>
    /// Execute with session tracking
    /// </summary>
    public async Task<AgentResult> ExecuteWithSessionAsync(
        string input,
        bool autoCreateSession = true,
        CancellationToken cancellationToken = default)
    {
        // Auto-create session if needed
        if (_currentSession == null && autoCreateSession)
        {
            await StartSessionAsync(cancellationToken: cancellationToken);
        }

        if (_currentSession == null)
        {
            throw new InvalidOperationException("No active session. Call StartSessionAsync or ResumeSessionAsync first.");
        }

        // Update session status
        _currentSession.Status = SessionStatus.Active;

        // Save user message to session
        await _sessionManager.AddMessageAsync(
            _currentSession.SessionId,
            "user",
            input,
            cancellationToken: cancellationToken
        );

        // Execute agent
        var result = await ExecuteAsync(input, cancellationToken);

        // Save assistant message to session
        await _sessionManager.AddMessageAsync(
            _currentSession.SessionId,
            "assistant",
            result.Output,
            metadata: result.Metadata,
            cancellationToken: cancellationToken
        );

        // Update session state
        _currentSession.State = GetStateSnapshot();
        await _sessionManager.UpdateSessionAsync(_currentSession, cancellationToken);

        return result;
    }

    /// <summary>
    /// Save current session
    /// </summary>
    public async Task SaveSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentSession == null)
        {
            throw new InvalidOperationException("No active session to save");
        }

        // Capture current state
        _currentSession.State = GetStateSnapshot();

        await _sessionManager.UpdateSessionAsync(_currentSession, cancellationToken);

        Context.Log(LogLevel.Information, "Saved session {SessionId}", _currentSession.SessionId);
    }

    /// <summary>
    /// Pause current session
    /// </summary>
    public async Task PauseSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentSession == null)
        {
            throw new InvalidOperationException("No active session to pause");
        }

        await _sessionManager.PauseSessionAsync(_currentSession.SessionId, cancellationToken);

        Context.Log(LogLevel.Information, "Paused session {SessionId}", _currentSession.SessionId);
    }

    /// <summary>
    /// Complete current session
    /// </summary>
    public async Task CompleteSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentSession == null)
        {
            throw new InvalidOperationException("No active session to complete");
        }

        await _sessionManager.CompleteSessionAsync(_currentSession.SessionId, cancellationToken);

        _currentSession = null;

        Context.Log(LogLevel.Information, "Completed session");
    }

    /// <summary>
    /// Get all sessions for this agent
    /// </summary>
    public async Task<List<Session>> GetAgentSessionsAsync(
        string? userId = null,
        SessionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        return await _sessionManager.ListSessionsAsync(
            agentName: Name,
            userId: userId,
            status: status,
            cancellationToken: cancellationToken
        );
    }

    protected override async Task OnDisposeAsync()
    {
        // Save session before disposal
        if (_currentSession != null && _currentSession.Status == SessionStatus.Active)
        {
            try
            {
                await SaveSessionAsync();
            }
            catch (Exception ex)
            {
                Context.Log(LogLevel.Error, "Error saving session during disposal: {Error}", ex.Message);
            }
        }

        await base.OnDisposeAsync();
    }
}
