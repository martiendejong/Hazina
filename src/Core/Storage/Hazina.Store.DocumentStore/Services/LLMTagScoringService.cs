using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// LLM-based tag scoring service that uses AI to score tags based on query relevance.
/// </summary>
public class LLMTagScoringService : ITagScoringService
{
    private readonly ILLMClient _llmClient;
    private readonly ITagRelevanceStore? _store;

    /// <summary>
    /// Create an LLM tag scoring service.
    /// </summary>
    /// <param name="llmClient">LLM client for generating scores</param>
    /// <param name="store">Optional store for caching scores</param>
    public LLMTagScoringService(ILLMClient llmClient, ITagRelevanceStore? store = null)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _store = store;
    }

    /// <summary>
    /// Score tags based on their relevance to a query using LLM.
    /// </summary>
    public async Task<TagRelevanceIndex> ScoreTagsAsync(
        IEnumerable<string> tags,
        string queryContext,
        CancellationToken ct = default)
    {
        var tagList = tags.ToList();
        if (!tagList.Any())
        {
            return new TagRelevanceIndex
            {
                QueryContext = queryContext,
                ContextChecksum = ComputeChecksum(queryContext)
            };
        }

        var prompt = BuildPrompt(tagList, queryContext);
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage(HazinaMessageRole.System, GetSystemPrompt()),
            new HazinaChatMessage(HazinaMessageRole.User, prompt)
        };

        try
        {
            var response = await _llmClient.GetResponse<TagScoreResponse>(messages, null, null, ct);

            var index = new TagRelevanceIndex
            {
                QueryContext = queryContext,
                ContextChecksum = ComputeChecksum(queryContext)
            };

            if (response?.Result?.Scores != null)
            {
                foreach (var score in response.Result.Scores)
                {
                    // Clamp score to 0.0-1.0 range
                    index.TagScores[score.Tag] = Math.Clamp(score.Score, 0.0, 1.0);
                }
            }

            // Ensure all requested tags have a score (default to 0.5 if missing)
            foreach (var tag in tagList)
            {
                if (!index.TagScores.ContainsKey(tag))
                {
                    index.TagScores[tag] = 0.5;
                }
            }

            // Store if we have a store
            if (_store != null)
            {
                await _store.StoreAsync(index, ct);
            }

            return index;
        }
        catch (Exception)
        {
            // On failure, return neutral scores
            var fallback = new TagRelevanceIndex
            {
                QueryContext = queryContext,
                ContextChecksum = ComputeChecksum(queryContext)
            };

            foreach (var tag in tagList)
            {
                fallback.TagScores[tag] = 0.5;
            }

            return fallback;
        }
    }

    /// <summary>
    /// Get or compute tag scores, using cache if available.
    /// </summary>
    public async Task<TagRelevanceIndex> GetOrComputeScoresAsync(
        IEnumerable<string> tags,
        string queryContext,
        TimeSpan? maxCacheAge = null,
        CancellationToken ct = default)
    {
        var checksum = ComputeChecksum(queryContext);

        // Try to get from cache
        if (_store != null)
        {
            var cached = await _store.GetByChecksumAsync(checksum, ct);
            if (cached != null)
            {
                var age = DateTime.UtcNow - cached.Created;
                if (!maxCacheAge.HasValue || age <= maxCacheAge.Value)
                {
                    return cached;
                }
            }
        }

        // Compute new scores
        return await ScoreTagsAsync(tags, queryContext, ct);
    }

    /// <summary>
    /// Check if scores exist for a query context.
    /// </summary>
    public async Task<bool> HasScoresForContextAsync(string queryContext, CancellationToken ct = default)
    {
        if (_store == null)
        {
            return false;
        }

        var checksum = ComputeChecksum(queryContext);
        var index = await _store.GetByChecksumAsync(checksum, ct);
        return index != null;
    }

    private static string GetSystemPrompt()
    {
        return @"You are a relevance scoring assistant. Your task is to score tags based on how relevant they are to a given query or instruction.

For each tag, provide a score from 0.0 to 1.0:
- 1.0 = Highly relevant - the tag directly relates to the query's main topic or intent
- 0.7-0.9 = Relevant - the tag relates to an important aspect of the query
- 0.4-0.6 = Neutral - the tag may or may not be relevant
- 0.1-0.3 = Slightly irrelevant - the tag is somewhat unrelated
- 0.0 = Not relevant - the tag is completely unrelated to the query

Respond with JSON containing a 'scores' array with objects having 'tag' and 'score' properties.";
    }

    private static string BuildPrompt(List<string> tags, string queryContext)
    {
        var tagsList = string.Join(", ", tags.Select(t => $"\"{t}\""));
        return $@"Score the following tags based on their relevance to this query/instruction:

QUERY: {queryContext}

TAGS TO SCORE: [{tagsList}]

Respond with JSON in this format:
{{
  ""scores"": [
    {{ ""tag"": ""tag_name"", ""score"": 0.8 }},
    ...
  ]
}}";
    }

    private static string ComputeChecksum(string input)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = md5.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Response model for tag scoring.
/// </summary>
public class TagScoreResponse : ChatResponse<TagScoreResponse>
{
    [JsonPropertyName("scores")]
    public List<TagScoreItem> Scores { get; set; } = new();

    [JsonIgnore]
    public override TagScoreResponse _example => new()
    {
        Scores = new List<TagScoreItem>
        {
            new TagScoreItem { Tag = "research", Score = 0.9 },
            new TagScoreItem { Tag = "evidence", Score = 0.8 }
        }
    };

    [JsonIgnore]
    public override string _signature => "TagScoreResponse";
}

/// <summary>
/// Individual tag score item.
/// </summary>
public class TagScoreItem
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "";

    [JsonPropertyName("score")]
    public double Score { get; set; }
}
