namespace Hazina.Neurochain.Core.Learning;

/// <summary>
/// Learned pattern from failures
/// </summary>
public class FailurePattern
{
    /// <summary>
    /// Pattern ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Pattern name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Pattern description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Failure category this pattern applies to
    /// </summary>
    public FailureCategory Category { get; set; }

    /// <summary>
    /// Trigger conditions (what causes this pattern)
    /// </summary>
    public List<string> Triggers { get; set; } = new();

    /// <summary>
    /// Symptoms (how to detect this pattern)
    /// </summary>
    public List<string> Symptoms { get; set; } = new();

    /// <summary>
    /// Number of times this pattern has been observed
    /// </summary>
    public int Occurrences { get; set; }

    /// <summary>
    /// Success rate of prevention strategies (0-1)
    /// </summary>
    public double PreventionSuccessRate { get; set; }

    /// <summary>
    /// Recommended prevention strategies
    /// </summary>
    public List<PreventionStrategy> PreventionStrategies { get; set; } = new();

    /// <summary>
    /// First observed
    /// </summary>
    public DateTime FirstObserved { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last observed
    /// </summary>
    public DateTime LastObserved { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Examples of this pattern
    /// </summary>
    public List<string> ExampleFailureIds { get; set; } = new();

    /// <summary>
    /// Confidence in this pattern (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Whether this pattern is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Strategy to prevent a failure pattern
/// </summary>
public class PreventionStrategy
{
    /// <summary>
    /// Strategy name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Strategy description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Type of prevention
    /// </summary>
    public PreventionType Type { get; set; }

    /// <summary>
    /// Specific action to take
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for the action
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Success rate of this strategy (0-1)
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Number of times this strategy has been applied
    /// </summary>
    public int TimesApplied { get; set; }

    /// <summary>
    /// Number of times this strategy succeeded
    /// </summary>
    public int TimesSucceeded { get; set; }

    /// <summary>
    /// Cost impact (relative, 0-1 where 1 is most expensive)
    /// </summary>
    public double CostImpact { get; set; }

    /// <summary>
    /// Latency impact (relative, 0-1 where 1 is slowest)
    /// </summary>
    public double LatencyImpact { get; set; }
}

/// <summary>
/// Types of prevention strategies
/// </summary>
public enum PreventionType
{
    /// <summary>
    /// Modify the prompt
    /// </summary>
    PromptModification,

    /// <summary>
    /// Switch to different provider
    /// </summary>
    ProviderSwitch,

    /// <summary>
    /// Use different reasoning layer
    /// </summary>
    LayerSelection,

    /// <summary>
    /// Adjust confidence threshold
    /// </summary>
    ConfidenceAdjustment,

    /// <summary>
    /// Add validation rules
    /// </summary>
    ValidationEnhancement,

    /// <summary>
    /// Add ground truth facts
    /// </summary>
    GroundTruthAddition,

    /// <summary>
    /// Enable cross-validation
    /// </summary>
    CrossValidationEnable,

    /// <summary>
    /// Increase reasoning steps
    /// </summary>
    ReasoningDepthIncrease,

    /// <summary>
    /// Add domain context
    /// </summary>
    ContextEnhancement
}

/// <summary>
/// Improvement recommendation based on learned patterns
/// </summary>
public class ImprovementRecommendation
{
    /// <summary>
    /// Recommendation title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Related failure pattern
    /// </summary>
    public string? PatternId { get; set; }

    /// <summary>
    /// Priority (1-5, where 5 is highest)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Expected impact (0-1)
    /// </summary>
    public double ExpectedImpact { get; set; }

    /// <summary>
    /// Confidence in this recommendation (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Recommended actions
    /// </summary>
    public List<PreventionStrategy> RecommendedActions { get; set; } = new();

    /// <summary>
    /// Supporting evidence
    /// </summary>
    public List<string> Evidence { get; set; } = new();

    /// <summary>
    /// Affected scenarios
    /// </summary>
    public List<string> AffectedScenarios { get; set; } = new();
}
