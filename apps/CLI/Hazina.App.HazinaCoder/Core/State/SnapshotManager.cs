using System.Text.Json;
using System.IO.Compression;
using Hazina.App.HazinaCoder.Core.Events;

namespace Hazina.App.HazinaCoder.Core.State;

/// <summary>
/// Snapshot/Restore system for instant context switching
/// Save complete agent state and restore instantly
/// </summary>
public class SnapshotManager
{
    private readonly string _snapshotDirectory;
    private readonly IEventBus? _eventBus;

    public SnapshotManager(string? snapshotDirectory = null, IEventBus? eventBus = null)
    {
        _snapshotDirectory = snapshotDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".hazinacoder",
            "snapshots"
        );
        _eventBus = eventBus;

        Directory.CreateDirectory(_snapshotDirectory);
    }

    /// <summary>
    /// Save current state to snapshot
    /// </summary>
    public async Task<Snapshot> SaveSnapshotAsync(
        string name,
        AgentState state,
        bool compress = true)
    {
        var snapshotId = Guid.NewGuid().ToString("N");
        var timestamp = DateTime.UtcNow;

        var snapshot = new Snapshot
        {
            Id = snapshotId,
            Name = name,
            Created = timestamp,
            State = state,
            Compressed = compress
        };

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var fileName = $"{snapshotId}_{SanitizeFileName(name)}.json";

        if (compress)
        {
            fileName += ".gz";
            var filePath = Path.Combine(_snapshotDirectory, fileName);
            await using var fileStream = File.Create(filePath);
            await using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
            await using var writer = new StreamWriter(gzipStream);
            await writer.WriteAsync(json);
        }
        else
        {
            var filePath = Path.Combine(_snapshotDirectory, fileName);
            await File.WriteAllTextAsync(filePath, json);
        }

        var fileInfo = new FileInfo(Path.Combine(_snapshotDirectory, fileName));

        _eventBus?.Publish(new SnapshotSavedEvent(snapshotId, name, fileInfo.Length));

        return snapshot;
    }

    /// <summary>
    /// Restore state from snapshot
    /// </summary>
    public async Task<AgentState> RestoreSnapshotAsync(string snapshotId)
    {
        var files = Directory.GetFiles(_snapshotDirectory, $"{snapshotId}*.json*");

        if (!files.Any())
        {
            throw new FileNotFoundException($"Snapshot not found: {snapshotId}");
        }

        var filePath = files.First();
        var compressed = filePath.EndsWith(".gz");

        string json;

        if (compressed)
        {
            await using var fileStream = File.OpenRead(filePath);
            await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);
            json = await reader.ReadToEndAsync();
        }
        else
        {
            json = await File.ReadAllTextAsync(filePath);
        }

        var snapshot = JsonSerializer.Deserialize<Snapshot>(json);

        if (snapshot == null || snapshot.State == null)
        {
            throw new InvalidOperationException("Invalid snapshot data");
        }

        _eventBus?.Publish(new SnapshotRestoredEvent(snapshot.Id, snapshot.Name));

        return snapshot.State;
    }

    /// <summary>
    /// List all snapshots
    /// </summary>
    public List<SnapshotInfo> ListSnapshots()
    {
        var snapshots = new List<SnapshotInfo>();

        var files = Directory.GetFiles(_snapshotDirectory, "*.json*");

        foreach (var file in files)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.EndsWith(".json"))
                {
                    fileName = Path.GetFileNameWithoutExtension(fileName);
                }

                var parts = fileName.Split('_', 2);
                if (parts.Length == 2)
                {
                    snapshots.Add(new SnapshotInfo
                    {
                        Id = parts[0],
                        Name = parts[1].Replace('_', ' '),
                        FilePath = file,
                        SizeBytes = fileInfo.Length,
                        Created = fileInfo.CreationTimeUtc,
                        LastAccessed = fileInfo.LastAccessTimeUtc
                    });
                }
            }
            catch
            {
                // Skip invalid files
            }
        }

        return snapshots.OrderByDescending(s => s.Created).ToList();
    }

    /// <summary>
    /// Delete snapshot
    /// </summary>
    public void DeleteSnapshot(string snapshotId)
    {
        var files = Directory.GetFiles(_snapshotDirectory, $"{snapshotId}*.json*");

        foreach (var file in files)
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// Export snapshot to file
    /// </summary>
    public async Task ExportSnapshotAsync(string snapshotId, string exportPath)
    {
        var files = Directory.GetFiles(_snapshotDirectory, $"{snapshotId}*.json*");

        if (!files.Any())
        {
            throw new FileNotFoundException($"Snapshot not found: {snapshotId}");
        }

        var sourceFile = files.First();
        await using var source = File.OpenRead(sourceFile);
        await using var destination = File.Create(exportPath);
        await source.CopyToAsync(destination);
    }

    /// <summary>
    /// Import snapshot from file
    /// </summary>
    public async Task<string> ImportSnapshotAsync(string importPath)
    {
        var newId = Guid.NewGuid().ToString("N");
        var fileName = Path.GetFileName(importPath);
        var newFileName = $"{newId}_{fileName}";
        var destPath = Path.Combine(_snapshotDirectory, newFileName);

        await using var source = File.OpenRead(importPath);
        await using var destination = File.Create(destPath);
        await source.CopyToAsync(destination);

        return newId;
    }

    /// <summary>
    /// Sanitize file name
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 50 ? sanitized[..50] : sanitized;
    }
}

/// <summary>
/// Complete agent state snapshot
/// </summary>
public class Snapshot
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public bool Compressed { get; set; }
    public AgentState? State { get; set; }
}

/// <summary>
/// Agent state to be saved/restored
/// </summary>
public class AgentState
{
    // Context
    public string WorkingDirectory { get; set; } = string.Empty;
    public List<string> LoadedFiles { get; set; } = new();
    public Dictionary<string, string> FileContents { get; set; } = new();

    // Conversation history
    public List<ConversationMessage> ConversationHistory { get; set; } = new();

    // Memory/summaries
    public List<string> MemorySummaries { get; set; } = new();

    // Active goals/tasks
    public List<string> ActiveGoals { get; set; } = new();

    // Configuration
    public Dictionary<string, string> Configuration { get; set; } = new();

    // Metadata
    public DateTime LastActivity { get; set; }
    public int IterationCount { get; set; }
    public string CurrentTask { get; set; } = string.Empty;
}

/// <summary>
/// Conversation message
/// </summary>
public class ConversationMessage
{
    public string Role { get; set; } = string.Empty; // user, assistant, system
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Snapshot info for listing
/// </summary>
public class SnapshotInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastAccessed { get; set; }

    public string SizeFormatted => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{SizeBytes / (1024.0 * 1024.0 * 1024.0):F1} GB"
    };

    public string CreatedAgo
    {
        get
        {
            var span = DateTime.UtcNow - Created;
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 30) return $"{(int)span.TotalDays}d ago";
            return Created.ToString("yyyy-MM-dd");
        }
    }
}
