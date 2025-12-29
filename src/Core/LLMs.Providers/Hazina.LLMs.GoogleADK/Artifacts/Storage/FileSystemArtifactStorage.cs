using System.Collections.Concurrent;
using System.Text.Json;
using Hazina.LLMs.GoogleADK.Artifacts.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Artifacts.Storage;

/// <summary>
/// File system based artifact storage
/// </summary>
public class FileSystemArtifactStorage : IArtifactStorage
{
    private readonly string _basePath;
    private readonly ConcurrentDictionary<string, Artifact> _metadataCache = new();
    private readonly ILogger? _logger;

    public FileSystemArtifactStorage(string basePath, ILogger? logger = null)
    {
        _basePath = basePath;
        _logger = logger;

        // Ensure base directory exists
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> StoreArtifactAsync(
        Artifact artifact,
        CancellationToken cancellationToken = default)
    {
        var artifactDir = Path.Combine(_basePath, artifact.ArtifactId);
        Directory.CreateDirectory(artifactDir);

        // Store data if present
        if (artifact.Data != null)
        {
            var dataPath = Path.Combine(artifactDir, "data");
            await File.WriteAllBytesAsync(dataPath, artifact.Data, cancellationToken);
            artifact.FilePath = dataPath;
            artifact.Size = artifact.Data.Length;
        }
        else if (!string.IsNullOrEmpty(artifact.FilePath) && File.Exists(artifact.FilePath))
        {
            // Copy external file
            var destPath = Path.Combine(artifactDir, Path.GetFileName(artifact.FilePath));
            File.Copy(artifact.FilePath, destPath, true);
            artifact.FilePath = destPath;
            artifact.Size = new FileInfo(destPath).Length;
        }

        // Store metadata
        var metadataPath = Path.Combine(artifactDir, "metadata.json");
        var json = JsonSerializer.Serialize(artifact, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(metadataPath, json, cancellationToken);

        _metadataCache[artifact.ArtifactId] = artifact;

        _logger?.LogInformation("Stored artifact {ArtifactId}: {Name} ({Size} bytes)",
            artifact.ArtifactId, artifact.Name, artifact.Size);

        return artifact.ArtifactId;
    }

    public async Task<Artifact?> GetArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        if (_metadataCache.TryGetValue(artifactId, out var cached))
        {
            return cached;
        }

        var metadataPath = Path.Combine(_basePath, artifactId, "metadata.json");
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
        var artifact = JsonSerializer.Deserialize<Artifact>(json);

        if (artifact != null)
        {
            _metadataCache[artifactId] = artifact;
        }

        return artifact;
    }

    public Task<bool> DeleteArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        var artifactDir = Path.Combine(_basePath, artifactId);
        if (Directory.Exists(artifactDir))
        {
            Directory.Delete(artifactDir, true);
            _metadataCache.TryRemove(artifactId, out _);
            _logger?.LogInformation("Deleted artifact: {ArtifactId}", artifactId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public async Task<List<Artifact>> SearchArtifactsAsync(
        ArtifactQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = new List<Artifact>();

        // Load all metadata files
        var directories = Directory.GetDirectories(_basePath);
        foreach (var dir in directories)
        {
            var metadataPath = Path.Combine(dir, "metadata.json");
            if (!File.Exists(metadataPath))
                continue;

            var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
            var artifact = JsonSerializer.Deserialize<Artifact>(json);

            if (artifact == null)
                continue;

            // Apply filters
            if (!string.IsNullOrEmpty(query.AgentId) && artifact.AgentId != query.AgentId)
                continue;

            if (!string.IsNullOrEmpty(query.SessionId) && artifact.SessionId != query.SessionId)
                continue;

            if (query.Type.HasValue && artifact.Type != query.Type.Value)
                continue;

            if (query.CreatedAfter.HasValue && artifact.CreatedAt < query.CreatedAfter.Value)
                continue;

            if (query.CreatedBefore.HasValue && artifact.CreatedAt > query.CreatedBefore.Value)
                continue;

            if (query.Tags != null && query.Tags.Any())
            {
                if (!query.Tags.Any(t => artifact.Tags.Contains(t)))
                    continue;
            }

            results.Add(artifact);

            if (results.Count >= query.Limit)
                break;
        }

        return results.OrderByDescending(a => a.CreatedAt).ToList();
    }

    public Task<Stream?> GetArtifactStreamAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        var dataPath = Path.Combine(_basePath, artifactId, "data");
        if (!File.Exists(dataPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        var stream = File.OpenRead(dataPath);
        return Task.FromResult<Stream?>(stream);
    }

    public async Task<bool> UpdateMetadataAsync(
        string artifactId,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default)
    {
        var artifact = await GetArtifactAsync(artifactId, cancellationToken);
        if (artifact == null)
        {
            return false;
        }

        foreach (var kvp in metadata)
        {
            artifact.Metadata[kvp.Key] = kvp.Value;
        }

        var metadataPath = Path.Combine(_basePath, artifactId, "metadata.json");
        var json = JsonSerializer.Serialize(artifact, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(metadataPath, json, cancellationToken);

        _metadataCache[artifactId] = artifact;

        return true;
    }
}
