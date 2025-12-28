using Hazina.LLMs.GoogleADK.Sessions.Models;

namespace Hazina.LLMs.GoogleADK.Sessions.Storage;

/// <summary>
/// Interface for session storage providers
/// </summary>
public interface ISessionStorage
{
    /// <summary>
    /// Save a session to storage
    /// </summary>
    Task SaveSessionAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load a session from storage
    /// </summary>
    Task<Session?> LoadSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a session from storage
    /// </summary>
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a session exists
    /// </summary>
    Task<bool> SessionExistsAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all sessions (optionally filtered)
    /// </summary>
    Task<List<Session>> ListSessionsAsync(
        string? agentName = null,
        string? userId = null,
        SessionStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get sessions by tag
    /// </summary>
    Task<List<Session>> GetSessionsByTagAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up expired sessions
    /// </summary>
    Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get session count
    /// </summary>
    Task<int> GetSessionCountAsync(CancellationToken cancellationToken = default);
}
