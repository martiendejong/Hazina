using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for computing composite document scores from multiple signals.
/// Combines cosine similarity, tag relevance, recency, and position.
/// </summary>
public interface ICompositeScorer
{
    /// <summary>
    /// Score a single document using all available signals.
    /// </summary>
    /// <param name="document">Document with base similarity score</param>
    /// <param name="tagIndex">Tag relevance scores for current query</param>
    /// <param name="position">Position in original result set (0-based)</param>
    /// <param name="totalResults">Total number of results</param>
    /// <param name="options">Scoring weight configuration</param>
    /// <returns>Document with all scores computed</returns>
    ScoredDocument Score(
        ScoredDocument document,
        TagRelevanceIndex? tagIndex,
        int position,
        int totalResults,
        ScoringOptions? options = null);

    /// <summary>
    /// Score and rank a list of documents.
    /// </summary>
    /// <param name="documents">Documents with base similarity scores</param>
    /// <param name="tagIndex">Tag relevance scores for current query</param>
    /// <param name="options">Scoring weight configuration</param>
    /// <returns>Documents scored and sorted by composite score (descending)</returns>
    List<ScoredDocument> ScoreAndRank(
        IEnumerable<ScoredDocument> documents,
        TagRelevanceIndex? tagIndex,
        ScoringOptions? options = null);

    /// <summary>
    /// Async version of ScoreAndRank for large document sets.
    /// </summary>
    Task<List<ScoredDocument>> ScoreAndRankAsync(
        IEnumerable<ScoredDocument> documents,
        TagRelevanceIndex? tagIndex,
        ScoringOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Score and rank documents with detailed explanations.
    /// </summary>
    /// <param name="documents">Documents with base similarity scores</param>
    /// <param name="tagIndex">Tag relevance scores for current query</param>
    /// <param name="options">Scoring weight configuration</param>
    /// <returns>Documents with scores and explanations, sorted by composite score</returns>
    List<ScoredDocument> ScoreAndRankWithExplanations(
        IEnumerable<ScoredDocument> documents,
        TagRelevanceIndex? tagIndex,
        ScoringOptions? options = null);
}
