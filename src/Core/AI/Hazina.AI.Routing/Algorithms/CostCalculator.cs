using Hazina.AI.Routing.Models;

namespace Hazina.AI.Routing.Algorithms;

/// <summary>
/// Calculates delegation costs based on Transaction Cost Economics (TCE).
/// Implements formulas for transaction costs, execution costs, and ROI.
/// </summary>
public static class CostCalculator
{
    /// <summary>
    /// Calculates total delegation cost.
    /// </summary>
    /// <param name="trustScore">Trust score for the agent (0-10)</param>
    /// <param name="criticality">Criticality of the task (0-10)</param>
    /// <param name="agentExecutionTurns">Estimated execution time for agent</param>
    /// <returns>Cost breakdown</returns>
    public static CostBreakdown CalculateCosts(
        double trustScore,
        int criticality,
        double agentExecutionTurns)
    {
        var transactionCost = CalculateTransactionCost(trustScore, criticality);
        var executionCost = new ExecutionCost
        {
            Agent = agentExecutionTurns,
            Self = 0 // Will be provided by caller
        };

        return new CostBreakdown
        {
            Transaction = transactionCost,
            Execution = executionCost
        };
    }

    /// <summary>
    /// Calculates transaction cost components.
    /// Transaction Cost = Search + Negotiation + Enforcement
    /// </summary>
    private static TransactionCost CalculateTransactionCost(double trustScore, int criticality)
    {
        // Enforcement cost: (10 - trust) × criticality / 10
        // Higher criticality + lower trust = more verification needed
        var enforcementCost = (10.0 - trustScore) * criticality / 10.0;

        return new TransactionCost
        {
            Search = 0.5,       // Finding/briefing agent
            Negotiation = 0.5,  // Defining success criteria
            Enforcement = enforcementCost
        };
    }

    /// <summary>
    /// Calculates verification level based on trust and criticality.
    /// Formula: turns_required = (10 - trust) × criticality / 10
    /// </summary>
    public static VerificationLevel CalculateVerificationLevel(double trustScore, int criticality)
    {
        var turnsRequired = (10.0 - trustScore) * criticality / 10.0;

        return turnsRequired switch
        {
            < 1.0 => VerificationLevel.SpotCheck,
            < 2.0 => VerificationLevel.Moderate,
            < 4.0 => VerificationLevel.Thorough,
            _ => VerificationLevel.DeepAudit
        };
    }

    /// <summary>
    /// Calculates ROI of delegation.
    /// ROI = (cost_self - cost_delegate) / cost_self
    /// </summary>
    public static double CalculateROI(double costDelegate, double costSelf)
    {
        if (costSelf == 0) return 0;
        return (costSelf - costDelegate) / costSelf;
    }

    /// <summary>
    /// Makes delegation decision based on costs.
    /// Decision logic: Delegate if total_cost_delegate < total_cost_self
    /// </summary>
    public static DecisionType MakeDecision(double totalCostDelegate, double totalCostSelf)
    {
        return totalCostDelegate < totalCostSelf
            ? DecisionType.Delegate
            : DecisionType.DoMyself;
    }

    /// <summary>
    /// Calculates savings from delegation (or cost of not delegating).
    /// </summary>
    public static double CalculateSavings(double totalCostDelegate, double totalCostSelf)
    {
        return Math.Abs(totalCostSelf - totalCostDelegate);
    }
}
