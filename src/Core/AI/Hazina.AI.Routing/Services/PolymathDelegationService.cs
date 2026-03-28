using Hazina.AI.Routing.Interfaces;
using Hazina.AI.Routing.Models;
using Hazina.LLMs;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.Routing.Services;

/// <summary>
/// Multi-agent delegation service that fans out queries to multiple LLM agents
/// with differentiated perspectives and synthesizes their results.
/// </summary>
public class PolymathDelegationService : IPolymathDelegationService
{
    private readonly ILLMClient _llmClient;
    private readonly ILogger<PolymathDelegationService> _logger;

    private const int DefaultMaxAgents = 3;

    private static readonly string[] Perspectives =
    [
        "You are a critical analyst. Focus on risks, edge cases, and potential problems. Challenge assumptions.",
        "You are a creative strategist. Focus on opportunities, novel approaches, and innovative solutions.",
        "You are a practical implementer. Focus on feasibility, concrete steps, and real-world constraints."
    ];

    public PolymathDelegationService(ILLMClient llmClient, ILogger<PolymathDelegationService> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public int MaxAgents => DefaultMaxAgents;

    /// <inheritdoc />
    public async Task<PolymathResult> AnalyzeAsync(
        string query,
        int agentCount,
        SynthesisStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        // Cost guardrail: cap at MaxAgents
        var effectiveCount = Math.Clamp(agentCount, 1, MaxAgents);

        if (agentCount > MaxAgents)
        {
            _logger.LogWarning("Requested {Requested} agents, capped to {Max} (cost guardrail)", agentCount, MaxAgents);
        }

        // Single agent: skip fan-out overhead
        if (effectiveCount == 1)
        {
            return await SingleAgentFallback(query, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Starting polymath delegation: {AgentCount} agents, strategy={Strategy}", effectiveCount, strategy);

        // Fan out to multiple agents in parallel with differentiated perspectives
        var tasks = new Task<AgentResponse>[effectiveCount];
        for (var i = 0; i < effectiveCount; i++)
        {
            var index = i;
            var perspective = Perspectives[i % Perspectives.Length];
            tasks[i] = QueryAgent(index, perspective, query, cancellationToken);
        }

        var responses = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Synthesize results
        var synthesized = await Synthesize(
            responses.ToList(), query, strategy, cancellationToken).ConfigureAwait(false);

        var result = new PolymathResult
        {
            SynthesizedResult = synthesized,
            AgentResponses = responses.ToList(),
            Strategy = strategy,
            TotalInputTokens = responses.Sum(r => r.InputTokens),
            TotalOutputTokens = responses.Sum(r => r.OutputTokens)
        };

        _logger.LogInformation(
            "Polymath delegation complete: {AgentCount} agents, {InputTokens} input tokens, {OutputTokens} output tokens",
            result.AgentCount, result.TotalInputTokens, result.TotalOutputTokens);

        return result;
    }

    private async Task<AgentResponse> QueryAgent(
        int index, string perspective, string query, CancellationToken cancellationToken)
    {
        var messages = new List<HazinaChatMessage>
        {
            new(HazinaMessageRole.System, perspective),
            new(HazinaMessageRole.User, query)
        };

        var response = await _llmClient.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            toolsContext: null,
            images: null,
            cancel: cancellationToken).ConfigureAwait(false);

        return new AgentResponse
        {
            AgentIndex = index,
            Perspective = perspective,
            Response = response.Result ?? string.Empty,
            InputTokens = response.TokenUsage?.InputTokens ?? 0,
            OutputTokens = response.TokenUsage?.OutputTokens ?? 0
        };
    }

    private async Task<string> Synthesize(
        List<AgentResponse> responses,
        string originalQuery,
        SynthesisStrategy strategy,
        CancellationToken cancellationToken)
    {
        return strategy switch
        {
            SynthesisStrategy.Best => SelectBest(responses),
            SynthesisStrategy.Consensus => await SynthesizeConsensus(
                responses, originalQuery, cancellationToken).ConfigureAwait(false),
            SynthesisStrategy.Merge => await SynthesizeMerge(
                responses, originalQuery, cancellationToken).ConfigureAwait(false),
            _ => responses[0].Response
        };
    }

    private static string SelectBest(List<AgentResponse> responses)
    {
        // Select the longest response as a proxy for most thorough analysis
        return responses.OrderByDescending(r => r.Response.Length).First().Response;
    }

    private async Task<string> SynthesizeConsensus(
        List<AgentResponse> responses, string originalQuery, CancellationToken cancellationToken)
    {
        // Use the LLM to identify consensus across the different perspectives
        var agentOutputs = string.Join("\n\n---\n\n", responses.Select((r, i) =>
            $"Agent {i + 1} ({(i == 0 ? "Critical Analyst" : i == 1 ? "Creative Strategist" : "Practical Implementer")}):\n{r.Response}"));

        var synthesisPrompt = $"""
            Multiple agents analyzed the following query from different perspectives.
            Identify the points of agreement (consensus) across their responses.
            Produce a unified answer that reflects what the majority agree on.
            Where they disagree, note the disagreement briefly.

            Original query: {originalQuery}

            {agentOutputs}

            Provide a synthesized consensus response:
            """;

        var messages = new List<HazinaChatMessage>
        {
            new(HazinaMessageRole.System, "You are a synthesis agent. Your job is to find consensus across multiple expert analyses."),
            new(HazinaMessageRole.User, synthesisPrompt)
        };

        var response = await _llmClient.GetResponse(
            messages, HazinaChatResponseFormat.Text,
            toolsContext: null, images: null, cancel: cancellationToken).ConfigureAwait(false);

        return response.Result ?? string.Empty;
    }

    private async Task<string> SynthesizeMerge(
        List<AgentResponse> responses, string originalQuery, CancellationToken cancellationToken)
    {
        var agentOutputs = string.Join("\n\n---\n\n", responses.Select((r, i) =>
            $"Agent {i + 1} ({(i == 0 ? "Critical Analyst" : i == 1 ? "Creative Strategist" : "Practical Implementer")}):\n{r.Response}"));

        var mergePrompt = $"""
            Multiple agents analyzed the following query from different perspectives.
            Merge all their findings into a comprehensive, well-structured response.
            Include insights from every agent without redundancy.

            Original query: {originalQuery}

            {agentOutputs}

            Provide a merged comprehensive response:
            """;

        var messages = new List<HazinaChatMessage>
        {
            new(HazinaMessageRole.System, "You are a synthesis agent. Your job is to merge multiple expert analyses into one comprehensive answer."),
            new(HazinaMessageRole.User, mergePrompt)
        };

        var response = await _llmClient.GetResponse(
            messages, HazinaChatResponseFormat.Text,
            toolsContext: null, images: null, cancel: cancellationToken).ConfigureAwait(false);

        return response.Result ?? string.Empty;
    }

    private async Task<PolymathResult> SingleAgentFallback(
        string query, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Single agent mode: skipping polymath fan-out");

        var agentResponse = await QueryAgent(0, Perspectives[0], query, cancellationToken).ConfigureAwait(false);

        return new PolymathResult
        {
            SynthesizedResult = agentResponse.Response,
            AgentResponses = [agentResponse],
            Strategy = SynthesisStrategy.Best,
            TotalInputTokens = agentResponse.InputTokens,
            TotalOutputTokens = agentResponse.OutputTokens
        };
    }
}
