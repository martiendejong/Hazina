namespace Hazina.AI.Routing.Services;

public enum SynthesisStrategy
{
    Consensus,  // Majority agreement
    Best,       // Highest confidence
    Merge       // Combine findings
}

public interface IPolymathDelegationService
{
    Task<string> AnalyzeAsync(string query, int agentCount, SynthesisStrategy strategy = SynthesisStrategy.Consensus);
}
