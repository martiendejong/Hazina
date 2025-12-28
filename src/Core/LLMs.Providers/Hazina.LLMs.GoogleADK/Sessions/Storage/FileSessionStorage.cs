using System.Text.Json;
using Hazina.LLMs.GoogleADK.Sessions.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Sessions.Storage;

/// <summary>
/// File-based session storage
/// </summary>
public class FileSessionStorage : ISessionStorage
{
    private readonly string _storageDirectory;
    private readonly ILogger? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public FileSessionStorage(string storageDirectory, ILogger? logger = null)
    {
        _storageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory));
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Create storage directory if it doesn't exist
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
            _logger?.LogInformation("Created session storage directory: {Directory}", _storageDirectory);
        }
    }

    public async Task SaveSessionAsync(Session session, CancellationToken cancellationToken = default)
    {
        var filePath = GetSessionFilePath(session.SessionId);

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(session, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger?.LogDebug("Saved session {SessionId} to file: {FilePath}", session.SessionId, filePath);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<Session?> LoadSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var filePath = GetSessionFilePath(sessionId);

        if (!File.Exists(filePath))
        {
            _logger?.LogDebug("Session file not found: {FilePath}", filePath);
            return null;
        }

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var session = JsonSerializer.Deserialize<Session>(json, _jsonOptions);

            _logger?.LogDebug("Loaded session {SessionId} from file: {FilePath}", sessionId, filePath);
            return session;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading session {SessionId} from file", sessionId);
            return null;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var filePath = GetSessionFilePath(sessionId);

        if (File.Exists(filePath))
        {
            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                File.Delete(filePath);
                _logger?.LogDebug("Deleted session file: {FilePath}", filePath);
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }

    public Task<bool> SessionExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var filePath = GetSessionFilePath(sessionId);
        return Task.FromResult(File.Exists(filePath));
    }

    public async Task<List<Session>> ListSessionsAsync(
        string? agentName = null,
        string? userId = null,
        SessionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var sessions = new List<Session>();
        var files = Directory.GetFiles(_storageDirectory, "*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var session = JsonSerializer.Deserialize<Session>(json, _jsonOptions);

                if (session == null) continue;

                // Apply filters
                if (!string.IsNullOrEmpty(agentName) &&
                    !session.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(userId) &&
                    !session.UserId?.Equals(userId, StringComparison.OrdinalIgnoreCase) == true)
                    continue;

                if (status.HasValue && session.Status != status.Value)
                    continue;

                sessions.Add(session);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error loading session from file: {File}", file);
            }
        }

        return sessions;
    }

    public async Task<List<Session>> GetSessionsByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        var allSessions = await ListSessionsAsync(cancellationToken: cancellationToken);
        return allSessions
            .Where(s => s.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await ListSessionsAsync(cancellationToken: cancellationToken);
        var expiredSessions = sessions.Where(s => s.IsExpired()).ToList();

        foreach (var session in expiredSessions)
        {
            await DeleteSessionAsync(session.SessionId, cancellationToken);
        }

        if (expiredSessions.Any())
        {
            _logger?.LogInformation("Cleaned up {Count} expired session files", expiredSessions.Count);
        }

        return expiredSessions.Count;
    }

    public Task<int> GetSessionCountAsync(CancellationToken cancellationToken = default)
    {
        var count = Directory.GetFiles(_storageDirectory, "*.json").Length;
        return Task.FromResult(count);
    }

    private string GetSessionFilePath(string sessionId)
    {
        var fileName = $"{sessionId}.json";
        return Path.Combine(_storageDirectory, fileName);
    }
}
