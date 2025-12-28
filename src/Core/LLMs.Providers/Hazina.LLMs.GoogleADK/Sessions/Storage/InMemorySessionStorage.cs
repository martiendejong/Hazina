using System.Collections.Concurrent;
using Hazina.LLMs.GoogleADK.Sessions.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Sessions.Storage;

/// <summary>
/// In-memory session storage (non-persistent, for development/testing)
/// </summary>
public class InMemorySessionStorage : ISessionStorage
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly ILogger? _logger;

    public InMemorySessionStorage(ILogger? logger = null)
    {
        _logger = logger;
    }

    public Task SaveSessionAsync(Session session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Deep clone to prevent external modifications
        var clone = CloneSession(session);
        _sessions[session.SessionId] = clone;

        _logger?.LogDebug("Saved session {SessionId} to in-memory storage", session.SessionId);
        return Task.CompletedTask;
    }

    public Task<Session?> LoadSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_sessions.TryGetValue(sessionId, out var session))
        {
            _logger?.LogDebug("Loaded session {SessionId} from in-memory storage", sessionId);
            return Task.FromResult<Session?>(CloneSession(session));
        }

        _logger?.LogDebug("Session {SessionId} not found in in-memory storage", sessionId);
        return Task.FromResult<Session?>(null);
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _sessions.TryRemove(sessionId, out _);
        _logger?.LogDebug("Deleted session {SessionId} from in-memory storage", sessionId);
        return Task.CompletedTask;
    }

    public Task<bool> SessionExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_sessions.ContainsKey(sessionId));
    }

    public Task<List<Session>> ListSessionsAsync(
        string? agentName = null,
        string? userId = null,
        SessionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = _sessions.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(agentName))
        {
            query = query.Where(s => s.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(s => s.UserId?.Equals(userId, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        var results = query.Select(CloneSession).ToList();
        return Task.FromResult(results);
    }

    public Task<List<Session>> GetSessionsByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var results = _sessions.Values
            .Where(s => s.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .Select(CloneSession)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var expiredIds = _sessions.Values
            .Where(s => s.IsExpired())
            .Select(s => s.SessionId)
            .ToList();

        foreach (var id in expiredIds)
        {
            _sessions.TryRemove(id, out _);
        }

        if (expiredIds.Any())
        {
            _logger?.LogInformation("Cleaned up {Count} expired sessions", expiredIds.Count);
        }

        return Task.FromResult(expiredIds.Count);
    }

    public Task<int> GetSessionCountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_sessions.Count);
    }

    /// <summary>
    /// Clear all sessions (for testing)
    /// </summary>
    public void Clear()
    {
        _sessions.Clear();
        _logger?.LogDebug("Cleared all sessions from in-memory storage");
    }

    private Session CloneSession(Session session)
    {
        return new Session
        {
            SessionId = session.SessionId,
            AgentName = session.AgentName,
            Status = session.Status,
            CreatedAt = session.CreatedAt,
            LastActiveAt = session.LastActiveAt,
            ExpiresAt = session.ExpiresAt,
            Metadata = new Dictionary<string, object>(session.Metadata),
            State = new Dictionary<string, object>(session.State),
            Messages = session.Messages.Select(m => new SessionMessage
            {
                Role = m.Role,
                Content = m.Content,
                Timestamp = m.Timestamp,
                Metadata = new Dictionary<string, object>(m.Metadata)
            }).ToList(),
            Configuration = new SessionConfiguration
            {
                MaxMessages = session.Configuration.MaxMessages,
                TimeoutMinutes = session.Configuration.TimeoutMinutes,
                PersistToStorage = session.Configuration.PersistToStorage,
                AutoSaveIntervalSeconds = session.Configuration.AutoSaveIntervalSeconds,
                EnableRecovery = session.Configuration.EnableRecovery
            },
            Tags = new List<string>(session.Tags),
            UserId = session.UserId
        };
    }
}
