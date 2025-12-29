using Hazina.LLMs.GoogleADK.Artifacts.Models;
using Hazina.LLMs.GoogleADK.Artifacts.Storage;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Artifacts;

/// <summary>
/// Manages artifact creation, storage, and retrieval
/// </summary>
public class ArtifactManager : IAsyncDisposable
{
    private readonly IArtifactStorage _storage;
    private readonly ILogger? _logger;

    public ArtifactManager(IArtifactStorage storage, ILogger? logger = null)
    {
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// Create artifact from file
    /// </summary>
    public async Task<Artifact> CreateFromFileAsync(
        string filePath,
        string? name = null,
        string? agentId = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var fileInfo = new FileInfo(filePath);
        var mimeType = GetMimeType(filePath);

        var artifact = new Artifact
        {
            Name = name ?? fileInfo.Name,
            Type = DetermineArtifactType(mimeType),
            MimeType = mimeType,
            Size = fileInfo.Length,
            FilePath = filePath,
            AgentId = agentId ?? string.Empty,
            SessionId = sessionId ?? string.Empty
        };

        await _storage.StoreArtifactAsync(artifact, cancellationToken);

        _logger?.LogInformation("Created artifact from file: {FilePath}", filePath);

        return artifact;
    }

    /// <summary>
    /// Create artifact from binary data
    /// </summary>
    public async Task<Artifact> CreateFromDataAsync(
        byte[] data,
        string name,
        string mimeType = "application/octet-stream",
        string? agentId = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var artifact = new Artifact
        {
            Name = name,
            Type = DetermineArtifactType(mimeType),
            MimeType = mimeType,
            Size = data.Length,
            Data = data,
            AgentId = agentId ?? string.Empty,
            SessionId = sessionId ?? string.Empty
        };

        await _storage.StoreArtifactAsync(artifact, cancellationToken);

        _logger?.LogInformation("Created artifact from data: {Name} ({Size} bytes)", name, data.Length);

        return artifact;
    }

    /// <summary>
    /// Create artifact from text
    /// </summary>
    public async Task<Artifact> CreateFromTextAsync(
        string text,
        string name,
        string? agentId = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var data = System.Text.Encoding.UTF8.GetBytes(text);

        var artifact = new Artifact
        {
            Name = name,
            Type = ArtifactType.Text,
            MimeType = "text/plain",
            Size = data.Length,
            Data = data,
            AgentId = agentId ?? string.Empty,
            SessionId = sessionId ?? string.Empty
        };

        await _storage.StoreArtifactAsync(artifact, cancellationToken);

        _logger?.LogInformation("Created text artifact: {Name}", name);

        return artifact;
    }

    /// <summary>
    /// Get artifact
    /// </summary>
    public async Task<Artifact?> GetArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        return await _storage.GetArtifactAsync(artifactId, cancellationToken);
    }

    /// <summary>
    /// Get artifact data as byte array
    /// </summary>
    public async Task<byte[]?> GetArtifactDataAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        var stream = await _storage.GetArtifactStreamAsync(artifactId, cancellationToken);
        if (stream == null)
        {
            return null;
        }

        using (stream)
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }
    }

    /// <summary>
    /// Get artifact data as text
    /// </summary>
    public async Task<string?> GetArtifactTextAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        var data = await GetArtifactDataAsync(artifactId, cancellationToken);
        if (data == null)
        {
            return null;
        }

        return System.Text.Encoding.UTF8.GetString(data);
    }

    /// <summary>
    /// Search artifacts
    /// </summary>
    public async Task<List<Artifact>> SearchAsync(
        ArtifactQuery query,
        CancellationToken cancellationToken = default)
    {
        return await _storage.SearchArtifactsAsync(query, cancellationToken);
    }

    /// <summary>
    /// Delete artifact
    /// </summary>
    public async Task<bool> DeleteArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _storage.DeleteArtifactAsync(artifactId, cancellationToken);

        if (deleted)
        {
            _logger?.LogInformation("Deleted artifact: {ArtifactId}", artifactId);
        }

        return deleted;
    }

    /// <summary>
    /// Export artifact to file
    /// </summary>
    public async Task<string> ExportToFileAsync(
        string artifactId,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var data = await GetArtifactDataAsync(artifactId, cancellationToken);
        if (data == null)
        {
            throw new InvalidOperationException($"Artifact not found: {artifactId}");
        }

        await File.WriteAllBytesAsync(destinationPath, data, cancellationToken);

        _logger?.LogInformation("Exported artifact {ArtifactId} to {Path}", artifactId, destinationPath);

        return destinationPath;
    }

    private string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".zip" => "application/zip",
            ".cs" => "text/x-csharp",
            ".py" => "text/x-python",
            ".js" => "text/javascript",
            ".html" => "text/html",
            ".css" => "text/css",
            _ => "application/octet-stream"
        };
    }

    private ArtifactType DetermineArtifactType(string mimeType)
    {
        if (mimeType.StartsWith("text/"))
            return ArtifactType.Text;
        if (mimeType.StartsWith("image/"))
            return ArtifactType.Image;
        if (mimeType.StartsWith("video/"))
            return ArtifactType.Video;
        if (mimeType.StartsWith("audio/"))
            return ArtifactType.Audio;
        if (mimeType.Contains("pdf") || mimeType.Contains("document"))
            return ArtifactType.Document;
        if (mimeType.Contains("json") || mimeType.Contains("xml"))
            return ArtifactType.Data;

        return ArtifactType.Binary;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
