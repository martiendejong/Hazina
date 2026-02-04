using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hazina.App.HazinaCoder.Core.State.HSM;

/// <summary>
/// Checkpoint manager - fast save/restore for interruption recovery
/// Creates named snapshots that can be restored instantly
/// Useful for: interruption recovery, risky operations, experimentation
/// </summary>
public sealed class CheckpointManager
{
    private readonly string _checkpointDirectory;
    private readonly ConcurrentDictionary<string, CheckpointMetadata> _checkpoints = new();
    private readonly int _maxCheckpoints;

    public CheckpointManager(string checkpointDirectory, int maxCheckpoints = 100)
    {
        _checkpointDirectory = checkpointDirectory;
        _maxCheckpoints = maxCheckpoints;
        Directory.CreateDirectory(checkpointDirectory);

        // Load existing checkpoint metadata
        LoadCheckpointMetadata();
    }

    /// <summary>
    /// Create checkpoint with optional tag
    /// Returns: checkpoint ID for later restoration
    /// </summary>
    public async Task<string> CreateCheckpointAsync(
        ImmutableStateSnapshot state,
        string? tag = null,
        string? description = null)
    {
        var checkpointId = GenerateCheckpointId(tag);
        var metadata = new CheckpointMetadata
        {
            CheckpointId = checkpointId,
            Tag = tag,
            Description = description,
            StateId = state.Id,
            CreatedAt = DateTime.UtcNow,
            SessionId = state.Session.SessionId
        };

        // Save state snapshot
        var checkpointPath = GetCheckpointPath(checkpointId);
        await SaveCheckpointAsync(checkpointPath, state);

        // Save metadata
        _checkpoints[checkpointId] = metadata;
        await SaveMetadataAsync();

        // Enforce max checkpoints (LRU eviction)
        await EnforceMaxCheckpointsAsync();

        return checkpointId;
    }

    /// <summary>
    /// Create checkpoint before risky operation
    /// </summary>
    public async Task<string> CreateBeforeRiskyOperationAsync(
        ImmutableStateSnapshot state,
        string operationName)
    {
        return await CreateCheckpointAsync(
            state,
            tag: $"before-{operationName}",
            description: $"Checkpoint before risky operation: {operationName}"
        );
    }

    /// <summary>
    /// Restore checkpoint by ID or tag
    /// </summary>
    public async Task<ImmutableStateSnapshot?> RestoreCheckpointAsync(string checkpointIdOrTag)
    {
        // Try direct ID lookup
        if (_checkpoints.TryGetValue(checkpointIdOrTag, out var metadata))
        {
            return await LoadCheckpointAsync(checkpointIdOrTag);
        }

        // Try tag lookup
        var checkpointByTag = _checkpoints.Values
            .FirstOrDefault(c => c.Tag == checkpointIdOrTag);

        if (checkpointByTag != null)
        {
            return await LoadCheckpointAsync(checkpointByTag.CheckpointId);
        }

        return null;
    }

    /// <summary>
    /// Restore most recent checkpoint
    /// </summary>
    public async Task<ImmutableStateSnapshot?> RestoreMostRecentAsync()
    {
        var mostRecent = _checkpoints.Values
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();

        if (mostRecent == null)
            return null;

        return await LoadCheckpointAsync(mostRecent.CheckpointId);
    }

    /// <summary>
    /// Restore checkpoint from specific time range
    /// </summary>
    public async Task<ImmutableStateSnapshot?> RestoreFromTimeRangeAsync(
        DateTime start,
        DateTime end)
    {
        var checkpoint = _checkpoints.Values
            .Where(c => c.CreatedAt >= start && c.CreatedAt <= end)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();

        if (checkpoint == null)
            return null;

        return await LoadCheckpointAsync(checkpoint.CheckpointId);
    }

    /// <summary>
    /// List all available checkpoints
    /// </summary>
    public IEnumerable<CheckpointMetadata> ListCheckpoints()
    {
        return _checkpoints.Values
            .OrderByDescending(c => c.CreatedAt);
    }

    /// <summary>
    /// Get checkpoint by tag
    /// </summary>
    public CheckpointMetadata? GetCheckpointByTag(string tag)
    {
        return _checkpoints.Values
            .FirstOrDefault(c => c.Tag == tag);
    }

    /// <summary>
    /// Delete checkpoint
    /// </summary>
    public async Task DeleteCheckpointAsync(string checkpointId)
    {
        if (_checkpoints.TryRemove(checkpointId, out _))
        {
            var checkpointPath = GetCheckpointPath(checkpointId);
            if (File.Exists(checkpointPath))
            {
                File.Delete(checkpointPath);
            }

            await SaveMetadataAsync();
        }
    }

    /// <summary>
    /// Delete all checkpoints older than specified duration
    /// </summary>
    public async Task<int> DeleteOlderThanAsync(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var toDelete = _checkpoints.Values
            .Where(c => c.CreatedAt < cutoff)
            .Select(c => c.CheckpointId)
            .ToList();

        foreach (var checkpointId in toDelete)
        {
            await DeleteCheckpointAsync(checkpointId);
        }

        return toDelete.Count;
    }

    /// <summary>
    /// Get total disk space used by checkpoints
    /// </summary>
    public long GetTotalSizeBytes()
    {
        var files = Directory.GetFiles(_checkpointDirectory, "checkpoint_*.gz");
        return files.Sum(f => new FileInfo(f).Length);
    }

