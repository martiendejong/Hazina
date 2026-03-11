namespace Hazina.AI.TaskPrediction;

/// <summary>
/// Predicts task complexity and scope based on historical outcomes.
/// Reusable across ClickUp, GitHub Issues, Jira, and other task systems.
/// </summary>
public interface IComplexityPredictor
{
    /// <summary>
    /// Predict task complexity (1-10 scale) based on description
    /// </summary>
    /// <param name="taskDescription">Task description text</param>
    /// <param name="taskType">Type of task (e.g., "feature", "bug", "refactor")</param>
    /// <param name="metadata">Optional metadata for context</param>
    /// <returns>Complexity prediction with confidence score</returns>
    Task<ComplexityPrediction> PredictComplexity(
        string taskDescription,
        string taskType,
        Dictionary<string, object>? metadata = null
    );

    /// <summary>
    /// Update prediction model with actual outcome
    /// </summary>
    /// <param name="outcome">Actual task outcome data</param>
    Task UpdateModel(TaskOutcome outcome);

    /// <summary>
    /// Get current prediction accuracy for task type
    /// </summary>
    /// <param name="taskType">Type of task to check accuracy for</param>
    /// <returns>Accuracy score (0-1)</returns>
    Task<double> GetAccuracy(string taskType);

    /// <summary>
    /// Get calibration metrics for predictions
    /// </summary>
    /// <returns>Calibration data showing prediction reliability</returns>
    Task<CalibrationMetrics> GetCalibration();
}

/// <summary>
/// Result of complexity prediction
/// </summary>
public class ComplexityPrediction
{
    /// <summary>Predicted complexity on 1-10 scale</summary>
    public int Complexity { get; set; }

    /// <summary>Predicted hours to complete</summary>
    public double PredictedHours { get; set; }

    /// <summary>Confidence in prediction (0-1)</summary>
    public double Confidence { get; set; }

    /// <summary>Method used for prediction</summary>
    public string Method { get; set; } = "base_estimate";

    /// <summary>IDs of similar past tasks</summary>
    public List<string> SimilarTasks { get; set; } = [];

    /// <summary>Factors that influenced prediction</summary>
    public Dictionary<string, double> Factors { get; set; } = [];
}

/// <summary>
/// Actual outcome data for model updates
/// </summary>
public class TaskOutcome
{
    /// <summary>Task identifier</summary>
    public required string TaskId { get; set; }

    /// <summary>Type of task</summary>
    public required string TaskType { get; set; }

    /// <summary>Actual complexity (1-10)</summary>
    public int ActualComplexity { get; set; }

    /// <summary>Actual hours spent</summary>
    public double ActualHours { get; set; }

    /// <summary>Quality score (0-1)</summary>
    public double Quality { get; set; }

    /// <summary>Outcome classification</summary>
    public required string Outcome { get; set; } // "success", "partial", "failure"

    /// <summary>Factors that led to success</summary>
    public List<string> SuccessFactors { get; set; } = [];

    /// <summary>Blockers encountered</summary>
    public List<string> Blockers { get; set; } = [];

    /// <summary>Timestamp of completion</summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Calibration metrics for prediction accuracy
/// </summary>
public class CalibrationMetrics
{
    /// <summary>Overall accuracy (0-1)</summary>
    public double OverallAccuracy { get; set; }

    /// <summary>Accuracy by task type</summary>
    public Dictionary<string, double> AccuracyByType { get; set; } = [];

    /// <summary>Mean absolute error in hours</summary>
    public double MeanAbsoluteError { get; set; }

    /// <summary>Confidence calibration curve</summary>
    public Dictionary<double, double> ConfidenceCalibration { get; set; } = [];

    /// <summary>Number of predictions made</summary>
    public int TotalPredictions { get; set; }

    /// <summary>Number of outcomes recorded</summary>
    public int TotalOutcomes { get; set; }
}
