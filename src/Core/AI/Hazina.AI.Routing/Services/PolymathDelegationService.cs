using Hazina.LLMs;

namespace Hazina.AI.Routing.Services;

public class PolymathDelegationService : IPolymathDelegationService
{
    private readonly ILlmProvider _llmProvider;
    private const int MaxAgents = 3; // Cost guardrail

    public PolymathDelegationService(ILlmProvider llmProvider)
    {
        _llmProvider = llmProvider;
    }

    public async Task<string> AnalyzeAsync(string query, int agentCount, SynthesisStrategy strategy = SynthesisStrategy.Consensus)
    {
        // Apply cost guardrail
        agentCount = Math.Min(agentCount, MaxAgents);

        // Single agent optimization
        if (agentCount == 1)
        {
            return await _llmProvider.SendAsync(query);
        }

        // Fan out to multiple agents in parallel
        var tasks = Enumerable.Range(0, agentCount)
            .Select(i => _llmProvider.SendAsync(query))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Synthesize results based on strategy
        return strategy switch
        {
            SynthesisStrategy.Consensus => SynthesizeConsensus(results),
            SynthesisStrategy.Best => SynthesizeBest(results),
            SynthesisStrategy.Merge => SynthesizeMerge(results),
            _ => results[0]
        };
    }

    private string SynthesizeConsensus(string[] results)
    {
        // Simple consensus: most common answer
        var grouped = results.GroupBy(r => r).OrderByDescending(g => g.Count());
        return grouped.First().Key;
    }

    private string SynthesizeBest(string[] results)
    {
        // Select longest response as proxy for "best" (most detailed)
        return results.OrderByDescending(r => r.Length).First();
    }

    private string SynthesizeMerge(string[] results)
    {
        // Combine all results with numbered sections
        return string.Join("\n\n", results.Select((r, i) => $"Agent {i + 1}:\n{r}"));
    }
}
