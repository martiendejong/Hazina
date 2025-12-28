using System.Collections.Concurrent;
using Hazina.LLMs.GoogleADK.Sessions.Models;
using Hazina.LLMs.GoogleADK.Sessions.Storage;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Sessions;

/// <summary>
/// Manages agent sessions with lifecycle, persistence, and recovery
/// </summary>
public class SessionManager : IAsyncDisposable
{
    private readonly ISessionStorage _storage;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, Session> _activeSessions = new();
    private readonly Timer? _cleanupTimer;
    private readonly Timer? _autoSaveTimer;
    private bool _disposed;

    public SessionManager(ISessionStorage storage, ILogger? logger = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _logger = logger;

        // Start cleanup timer (runs every 5 minutes)
        _cleanupTimer = new Timer(
            async _ => await CleanupExpiredSessionsAsync(),
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5)
        );

        // Start auto-save timer (runs every minute)
        _autoSaveTimer = new Timer(
            async _ => await AutoSaveSessionsAsync(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1)
        );

        _logger?.LogInformation("SessionManager initialized");
    }

    /// <summary>
    /// Create a new session
    /// </summary>
    public async Task<Session> CreateSessionAsync(
        string agentName,
        string? userId = null,
        SessionConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        var session = new Session
        {
            SessionId = Guid.NewGuid().ToString(),
            AgentName = agentName,
            UserId = userId,
            Status = SessionStatus.Created,
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow,
            Configuration = configuration ?? new SessionConfiguration()
        };

        // Set expiration if timeout is configured
        if (session.Configuration.TimeoutMinutes > 0)
        {
            session.ExpiresAt = DateTime.UtcNow.AddMinutes(session.Configuration.TimeoutMinutes);
        }

        _activeSessions[session.SessionId] = session;

        if (session.Configuration.PersistToStorage)
        {
            await _storage.SaveSessionAsync(session, cancellationToken);
        }

        _logger?.LogInformation("Created session {SessionId} for agent {AgentName}", session.SessionId, agentName);
        return session;
    }

    /// <summary>
    /// Get an active session
    /// </summary>
    public Session? GetSession(string sessionId)
    {
        return _activeSessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    /// <summary>
    /// Resume a session from storage
    /// </summary>
    public async Task<Session?> ResumeSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        // Check if already active
        if (_activeSessions.TryGetValue(sessionId, out var activeSession))
        {
            _logger?.LogDebug("Session {SessionId} already active", sessionId);
            return activeSession;
        }

        // Load from storage
        var session = await _storage.LoadSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            _logger?.LogWarning("Session {SessionId} not found in storage", sessionId);
            return null;
        }

        // Check if expired
        if (session.IsExpired())
        {
            _logger?.LogWarning("Session {SessionId} is expired", sessionId);
            session.Status = SessionStatus.Expired;
            await _storage.SaveSessionAsync(session, cancellationToken);
            return null;
        }

        // Reactivate session
        session.Status = SessionStatus.Active;
        session.Touch();

        // Update expiration if timeout is configured
        if (session.Configuration.TimeoutMinutes > 0)
        {
            session.ExpiresAt = DateTime.UtcNow.AddMinutes(session.Configuration.TimeoutMinutes);
        }

        _activeSessions[sessionId] = session;

        _logger?.LogInformation("Resumed session {SessionId}", sessionId);
        return session;
    }

    /// <summary>
    /// Update session state
    /// </summary>
    public async Task UpdateSessionAsync(
        Session session,
        CancellationToken cancellationToken = default)
    {
        session.Touch();

        // Update expiration if timeout is configured
        if (session.Configuration.TimeoutMinutes > 0)
        {
            session.ExpiresAt = DateTime.UtcNow.AddMinutes(session.Configuration.TimeoutMinutes);
        }

        if (session.Configuration.PersistToStorage)
        {
            await _storage.SaveSessionAsync(session, cancellationToken);
        }

        _logger?.LogDebug("Updated session {SessionId}", session.SessionId);
    }

    /// <summary>
    /// Add a message to session
    /// </summary>
    public async Task AddMessageAsync(
        string sessionId,
        string role,
        string content,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var session = GetSession(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        var message = new SessionMessage
        {
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        session.Messages.Add(message);

        // Trim messages if max is exceeded
        if (session.Configuration.MaxMessages > 0 &&
            session.Messages.Count > session.Configuration.MaxMessages)
        {
            var toRemove = session.Messages.Count - session.Configuration.MaxMessages;
            session.Messages.RemoveRange(0, toRemove);
            _logger?.LogDebug("Trimmed {Count} messages from session {SessionId}", toRemove, sessionId);
        }

        await UpdateSessionAsync(session, cancellationToken);
    }

    /// <summary>
    /// Pause a session
    /// </summary>
    public async Task PauseSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = GetSession(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        session.Status = SessionStatus.Paused;
        await UpdateSessionAsync(session, cancellationToken);

        _logger?.LogInformation("Paused session {SessionId}", sessionId);
    }

    /// <summary>
    /// Complete a session
    /// </summary>
    public async Task CompleteSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = GetSession(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        session.Status = SessionStatus.Completed;
        await UpdateSessionAsync(session, cancellationToken);

        // Remove from active sessions
        _activeSessions.TryRemove(sessionId, out _);

        _logger?.LogInformation("Completed session {SessionId}", sessionId);
    }

    /// <summary>
    /// Terminate a session
    /// </summary>
    public async Task TerminateSessionAsync(
        string sessionId,
        bool deleteFromStorage = false,
        CancellationToken cancellationToken = default)
    {
        var session = GetSession(sessionId);
        if (session == null)
        {
            _logger?.LogWarning("Session {SessionId} not found for termination", sessionId);
            return;
        }

        session.Status = SessionStatus.Terminated;

        if (deleteFromStorage)
        {
            await _storage.DeleteSessionAsync(sessionId, cancellationToken);
            _logger?.LogInformation("Deleted session {SessionId} from storage", sessionId);
        }
        else
        {
            await _storage.SaveSessionAsync(session, cancellationToken);
        }

        _activeSessions.TryRemove(sessionId, out _);

        _logger?.LogInformation("Terminated session {SessionId}", sessionId);
    }

    /// <summary>
    /// List all active sessions
    /// </summary>
    public List<Session> GetActiveSessions()
    {
        return _activeSessions.Values.ToList();
    }

    /// <summary>
    /// List sessions from storage
    /// </summary>
    public async Task<List<Session>> ListSessionsAsync(
        string? agentName = null,
        string? userId = null,
        SessionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        return await _storage.ListSessionsAsync(agentName, userId, status, cancellationToken);
    }

    /// <summary>
    /// Cleanup expired sessions
    /// </summary>
    public async Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        // Cleanup active sessions
        var expiredActive = _activeSessions.Values
            .Where(s => s.IsExpired())
            .ToList();

        foreach (var session in expiredActive)
        {
            session.Status = SessionStatus.Expired;
            await _storage.SaveSessionAsync(session, cancellationToken);
            _activeSessions.TryRemove(session.SessionId, out _);
        }

        // Cleanup storage
        var expiredCount = await _storage.CleanupExpiredSessionsAsync(cancellationToken);

        var totalExpired = expiredActive.Count + expiredCount;
        if (totalExpired > 0)
        {
            _logger?.LogInformation("Cleaned up {Count} expired sessions", totalExpired);
        }

        return totalExpired;
    }

    /// <summary>
    /// Auto-save all active sessions
    /// </summary>
    private async Task AutoSaveSessionsAsync()
    {
        if (_disposed) return;

        var sessionsToSave = _activeSessions.Values
            .Where(s => s.Configuration.PersistToStorage &&
                       s.Configuration.AutoSaveIntervalSeconds > 0)
            .ToList();

        foreach (var session in sessionsToSave)
        {
            try
            {
                await _storage.SaveSessionAsync(session);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error auto-saving session {SessionId}", session.SessionId);
            }
        }

        if (sessionsToSave.Any())
        {
            _logger?.LogDebug("Auto-saved {Count} sessions", sessionsToSave.Count);
        }
    }

    /// <summary>
    /// Get session statistics
    /// </summary>
    public async Task<SessionStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var totalSessions = await _storage.GetSessionCountAsync(cancellationToken);

        return new SessionStatistics
        {
            ActiveSessions = _activeSessions.Count,
            TotalSessions = totalSessions,
            SessionsByStatus = _activeSessions.Values
                .GroupBy(s => s.Status)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;

        // Stop timers
        if (_cleanupTimer != null)
        {
            await _cleanupTimer.DisposeAsync();
        }

        if (_autoSaveTimer != null)
        {
            await _autoSaveTimer.DisposeAsync();
        }

        // Save all active sessions
        foreach (var session in _activeSessions.Values)
        {
            if (session.Configuration.PersistToStorage)
            {
                try
                {
                    await _storage.SaveSessionAsync(session);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error saving session {SessionId} during disposal", session.SessionId);
                }
            }
        }

        _logger?.LogInformation("SessionManager disposed");
    }
}

/// <summary>
/// Session statistics
/// </summary>
public class SessionStatistics
{
    public int ActiveSessions { get; set; }
    public int TotalSessions { get; set; }
    public Dictionary<SessionStatus, int> SessionsByStatus { get; set; } = new();
}
