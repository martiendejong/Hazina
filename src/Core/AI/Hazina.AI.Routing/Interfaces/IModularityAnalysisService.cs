namespace Hazina.AI.Routing.Interfaces;

/// <summary>
/// Analyzes task descriptions to determine how modular (parallelizable) they are.
/// A high modularity score means the task can be split across multiple agents effectively.
/// </summary>
public interface IModularityAnalysisService
{
    /// <summary>
    /// Calculates a modularity score for the given task description.
    /// </summary>
    /// <param name="taskDescription">The task to analyze.</param>
    /// <returns>Score from 0.0 (fully sequential) to 1.0 (highly modular/parallelizable).</returns>
    double CalculateScore(string taskDescription);
}