    /// <summary>
    /// Get checkpoint statistics
    /// </summary>
    public CheckpointStats GetStats()
    {
        return new CheckpointStats
        {
            TotalCheckpoints = _checkpoints.Count,
            OldestCheckpoint = _checkpoints.Values.MinBy(c => c.CreatedAt)?.CreatedAt,
            NewestCheckpoint = _checkpoints.Values.MaxBy(c => c.CreatedAt)?.CreatedAt,
            TotalSizeBytes = GetTotalSizeBytes(),
            CheckpointsWithTags = _checkpoints.Values.Count(c => !string.IsNullOrEmpty(c.Tag))
        };
    }

    /// <summary>
    /// Auto-checkpoint at regular intervals
    /// </summary>
    public async Task StartAutoCheckpointAsync(
        Func<ImmutableStateSnapshot> getCurrentState,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken);

            try
            {
                var currentState = getCurrentState();
                await CreateCheckpointAsync(
                    currentState,
                    tag: "auto",
                    description: $"Auto-checkpoint at {DateTime.UtcNow:HH:mm:ss}"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Auto-checkpoint failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Generate checkpoint ID with optional tag
    /// </summary>
    private string GenerateCheckpointId(string? tag)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var tagPart = !string.IsNullOrEmpty(tag) ? $"-{tag}" : "";
        return $"checkpoint-{timestamp}{tagPart}";
    }

    /// <summary>
    /// Get file path for checkpoint
    /// </summary>
    private string GetCheckpointPath(string checkpointId)
    {
        return Path.Combine(_checkpointDirectory, $"{checkpointId}.gz");
    }

    /// <summary>
    /// Save checkpoint to disk (compressed)
    /// </summary>
    private async Task SaveCheckpointAsync(string path, ImmutableStateSnapshot state)
    {
        var json = state.ToJson();
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        // Compress with GZip
        await using var fileStream = File.Create(path);
        await using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
        await gzipStream.WriteAsync(bytes);
    }

    /// <summary>
    /// Load checkpoint from disk (decompress)
    /// </summary>
    private async Task<ImmutableStateSnapshot> LoadCheckpointAsync(string checkpointId)
    {
        var path = GetCheckpointPath(checkpointId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Checkpoint {checkpointId} not found");
        }

        // Decompress GZip
        await using var fileStream = File.OpenRead(path);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);
        var json = await reader.ReadToEndAsync();

        return ImmutableStateSnapshot.FromJson(json);
    }

    /// <summary>
    /// Save checkpoint metadata to disk
    /// </summary>
    private async Task SaveMetadataAsync()
    {
        var metadataPath = Path.Combine(_checkpointDirectory, "checkpoints.json");
        var json = JsonSerializer.Serialize(_checkpoints, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(metadataPath, json);
    }

    /// <summary>
    /// Load checkpoint metadata from disk
    /// </summary>
    private void LoadCheckpointMetadata()
    {
        var metadataPath = Path.Combine(_checkpointDirectory, "checkpoints.json");
        if (!File.Exists(metadataPath))
            return;

        try
        {
            var json = File.ReadAllText(metadataPath);
            var loaded = JsonSerializer.Deserialize<ConcurrentDictionary<string, CheckpointMetadata>>(json);
            if (loaded != null)
            {
                foreach (var kvp in loaded)
                {
                    _checkpoints[kvp.Key] = kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load checkpoint metadata: {ex.Message}");
        }
    }

    /// <summary>
    /// Enforce maximum number of checkpoints (LRU eviction)
    /// </summary>
    private async Task EnforceMaxCheckpointsAsync()
    {
        if (_checkpoints.Count <= _maxCheckpoints)
            return;

        // Delete oldest checkpoints (excluding tagged ones)
        var toDelete = _checkpoints.Values
            .Where(c => string.IsNullOrEmpty(c.Tag)) // Keep tagged checkpoints
            .OrderBy(c => c.CreatedAt)
            .Take(_checkpoints.Count - _maxCheckpoints)
            .Select(c => c.CheckpointId)
            .ToList();

        foreach (var checkpointId in toDelete)
        {
            await DeleteCheckpointAsync(checkpointId);
        }
    }
}

/// <summary>
/// Checkpoint metadata
/// </summary>
public sealed record CheckpointMetadata
{
    public required string CheckpointId { get; init; }
    public string? Tag { get; init; }
    public string? Description { get; init; }
    public required StateId StateId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required string SessionId { get; init; }

    public override string ToString()
    {
        var tagPart = !string.IsNullOrEmpty(Tag) ? $" [{Tag}]" : "";
        return $"{CheckpointId}{tagPart} - {CreatedAt:yyyy-MM-dd HH:mm:ss}";
    }
}

/// <summary>
/// Checkpoint statistics
/// </summary>
public sealed record CheckpointStats
{
    public int TotalCheckpoints { get; init; }
    public DateTime? OldestCheckpoint { get; init; }
    public DateTime? NewestCheckpoint { get; init; }
    public long TotalSizeBytes { get; init; }
    public int CheckpointsWithTags { get; init; }

    public string TotalSizeMB => $"{TotalSizeBytes / 1024.0 / 1024.0:F2} MB";
}
