namespace Hazina.LLMs.GoogleADK.Artifacts.Models;

/// <summary>
/// Represents an artifact (file or binary data) produced or consumed by an agent
/// </summary>
public class Artifact
{
    public string ArtifactId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ArtifactType Type { get; set; }
    public string MimeType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
    public string? FilePath { get; set; }
    public byte[]? Data { get; set; }
    public string? Uri { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Type of artifact
/// </summary>
public enum ArtifactType
{
    File,
    Binary,
    Text,
    Image,
    Video,
    Audio,
    Document,
    Code,
    Data,
    Model
}

/// <summary>
/// Artifact reference for lightweight passing
/// </summary>
public class ArtifactReference
{
    public string ArtifactId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ArtifactType Type { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? Uri { get; set; }
}

/// <summary>
/// Result of artifact operations
/// </summary>
public class ArtifactOperationResult
{
    public bool Success { get; set; }
    public string? ArtifactId { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Query for searching artifacts
/// </summary>
public class ArtifactQuery
{
    public string? AgentId { get; set; }
    public string? SessionId { get; set; }
    public ArtifactType? Type { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public int Limit { get; set; } = 100;
}
