using Hazina.AI.Routing.Models;

namespace Hazina.AI.Routing.Algorithms;

/// <summary>
/// Calculates and updates trust scores based on agent reputation.
/// Trust = financial parameter (discount on transaction costs), not ethical concept.
/// </summary>
public static class TrustCalculator
{
    public const double BaselineTrust = 5.0;
    public const double FailurePenalty = 1.0;
    public const double ExponentialMovingAverageAlpha = 0.3;

    /// <summary>
    /// Calculates trust score from success rate.
    /// Formula: trust = 10 × success_rate
    /// Recent failure applies penalty: trust -= 1.0
    /// </summary>
    public static double CalculateTrustScore(double successRate, bool recentFailure = false)
    {
        var baseTrust = 10.0 * successRate;

        if (recentFailure)
        {
            baseTrust = Math.Max(0, baseTrust - FailurePenalty);
        }

        return Math.Round(baseTrust, 1);
    }

    /// <summary>
    /// Updates average turns using exponential moving average.
    /// Gives more weight to recent performance.
    /// </summary>
    public static double UpdateAverageTurns(double currentAvg, double newValue)
    {
        if (currentAvg == 0)
        {
            return newValue;
        }

        // EMA: new_avg = α × new_value + (1-α) × old_avg
        return Math.Round(
            (ExponentialMovingAverageAlpha * newValue) +
            ((1 - ExponentialMovingAverageAlpha) * currentAvg),
            2);
    }

    /// <summary>
    /// Gets default execution time estimate for agent type.
    /// Used when no reputation data exists yet.
    /// </summary>
    public static double GetDefaultExecutionTime(AgentType agentType, TaskCategory category)
    {
        return agentType switch
        {
            AgentType.Plan => category switch
            {
                TaskCategory.FeaturePlanning => 6.0,
                TaskCategory.ArchitectureDesign => 8.0,
                _ => 5.0
            },
            AgentType.Explore => category switch
            {
                TaskCategory.ArchitectureAnalysis => 4.0,
                TaskCategory.CodeSearch => 2.0,
                TaskCategory.FileDiscovery => 1.5,
                _ => 3.0
            },
            AgentType.Bash => category switch
            {
                TaskCategory.GitOperations => 1.5,
                TaskCategory.TerminalCommands => 1.0,
                _ => 1.5
            },
            AgentType.GeneralPurpose => category switch
            {
                TaskCategory.Research => 5.0,
                TaskCategory.Debugging => 6.0,
                TaskCategory.ComplexAnalysis => 7.0,
                _ => 4.0
            },
            _ => 3.0
        };
    }

    /// <summary>
    /// Determines if agent is high-trust (success rate > 85%).
    /// High-trust agents earn lower verification overhead.
    /// </summary>
    public static bool IsHighTrust(double successRate)
    {
        return successRate > 0.85;
    }

    /// <summary>
    /// Determines if agent has sufficient data for reliable trust score.
    /// Requires at least 3 tasks to be considered calibrated.
    /// </summary>
    public static bool IsTrustScoreCalibrated(int totalTasks)
    {
        return totalTasks >= 3;
    }
}
