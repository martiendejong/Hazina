using Hazina.AI.Providers.Core;
using Hazina.AI.RAG.Interfaces;

namespace Hazina.AI.RAG.Retrieval;

/// <summary>
/// Reranks candidates using LLM-based relevance scoring with a stable, deterministic prompt.
/// Each candidate is scored independently on a 0-10 scale for relevance to the query.
/// </summary>
public class LlmJudgeReranker : IReranker
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly LlmJudgeRerankerOptions _options;

    public LlmJudgeReranker(
        IProviderOrchestrator orchestrator,
        LlmJudgeRerankerOptions? options = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _options = options ?? new LlmJudgeRerankerOptions();
    }

    public async Task<List<IRetrievalCandidate>> RerankAsync(
        string query,
        List<IRetrievalCandidate> candidates,
        int topN = 5,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
            return candidates;

        var scoredCandidates = new List<(IRetrievalCandidate candidate, double score)>();

        foreach (var candidate in candidates)
        {
            var relevanceScore = await ScoreRelevanceAsync(query, candidate.Text, cancellationToken);

            if (candidate is RetrievalCandidate rc)
            {
                rc.RerankScore = relevanceScore;
            }

            scoredCandidates.Add((candidate, relevanceScore));
        }

        return scoredCandidates
            .OrderByDescending(sc => sc.score)
            .Take(topN)
            .Select(sc => sc.candidate)
            .ToList();
    }

    private async Task<double> ScoreRelevanceAsync(
        string query,
        string documentText,
        CancellationToken cancellationToken)
    {
        var prompt = BuildScoringPrompt(query, documentText);

        try
        {
            var messages = new List<HazinaChatMessage>
            {
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.System,
                    Text = _options.SystemPrompt
                },
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.User,
                    Text = prompt
                }
            };

            var response = await _orchestrator.GetResponse(
                messages,
                HazinaChatResponseFormat.Text,
                null,
                null,
                cancellationToken
            );

            var scoreText = response.Result.Trim();

            if (int.TryParse(scoreText, out var score))
            {
                return Math.Clamp(score, 0, 10) / 10.0;
            }

            if (double.TryParse(scoreText, out var doubleScore))
            {
                return Math.Clamp(doubleScore, 0.0, 10.0) / 10.0;
            }
        }
        catch
        {
            // Fall back to medium score on error
        }

        return 0.5;
    }

    private string BuildScoringPrompt(string query, string documentText)
    {
        return _options.PromptTemplate
            .Replace("{QUERY}", query)
            .Replace("{DOCUMENT}", TruncateDocument(documentText, _options.MaxDocumentLength));
    }

    private static string TruncateDocument(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }
}

/// <summary>
/// Configuration options for LlmJudgeReranker
/// </summary>
public class LlmJudgeRerankerOptions
{
    /// <summary>
    /// System prompt that instructs the LLM on its role
    /// </summary>
    public string SystemPrompt { get; set; } =
        "You are a relevance scoring system. Rate document relevance to queries on a scale of 0 to 10. " +
        "Respond ONLY with a single integer from 0 to 10. No explanation, no text, just the number.";

    /// <summary>
    /// Template for the scoring prompt. Use {QUERY} and {DOCUMENT} placeholders.
    /// </summary>
    public string PromptTemplate { get; set; } =
        @"Rate the relevance of this document to the query on a scale of 0 to 10.

Query: {QUERY}

Document:
{DOCUMENT}

Relevance score (0-10):";

    /// <summary>
    /// Maximum document length to include in the prompt (to avoid token limits)
    /// </summary>
    public int MaxDocumentLength { get; set; } = 2000;
}
