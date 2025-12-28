using Hazina.LLMs.GoogleADK.Sessions.Models;
using Hazina.LLMs.GoogleADK.Sessions.Storage;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Sessions;

/// <summary>
/// Service for recovering sessions after failures or restarts
/// </summary>
public class SessionRecoveryService
{
    private readonly ISessionStorage _storage;
    private readonly ILogger? _logger;

    public SessionRecoveryService(ISessionStorage storage, ILogger? logger = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _logger = logger;
    }

    /// <summary>
    /// Recover all sessions for an agent
    /// </summary>
    public async Task<List<Session>> RecoverAgentSessionsAsync(
        string agentName,
        bool onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Recovering sessions for agent: {AgentName}", agentName);

        var sessions = await _storage.ListSessionsAsync(
            agentName: agentName,
            cancellationToken: cancellationToken
        );

        if (onlyActive)
        {
            sessions = sessions
                .Where(s => s.Status == SessionStatus.Active || s.Status == SessionStatus.Paused)
                .Where(s => !s.IsExpired())
                .ToList();
        }

        _logger?.LogInformation("Recovered {Count} sessions for agent {AgentName}", sessions.Count, agentName);
        return sessions;
    }

    /// <summary>
    /// Recover user sessions
    /// </summary>
    public async Task<List<Session>> RecoverUserSessionsAsync(
        string userId,
        bool onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Recovering sessions for user: {UserId}", userId);

        var sessions = await _storage.ListSessionsAsync(
            userId: userId,
            cancellationToken: cancellationToken
        );

        if (onlyActive)
        {
            sessions = sessions
                .Where(s => s.Status == SessionStatus.Active || s.Status == SessionStatus.Paused)
                .Where(s => !s.IsExpired())
                .ToList();
        }

        _logger?.LogInformation("Recovered {Count} sessions for user {UserId}", sessions.Count, userId);
        return sessions;
    }

    /// <summary>
    /// Attempt to recover a specific session with validation
    /// </summary>
    public async Task<SessionRecoveryResult> RecoverSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var result = new SessionRecoveryResult { SessionId = sessionId };

        try
        {
            var session = await _storage.LoadSessionAsync(sessionId, cancellationToken);

            if (session == null)
            {
                result.Success = false;
                result.FailureReason = "Session not found";
                _logger?.LogWarning("Session {SessionId} not found for recovery", sessionId);
                return result;
            }

            // Check if expired
            if (session.IsExpired())
            {
                result.Success = false;
                result.FailureReason = "Session expired";
                result.Session = session;
                _logger?.LogWarning("Session {SessionId} is expired", sessionId);
                return result;
            }

            // Validate session data
            var validation = ValidateSession(session);
            if (!validation.IsValid)
            {
                result.Success = false;
                result.FailureReason = $"Session validation failed: {string.Join(", ", validation.Errors)}";
                result.Session = session;
                _logger?.LogWarning("Session {SessionId} validation failed", sessionId);
                return result;
            }

            result.Success = true;
            result.Session = session;
            _logger?.LogInformation("Successfully recovered session {SessionId}", sessionId);
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.FailureReason = $"Exception: {ex.Message}";
            _logger?.LogError(ex, "Error recovering session {SessionId}", sessionId);
            return result;
        }
    }

    /// <summary>
    /// Validate session data integrity
    /// </summary>
    public SessionValidationResult ValidateSession(Session session)
    {
        var result = new SessionValidationResult();

        if (string.IsNullOrEmpty(session.SessionId))
        {
            result.Errors.Add("SessionId is empty");
        }

        if (string.IsNullOrEmpty(session.AgentName))
        {
            result.Errors.Add("AgentName is empty");
        }

        if (session.CreatedAt > DateTime.UtcNow)
        {
            result.Errors.Add("CreatedAt is in the future");
        }

        if (session.LastActiveAt < session.CreatedAt)
        {
            result.Errors.Add("LastActiveAt is before CreatedAt");
        }

        if (session.ExpiresAt.HasValue && session.ExpiresAt.Value < session.CreatedAt)
        {
            result.Errors.Add("ExpiresAt is before CreatedAt");
        }

        // Validate messages
        foreach (var message in session.Messages)
        {
            if (string.IsNullOrEmpty(message.Role))
            {
                result.Warnings.Add("Message has empty role");
            }

            if (message.Timestamp > DateTime.UtcNow)
            {
                result.Warnings.Add("Message timestamp is in the future");
            }
        }

        return result;
    }

    /// <summary>
    /// Migrate session to new format (for version upgrades)
    /// </summary>
    public Task<Session> MigrateSessionAsync(
        Session session,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation(
            "Migrating session {SessionId} from version {FromVersion} to {ToVersion}",
            session.SessionId,
            fromVersion,
            toVersion
        );

        // Implement migration logic here as needed
        // For now, just return the session as-is

        return Task.FromResult(session);
    }

    /// <summary>
    /// Create a backup of a session
    /// </summary>
    public async Task<string> BackupSessionAsync(
        Session session,
        string backupDirectory,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var backupFileName = $"{session.SessionId}_{timestamp}_backup.json";
        var backupPath = Path.Combine(backupDirectory, backupFileName);

        if (!Directory.Exists(backupDirectory))
        {
            Directory.CreateDirectory(backupDirectory);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(session, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(backupPath, json, cancellationToken);

        _logger?.LogInformation("Backed up session {SessionId} to {BackupPath}", session.SessionId, backupPath);
        return backupPath;
    }
}

/// <summary>
/// Result of session recovery attempt
/// </summary>
public class SessionRecoveryResult
{
    public string SessionId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public Session? Session { get; set; }
}

/// <summary>
/// Result of session validation
/// </summary>
public class SessionValidationResult
{
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool IsValid => !Errors.Any();
}
