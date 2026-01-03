using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Default implementation of composite document scoring.
/// Combines cosine similarity, tag relevance, recency, and position into a final score.
/// </summary>
public class DefaultCompositeScorer : ICompositeScorer
{
    /// <summary>
    /// Score a single document using all available signals.
    /// </summary>
    public ScoredDocument Score(
        ScoredDocument document,
        TagRelevanceIndex? tagIndex,
        int position,
        int totalResults,
        ScoringOptions? options = null)
    {
        options ??= ScoringOptions.Default;

        // Calculate tag score
        if (tagIndex != null && document.Metadata?.Tags != null && document.Metadata.Tags.Any())
        {
            document.TagScore = options.TagAggregation switch
            {
                TagAggregationMethod.Maximum => tagIndex.GetAggregateScore(document.Metadata.Tags),
                TagAggregationMethod.Average => tagIndex.GetAverageScore(document.Metadata.Tags),
                TagAggregationMethod.Sum => Math.Min(1.0, document.Metadata.Tags.Sum(t => tagIndex.GetScore(t))),
                _ => tagIndex.GetAggregateScore(document.Metadata.Tags)
            };
        }
        else
        {
            document.TagScore = 0.5; // Neutral if no tags
        }

        // Calculate recency score (exponential decay)
        document.RecencyScore = CalculateRecencyScore(document.Metadata?.Created, options.RecencyHalfLifeDays);

        // Calculate position score (linear decay from 1.0 to 0.0)
        document.PositionScore = totalResults > 1
            ? 1.0 - ((double)position / (totalResults - 1))
            : 1.0;

        // Calculate composite score
        document.CompositeScore =
            (options.CosineSimilarityWeight * document.Similarity) +
            (options.TagScoreWeight * document.TagScore) +
            (options.RecencyWeight * document.RecencyScore) +
            (options.PositionWeight * document.PositionScore);

        // Store breakdown for debugging/transparency
        document.ScoreBreakdown = new Dictionary<string, double>
        {
            ["similarity"] = document.Similarity,
            ["similarity_weighted"] = options.CosineSimilarityWeight * document.Similarity,
            ["tag_score"] = document.TagScore,
            ["tag_score_weighted"] = options.TagScoreWeight * document.TagScore,
            ["recency_score"] = document.RecencyScore,
            ["recency_score_weighted"] = options.RecencyWeight * document.RecencyScore,
            ["position_score"] = document.PositionScore,
            ["position_score_weighted"] = options.PositionWeight * document.PositionScore,
            ["composite"] = document.CompositeScore
        };

        return document;
    }

    /// <summary>
    /// Score and rank a list of documents.
    /// </summary>
    public List<ScoredDocument> ScoreAndRank(
        IEnumerable<ScoredDocument> documents,
        TagRelevanceIndex? tagIndex,
        ScoringOptions? options = null)
    {
        options ??= ScoringOptions.Default;
        var docList = documents.ToList();
        var totalResults = docList.Count;

        // Score each document
        for (int i = 0; i < docList.Count; i++)
        {
            Score(docList[i], tagIndex, i, totalResults, options);
        }

        // Filter by minimum score
        if (options.MinimumScore > 0)
        {
            docList = docList.Where(d => d.CompositeScore >= options.MinimumScore).ToList();
        }

        // Sort by composite score descending
        return docList.OrderByDescending(d => d.CompositeScore).ToList();
    }

    /// <summary>
    /// Async version of ScoreAndRank.
    /// </summary>
    public Task<List<ScoredDocument>> ScoreAndRankAsync(
        IEnumerable<ScoredDocument> documents,
        TagRelevanceIndex? tagIndex,
        ScoringOptions? options = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(ScoreAndRank(documents, tagIndex, options));
    }

    /// <summary>
    /// Calculate recency score using exponential decay.
    /// </summary>
    private static double CalculateRecencyScore(DateTime? created, double halfLifeDays)
    {
        if (!created.HasValue)
        {
            return 0.5; // Neutral if no date
        }

        var age = DateTime.UtcNow - created.Value;
        if (age.TotalDays <= 0)
        {
            return 1.0; // Future or now = max score
        }

        // Exponential decay: score = 0.5^(age/halfLife)
        return Math.Pow(0.5, age.TotalDays / halfLifeDays);
    }
}
