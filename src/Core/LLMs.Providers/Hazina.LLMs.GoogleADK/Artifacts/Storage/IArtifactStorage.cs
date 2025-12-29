using Hazina.LLMs.GoogleADK.Artifacts.Models;

namespace Hazina.LLMs.GoogleADK.Artifacts.Storage;

/// <summary>
/// Storage interface for artifacts
/// </summary>
public interface IArtifactStorage
{
    /// <summary>
    /// Store an artifact
    /// </summary>
    Task<string> StoreArtifactAsync(
        Artifact artifact,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve an artifact by ID
    /// </summary>
    Task<Artifact?> GetArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an artifact
    /// </summary>
    Task<bool> DeleteArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for artifacts
    /// </summary>
    Task<List<Artifact>> SearchArtifactsAsync(
        ArtifactQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get artifact data stream
    /// </summary>
    Task<Stream?> GetArtifactStreamAsync(
        string artifactId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update artifact metadata
    /// </summary>
    Task<bool> UpdateMetadataAsync(
        string artifactId,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default);
}
