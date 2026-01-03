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
    /// Score and rank documents with detailed explanations.
    /// </summary>
    public List<ScoredDocument> ScoreAndRankWithExplanations(
        IEnumerable<ScoredDocument> documents,
        TagRelevanceIndex? tagIndex,
        ScoringOptions? options = null)
    {
        options ??= ScoringOptions.Default;
        var docList = documents.ToList();
        var totalResults = docList.Count;

        // Score each document with explanation
        for (int i = 0; i < docList.Count; i++)
        {
            ScoreWithExplanation(docList[i], tagIndex, i, totalResults, options);
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
    /// Score a document and generate a detailed explanation.
    /// </summary>
    private ScoredDocument ScoreWithExplanation(
        ScoredDocument document,
        TagRelevanceIndex? tagIndex,
        int position,
        int totalResults,
        ScoringOptions options)
    {
        // First, do the normal scoring
        Score(document, tagIndex, position, totalResults, options);

        // Build the explanation
        var explanation = new SearchResultExplanation();
        var breakdown = new ScoreBreakdownDetail();

        // Similarity details
        breakdown.Similarity = new SimilarityDetail
        {
            Score = document.Similarity,
            Weight = options.CosineSimilarityWeight,
            Contribution = options.CosineSimilarityWeight * document.Similarity,
            Method = "cosine"
        };

        // Tag score details
        var matchedTags = document.Metadata?.Tags ?? new List<string>();
        var tagScores = new Dictionary<string, double>();
        if (tagIndex != null)
        {
            foreach (var tag in matchedTags)
            {
                tagScores[tag] = tagIndex.GetScore(tag);
            }
        }

        breakdown.TagScore = new TagScoreDetail
        {
            Score = document.TagScore,
            Weight = options.TagScoreWeight,
            Contribution = options.TagScoreWeight * document.TagScore,
            MatchedTags = matchedTags.ToList(),
            TagScores = tagScores,
            AggregationMethod = options.TagAggregation.ToString().ToLowerInvariant()
        };

        // Recency details
        var age = document.Metadata != null
            ? DateTime.UtcNow - document.Metadata.Created
            : (TimeSpan?)null;

        breakdown.Recency = new RecencyDetail
        {
            Score = document.RecencyScore,
            Weight = options.RecencyWeight,
            Contribution = options.RecencyWeight * document.RecencyScore,
            DocumentDate = document.Metadata?.Created,
            AgeDescription = FormatAge(age),
            HalfLifeDays = options.RecencyHalfLifeDays
        };

        // Position details
        breakdown.Position = new PositionDetail
        {
            Score = document.PositionScore,
            Weight = options.PositionWeight,
            Contribution = options.PositionWeight * document.PositionScore,
            OriginalPosition = position + 1, // 1-based for display
            TotalResults = totalResults
        };

        explanation.Breakdown = breakdown;

        // Generate boosts and penalties
        if (breakdown.TagScore.Score > 0.7)
        {
            var topTags = tagScores.OrderByDescending(kv => kv.Value).Take(2).Select(kv => kv.Key);
            explanation.Boosts.Add($"High tag relevance: {string.Join(", ", topTags)}");
        }

        if (breakdown.Similarity.Score > 0.8)
        {
            explanation.Boosts.Add("Strong semantic similarity to query");
        }

        if (breakdown.Recency.Score > 0.8)
        {
            explanation.Boosts.Add($"Recent document ({breakdown.Recency.AgeDescription})");
        }

        if (breakdown.Position.Score > 0.8)
        {
            explanation.Boosts.Add("High initial ranking position");
        }

        if (breakdown.TagScore.Score < 0.3)
        {
            explanation.Penalties.Add("Low tag relevance to query");
        }

        if (breakdown.Recency.Score < 0.3)
        {
            explanation.Penalties.Add($"Older document ({breakdown.Recency.AgeDescription})");
        }

        if (breakdown.Similarity.Score < 0.4)
        {
            explanation.Penalties.Add("Low semantic similarity");
        }

        // Generate summary
        explanation.Summary = SearchResultExplanation.GenerateSummary(breakdown);

        document.Explanation = explanation;
        return document;
    }

    /// <summary>
    /// Format a timespan as a human-readable age description.
    /// </summary>
    private static string FormatAge(TimeSpan? age)
    {
        if (!age.HasValue) return "unknown age";

        var days = age.Value.TotalDays;
        if (days < 1) return "today";
        if (days < 2) return "1 day old";
        if (days < 7) return $"{(int)days} days old";
        if (days < 14) return "1 week old";
        if (days < 30) return $"{(int)(days / 7)} weeks old";
        if (days < 60) return "1 month old";
        if (days < 365) return $"{(int)(days / 30)} months old";
        return $"{(int)(days / 365)} years old";
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
