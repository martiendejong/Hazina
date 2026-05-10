using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Hazina.AgenticOrchestration.Services;

/// <summary>
/// Session persistence service - saves terminal state across restarts
/// Pattern: VibeTunnel session management
/// </summary>
public interface ISessionPersistence
{
    Task SaveSessionAsync(string sessionId, SessionMetadata metadata);
    Task AppendOutputAsync(string sessionId, string output);
    Task<SessionMetadata?> LoadSessionAsync(string sessionId);
    Task<string> GetTranscriptAsync(string sessionId);
    Task ArchiveSessionAsync(string sessionId);
    Task<IEnumerable<SessionMetadata>> GetActiveSessionsAsync();
    Task DeleteSessionFilesAsync(string sessionId);
}

public class SessionMetadata
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActive { get; set; }
    public string Command { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public TerminalDimensions Dimensions { get; set; } = new();
    public SessionState State { get; set; }
    public int DisplayOrder { get; set; }
}

public class TerminalDimensions
{
    public int Cols { get; set; }
    public int Rows { get; set; }
}

public enum SessionState
{
    Active,
    Suspended,
    Completed,
    Recoverable
}

public class SessionPersistence : ISessionPersistence
{
    private const string ActiveDirectory = "active";
    private const string ArchiveDirectory = "archive";

    private readonly string _baseDirectory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sessionLocks = new();

    public SessionPersistence(IConfiguration configuration)
    {
        var configuredPath = configuration["AgenticOrchestration:SessionLogging:BasePath"];
        _baseDirectory = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HazinaOrchestration", "sessions");
        EnsureDirectories();
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_baseDirectory, ActiveDirectory));
        Directory.CreateDirectory(Path.Combine(_baseDirectory, ArchiveDirectory));
    }

    public async Task SaveSessionAsync(string sessionId, SessionMetadata metadata)
    {
        var lockObj = _sessionLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync();

        try
        {
            var metadataPath = GetMetadataPath(sessionId);
            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(metadataPath, json);
        }
        finally
        {
            lockObj.Release();
        }
    }

    public async Task AppendOutputAsync(string sessionId, string output)
    {
        var lockObj = _sessionLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync();

        try
        {
            var transcriptPath = GetTranscriptPath(sessionId);
            await File.AppendAllTextAsync(transcriptPath, output);

            // Also append to asciinema format
            var asciinemaPath = GetAsciinemaPath(sessionId);
            var timestamp = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
            var asciinemaLine = $"[{timestamp:F6}, \"o\", {JsonSerializer.Serialize(output)}]\n";
            await File.AppendAllTextAsync(asciinemaPath, asciinemaLine);
        }
        finally
        {
            lockObj.Release();
        }
    }

    public async Task<SessionMetadata?> LoadSessionAsync(string sessionId)
    {
        var metadataPath = GetMetadataPath(sessionId);

        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(metadataPath);
        return JsonSerializer.Deserialize<SessionMetadata>(json);
    }

    public async Task<string> GetTranscriptAsync(string sessionId)
    {
        var transcriptPath = GetTranscriptPath(sessionId);

        if (!File.Exists(transcriptPath))
        {
            return string.Empty;
        }

        return await File.ReadAllTextAsync(transcriptPath);
    }

    public async Task ArchiveSessionAsync(string sessionId)
    {
        var archivePath = GetArchivePath(sessionId);
        Directory.CreateDirectory(archivePath);

        var files = new[]
        {
            (GetMetadataPath(sessionId), Path.Combine(archivePath, $"{sessionId}.session.json")),
            (GetTranscriptPath(sessionId), Path.Combine(archivePath, $"{sessionId}.transcript.txt")),
            (GetAsciinemaPath(sessionId), Path.Combine(archivePath, $"{sessionId}.asciinema"))
        };

        foreach (var (source, dest) in files)
        {
            if (File.Exists(source))
            {
                File.Move(source, dest, overwrite: true);
            }
        }

        // Update metadata state
        var metadata = await LoadSessionAsync(sessionId);
        if (metadata != null)
        {
            metadata.State = SessionState.Completed;
            await SaveSessionAsync(sessionId, metadata);
        }
    }

    public async Task<IEnumerable<SessionMetadata>> GetActiveSessionsAsync()
    {
        var activePath = Path.Combine(_baseDirectory, ActiveDirectory);
        var metadataFiles = Directory.GetFiles(activePath, "*.session.json");

        var sessions = new List<SessionMetadata>();

        foreach (var file in metadataFiles)
        {
            var json = await File.ReadAllTextAsync(file);
            var metadata = JsonSerializer.Deserialize<SessionMetadata>(json);
            if (metadata != null)
            {
                sessions.Add(metadata);
            }
        }

        return sessions;
    }

    public Task DeleteSessionFilesAsync(string sessionId)
    {
        var files = new[]
        {
            GetMetadataPath(sessionId),
            GetTranscriptPath(sessionId),
            GetAsciinemaPath(sessionId)
        };

        foreach (var file in files)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        _sessionLocks.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    private string GetMetadataPath(string sessionId) =>
        Path.Combine(_baseDirectory, ActiveDirectory, $"{sessionId}.session.json");

    private string GetTranscriptPath(string sessionId) =>
        Path.Combine(_baseDirectory, ActiveDirectory, $"{sessionId}.transcript.txt");

    private string GetAsciinemaPath(string sessionId) =>
        Path.Combine(_baseDirectory, ActiveDirectory, $"{sessionId}.asciinema");

    private string GetArchivePath(string sessionId)
    {
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return Path.Combine(_baseDirectory, ArchiveDirectory, date, sessionId);
    }
}
