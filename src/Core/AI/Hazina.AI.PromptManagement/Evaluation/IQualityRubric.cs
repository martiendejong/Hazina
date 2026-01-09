using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AI.PromptManagement.Evaluation;

/// <summary>
/// Interface for custom quality rubrics
/// </summary>
public interface IQualityRubric
{
    /// <summary>
    /// Unique name for this rubric
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what this rubric evaluates
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Evaluate a response against this rubric
    /// </summary>
    /// <param name="context">Evaluation context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Score between 0.0 and 1.0</returns>
    Task<RubricScore> EvaluateAsync(
        EvaluationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for rubric evaluation
/// </summary>
public class EvaluationContext
{
    public required string Query { get; set; }
    public required string Response { get; set; }
    public string? ExpectedOutput { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Result of rubric evaluation
/// </summary>
public class RubricScore
{
    /// <summary>
    /// Score between 0.0 and 1.0
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Confidence in this score (0.0 to 1.0)
    /// </summary>
    public double? Confidence { get; set; }

    /// <summary>
    /// Explanation of the score
    /// </summary>
    public string? Explanation { get; set; }

    /// <summary>
    /// Detailed breakdown (optional)
    /// </summary>
    public Dictionary<string, double>? Breakdown { get; set; }
}

/// <summary>
/// Factory for creating quality rubrics
/// </summary>
public interface IQualityRubricFactory
{
    /// <summary>
    /// Get a rubric by name
    /// </summary>
    IQualityRubric GetRubric(string name);

    /// <summary>
    /// Register a custom rubric
    /// </summary>
    void RegisterRubric(IQualityRubric rubric);

    /// <summary>
    /// Get all available rubrics
    /// </summary>
    List<string> GetAvailableRubrics();
}
