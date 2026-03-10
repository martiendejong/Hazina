namespace Hazina.AI.DecisionTracking;

/// <summary>
/// Tracks AI decisions for transparency and learning.
/// Enables "why did you suggest this?" explanations.
/// </summary>
public interface IDecisionAuditor
{
    /// <summary>
    /// Log a decision with full context and rationale
    /// </summary>
    /// <param name="decision">Decision data</param>
    /// <returns>Decision ID for tracking</returns>
    Task<string> LogDecision(Decision decision);

    /// <summary>
    /// Search for similar past decisions
    /// </summary>
    /// <param name="context">Context to match</param>
    /// <param name="limit">Maximum results</param>
    /// <returns>List of similar decisions</returns>
    Task<List<Decision>> SearchSimilar(string context, int limit = 5);

    /// <summary>
    /// Get outcome of a past decision
    /// </summary>
    /// <param name="decisionId">Decision ID</param>
    /// <returns>Outcome data or null if not available</returns>
    Task<DecisionOutcome?> GetOutcome(string decisionId);

    /// <summary>
    /// Record the outcome of a decision
    /// </summary>
    /// <param name="decisionId">Decision ID</param>
    /// <param name="outcome">Outcome data</param>
    Task RecordOutcome(string decisionId, DecisionOutcome outcome);

    /// <summary>
    /// Get calibration metrics for decision confidence
    /// </summary>
    /// <returns>Calibration data</returns>
    Task<CalibrationMetrics> GetCalibration();
}

/// <summary>
/// Represents a logged decision
/// </summary>
public class Decision
{
    /// <summary>Decision unique identifier</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>When decision was made</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Context in which decision was made</summary>
    public required string Context { get; set; }

    /// <summary>The decision that was made</summary>
    public required string DecisionMade { get; set; }

    /// <summary>Alternative options considered</summary>
    public List<string> AlternativesConsidered { get; set; } = [];

    /// <summary>Confidence in decision (0-1)</summary>
    public double Confidence { get; set; }

    /// <summary>Rationale for the decision</summary>
    public Dictionary<string, object> Rationale { get; set; } = [];

    /// <summary>Tags for categorization</summary>
    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// Outcome of a decision
/// </summary>
public class DecisionOutcome
{
    /// <summary>Associated decision ID</summary>
    public required string DecisionId { get; set; }

    /// <summary>Was the decision successful?</summary>
    public bool Successful { get; set; }

    /// <summary>What actually happened</summary>
    public required string ActualOutcome { get; set; }

    /// <summary>Time until resolution</summary>
    public TimeSpan TimeToResolution { get; set; }

    /// <summary>Lessons learned</summary>
    public List<string> LessonsLearned { get; set; } = [];

    /// <summary>When outcome was recorded</summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Calibration metrics for decision confidence
/// </summary>
public class CalibrationMetrics
{
    /// <summary>Overall decision success rate</summary>
    public double SuccessRate { get; set; }

    /// <summary>How well confidence correlates with success</summary>
    public double ConfidenceCalibration { get; set; }

    /// <summary>Total decisions logged</summary>
    public int TotalDecisions { get; set; }

    /// <summary>Decisions with outcomes</summary>
    public int DecisionsWithOutcomes { get; set; }

    /// <summary>Average time to resolution</summary>
    public TimeSpan AverageTimeToResolution { get; set; }
}
