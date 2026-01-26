using Hazina.App.HazinaCoder.Core.Identity;
using Hazina.App.HazinaCoder.Core.State;

namespace Hazina.App.HazinaCoder.Core.Learning;

/// <summary>
/// Represents a captured experience from user interaction
/// Forms the basis of persistent learning in HazinaCoder v2.0
/// </summary>
public class Experience
{
    /// <summary>
    /// Unique identifier for this experience
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// When this experience occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Type of experience (preference, pattern, resolution, etc.)
    /// </summary>
    public ExperienceType Type { get; set; }

    /// <summary>
    /// Context in which this experience occurred
    /// </summary>
    public ExperienceContext Context { get; set; } = new();

    /// <summary>
    /// The actual content (preference statement, code pattern, solution)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Natural language description for retrieval
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Embedding vector (1536-dim from OpenAI text-embedding-3-small)
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    /// Metadata for filtering and ranking
    /// </summary>
    public ExperienceMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Success/failure outcome (if applicable)
    /// </summary>
    public ExperienceOutcome? Outcome { get; set; }
}

/// <summary>
/// Type of experience captured
/// </summary>
public enum ExperienceType
{
    /// <summary>
    /// User explicitly stated a preference ("I prefer async/await")
    /// </summary>
    UserPreference,

    /// <summary>
    /// A successful code pattern or solution
    /// </summary>
    CodePattern,

    /// <summary>
    /// How an error or bug was resolved
    /// </summary>
    ErrorResolution,

    /// <summary>
    /// Project-specific architectural decisions or context
    /// </summary>
    ProjectContext,

    /// <summary>
    /// Insight about the user (communication style, expertise level)
    /// </summary>
    UserInsight,

    /// <summary>
    /// Effective tool usage patterns
    /// </summary>
    ToolUsage,

    /// <summary>
    /// Effective workflow sequences
    /// </summary>
    WorkflowPattern
}

/// <summary>
/// Context in which an experience occurred
/// </summary>
public class ExperienceContext
{
    /// <summary>
    /// Project name (if applicable)
    /// </summary>
    public string? ProjectName { get; set; }

    /// <summary>
    /// File name (if applicable)
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Function/method name (if applicable)
    /// </summary>
    public string? FunctionName { get; set; }

    /// <summary>
    /// Programming language (C#, TypeScript, etc.)
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Framework/library (.NET, React, etc.)
    /// </summary>
    public string? Framework { get; set; }

    /// <summary>
    /// Workflow mode (Feature Development vs Active Debugging)
    /// </summary>
    public WorkflowMode Mode { get; set; } = WorkflowMode.FeatureDevelopment;

    /// <summary>
    /// Active goals at time of experience
    /// </summary>
    public List<Goal> ActiveGoals { get; set; } = new();
}

/// <summary>
/// Metadata for experience importance and ranking
/// </summary>
public class ExperienceMetadata
{
    /// <summary>
    /// Importance score (0.0 - 1.0)
    /// Calculated from: emotional intensity, goal alignment, novelty
    /// </summary>
    public double Importance { get; set; }

    /// <summary>
    /// Emotional intensity at time of experience (0.0 - 1.0)
    /// Higher for frustration, breakthrough, satisfaction
    /// </summary>
    public double EmotionalIntensity { get; set; }

    /// <summary>
    /// Novelty - how new/surprising this experience was (0.0 - 1.0)
    /// Decreases as similar experiences accumulate
    /// </summary>
    public double Novelty { get; set; } = 1.0;

    /// <summary>
    /// How many times this experience has been retrieved
    /// Indicates usefulness
    /// </summary>
    public int RetrievalCount { get; set; }

    /// <summary>
    /// Last time this experience was retrieved
    /// </summary>
    public DateTime? LastRetrieved { get; set; }

    /// <summary>
    /// Confidence in this experience (0.0 - 1.0)
    /// 1.0 for user-stated preferences, lower for inferred patterns
    /// </summary>
    public double Confidence { get; set; } = 1.0;
}

/// <summary>
/// Outcome of an experience (success/failure)
/// </summary>
public class ExperienceOutcome
{
    /// <summary>
    /// Was this experience successful?
    /// </summary>
    public bool Successful { get; set; }

    /// <summary>
    /// Description of the result
    /// </summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// User satisfaction with the outcome (0.0 - 1.0)
    /// Inferred from follow-up interactions
    /// </summary>
    public double Satisfaction { get; set; }
}

/// <summary>
/// User interaction event for experience capture
/// </summary>
public class UserInteraction
{
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ExperienceContext Context { get; set; } = new();
    public string? Response { get; set; }
}
