using Hazina.AI.Providers.Core;
using Hazina.AI.RAG.Core;

namespace Hazina.AI.RAG.Retrieval;

/// <summary>
/// Reranks retrieved documents using various strategies
/// </summary>
public class Reranker
{
    private readonly IProviderOrchestrator? _orchestrator;
    private readonly RerankingOptions _options;

    public Reranker(IProviderOrchestrator? orchestrator = null, RerankingOptions? options = null)
    {
        _orchestrator = orchestrator;
        _options = options ?? new RerankingOptions();
    }

    /// <summary>
    /// Rerank documents for better relevance
    /// </summary>
    public async Task<List<RetrievedDocument>> RerankAsync(
        string query,
        List<RetrievedDocument> documents,
        CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
            return documents;

        switch (_options.Strategy)
        {
            case RerankingStrategy.Similarity:
                return documents.OrderByDescending(d => d.Similarity).ToList();

            case RerankingStrategy.LLMBased:
                return await RerankWithLLMAsync(query, documents, cancellationToken);

            case RerankingStrategy.Hybrid:
                return await RerankHybridAsync(query, documents, cancellationToken);

            default:
                return documents;
        }
    }

    /// <summary>
    /// Rerank using LLM to assess relevance
    /// </summary>
    private async Task<List<RetrievedDocument>> RerankWithLLMAsync(
        string query,
        List<RetrievedDocument> documents,
        CancellationToken cancellationToken)
    {
        if (_orchestrator == null)
            return documents; // Fall back to similarity-based

        var scoredDocuments = new List<(RetrievedDocument doc, double score)>();

        foreach (var doc in documents)
        {
            var relevanceScore = await ScoreRelevanceAsync(query, doc.Content, cancellationToken);
            scoredDocuments.Add((doc, relevanceScore));
        }

        return scoredDocuments
            .OrderByDescending(sd => sd.score)
            .Select(sd => sd.doc)
            .ToList();
    }

    /// <summary>
    /// Hybrid reranking combining similarity and LLM
    /// </summary>
    private async Task<List<RetrievedDocument>> RerankHybridAsync(
        string query,
        List<RetrievedDocument> documents,
        CancellationToken cancellationToken)
    {
        if (_orchestrator == null)
            return documents.OrderByDescending(d => d.Similarity).ToList();

        var scoredDocuments = new List<(RetrievedDocument doc, double score)>();

        foreach (var doc in documents)
        {
            var llmScore = await ScoreRelevanceAsync(query, doc.Content, cancellationToken);
            var hybridScore = (doc.Similarity * _options.SimilarityWeight) + (llmScore * _options.LLMWeight);
            scoredDocuments.Add((doc, hybridScore));
        }

        return scoredDocuments
            .OrderByDescending(sd => sd.score)
            .Select(sd => sd.doc)
            .ToList();
    }

    /// <summary>
    /// Score document relevance using LLM
    /// </summary>
    private async Task<double> ScoreRelevanceAsync(
        string query,
        string document,
        CancellationToken cancellationToken)
    {
        if (_orchestrator == null)
            return 0.5;

        var prompt = $@"Rate the relevance of this document to the query on a scale of 0.0 to 1.0.

Query: {query}

Document:
{document}

Respond with only a number between 0.0 and 1.0 representing the relevance score.";

        try
        {
            var messages = new List<HazinaChatMessage>
            {
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.System,
                    Text = "You are a relevance scoring system. Respond only with a decimal number between 0.0 and 1.0."
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

            if (double.TryParse(response.Result.Trim(), out var score))
            {
                return Math.Clamp(score, 0.0, 1.0);
            }
        }
        catch
        {
            // Fall back to medium score on error
        }

        return 0.5;
    }

    /// <summary>
    /// Filter documents by minimum relevance threshold
    /// </summary>
    public List<RetrievedDocument> FilterByRelevance(
        List<RetrievedDocument> documents,
        double minRelevance)
    {
        return documents.Where(d => d.Similarity >= minRelevance).ToList();
    }

    /// <summary>
    /// Diversify results to reduce redundancy
    /// </summary>
    public List<RetrievedDocument> DiversifyResults(
        List<RetrievedDocument> documents,
        int maxResults,
        double diversityThreshold = 0.8)
    {
        if (documents.Count <= maxResults)
            return documents;

        var selected = new List<RetrievedDocument>();
        selected.Add(documents[0]); // Always add top result

        foreach (var doc in documents.Skip(1))
        {
            if (selected.Count >= maxResults)
                break;

            // Check if document is sufficiently different from selected ones
            bool isDiverse = true;
            foreach (var selectedDoc in selected)
            {
                var similarity = CalculateTextSimilarity(doc.Content, selectedDoc.Content);
                if (similarity > diversityThreshold)
                {
                    isDiverse = false;
                    break;
                }
            }

            if (isDiverse)
            {
                selected.Add(doc);
            }
        }

        return selected;
    }

    /// <summary>
    /// Simple text similarity calculation
    /// </summary>
    private double CalculateTextSimilarity(string text1, string text2)
    {
        var words1 = text1.ToLower().Split(new[] { ' ', '\n', '\r', '.', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        var words2 = text2.ToLower().Split(new[] { ' ', '\n', '\r', '.', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        var set1 = new HashSet<string>(words1);
        var set2 = new HashSet<string>(words2);

        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }
}

/// <summary>
/// Reranking options
/// </summary>
public class RerankingOptions
{
    public RerankingStrategy Strategy { get; set; } = RerankingStrategy.Similarity;
    public double SimilarityWeight { get; set; } = 0.5;
    public double LLMWeight { get; set; } = 0.5;
}

/// <summary>
/// Reranking strategies
/// </summary>
public enum RerankingStrategy
{
    None,
    Similarity,
    LLMBased,
    Hybrid
}
