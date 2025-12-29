using Hazina.LLMs.GoogleADK.Artifacts;
using Hazina.LLMs.GoogleADK.Artifacts.Models;
using Hazina.LLMs.GoogleADK.Core;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Agents;

/// <summary>
/// Agent capable of creating and managing artifacts
/// </summary>
public class ArtifactEnabledAgent : LlmAgent
{
    private readonly ArtifactManager _artifactManager;
    private readonly List<string> _producedArtifacts = new();

    public ArtifactEnabledAgent(
        string name,
        ILLMClient llmClient,
        ArtifactManager artifactManager,
        AgentContext? context = null)
        : base(name, llmClient, context)
    {
        _artifactManager = artifactManager;
    }

    /// <summary>
    /// Create artifact from file
    /// </summary>
    public async Task<Artifact> ProduceFileArtifactAsync(
        string filePath,
        string? name = null,
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var artifact = await _artifactManager.CreateFromFileAsync(
            filePath,
            name,
            AgentId,
            sessionId: null,
            cancellationToken
        );

        if (tags != null)
        {
            artifact.Tags = tags;
        }

        _producedArtifacts.Add(artifact.ArtifactId);

        Context.Logger?.LogInformation("Agent {AgentId} produced file artifact: {Name}",
            AgentId, artifact.Name);

        return artifact;
    }

    /// <summary>
    /// Create artifact from text
    /// </summary>
    public async Task<Artifact> ProduceTextArtifactAsync(
        string text,
        string name,
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var artifact = await _artifactManager.CreateFromTextAsync(
            text,
            name,
            AgentId,
            sessionId: null,
            cancellationToken
        );

        if (tags != null)
        {
            artifact.Tags = tags;
        }

        _producedArtifacts.Add(artifact.ArtifactId);

        Context.Logger?.LogInformation("Agent {AgentId} produced text artifact: {Name}",
            AgentId, artifact.Name);

        return artifact;
    }

    /// <summary>
    /// Create artifact from binary data
    /// </summary>
    public async Task<Artifact> ProduceBinaryArtifactAsync(
        byte[] data,
        string name,
        string mimeType = "application/octet-stream",
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var artifact = await _artifactManager.CreateFromDataAsync(
            data,
            name,
            mimeType,
            AgentId,
            sessionId: null,
            cancellationToken
        );

        if (tags != null)
        {
            artifact.Tags = tags;
        }

        _producedArtifacts.Add(artifact.ArtifactId);

        Context.Logger?.LogInformation("Agent {AgentId} produced binary artifact: {Name} ({Size} bytes)",
            AgentId, artifact.Name, artifact.Size);

        return artifact;
    }

    /// <summary>
    /// Consume (read) an artifact
    /// </summary>
    public async Task<Artifact?> ConsumeArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        var artifact = await _artifactManager.GetArtifactAsync(artifactId, cancellationToken);

        if (artifact != null)
        {
            Context.Logger?.LogInformation("Agent {AgentId} consumed artifact: {ArtifactId}",
                AgentId, artifactId);
        }

        return artifact;
    }

    /// <summary>
    /// Get artifact data as text
    /// </summary>
    public async Task<string?> GetArtifactTextAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        return await _artifactManager.GetArtifactTextAsync(artifactId, cancellationToken);
    }

    /// <summary>
    /// Get artifact data as bytes
    /// </summary>
    public async Task<byte[]?> GetArtifactDataAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        return await _artifactManager.GetArtifactDataAsync(artifactId, cancellationToken);
    }

    /// <summary>
    /// Search for artifacts
    /// </summary>
    public async Task<List<Artifact>> SearchArtifactsAsync(
        ArtifactQuery query,
        CancellationToken cancellationToken = default)
    {
        return await _artifactManager.SearchAsync(query, cancellationToken);
    }

    /// <summary>
    /// Get all artifacts produced by this agent
    /// </summary>
    public async Task<List<Artifact>> GetProducedArtifactsAsync(
        CancellationToken cancellationToken = default)
    {
        var artifacts = new List<Artifact>();

        foreach (var artifactId in _producedArtifacts)
        {
            var artifact = await _artifactManager.GetArtifactAsync(artifactId, cancellationToken);
            if (artifact != null)
            {
                artifacts.Add(artifact);
            }
        }

        return artifacts;
    }

    /// <summary>
    /// Export artifact to file
    /// </summary>
    public async Task<string> ExportArtifactAsync(
        string artifactId,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        return await _artifactManager.ExportToFileAsync(artifactId, destinationPath, cancellationToken);
    }
}
