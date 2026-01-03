using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// LLM-based query intent classifier for accurate classification.
/// Falls back to heuristic classifier if LLM fails.
/// </summary>
public class LLMQueryIntentClassifier : IQueryIntentClassifier
{
    private readonly ILLMClient _llmClient;
    private readonly HeuristicQueryIntentClassifier _fallback;

    public LLMQueryIntentClassifier(ILLMClient llmClient)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _fallback = new HeuristicQueryIntentClassifier();
    }

    public QueryIntent Classify(string query)
    {
        // Synchronous version uses heuristic classifier
        return _fallback.Classify(query);
    }

    public async Task<QueryIntent> ClassifyAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new QueryIntent
            {
                Type = QueryIntentType.Unknown,
                Confidence = 0.0,
                Explanation = "Empty query"
            };
        }

        try
        {
            var prompt = BuildPrompt(query);
            var messages = new List<HazinaChatMessage>
            {
                new HazinaChatMessage(HazinaMessageRole.System, GetSystemPrompt()),
                new HazinaChatMessage(HazinaMessageRole.User, prompt)
            };

            var response = await _llmClient.GetResponse<QueryIntentResponse>(messages, null, null, ct);

            if (response?.Result != null)
            {
                return MapResponse(response.Result, query);
            }
        }
        catch
        {
            // Fall back to heuristic on any error
        }

        return _fallback.Classify(query);
    }

    private static string GetSystemPrompt()
    {
        return @"You are a query intent classifier for a document search system.
Analyze the user's query and determine:

1. The primary intent type:
   - semantic: Conceptual/meaning-based search (e.g., 'What is the thesis?')
   - metadata_filter: Filter by file type, date, etc. (e.g., 'Find PDFs from last week')
   - tag_search: Search by tags/categories (e.g., 'Documents tagged evidence')
   - similarity: Find similar documents (e.g., 'Similar to document X')
   - keyword: Exact text match (e.g., 'Search for ""climate change""')
   - hybrid: Combination of above

2. Extract any filters:
   - file_type: pdf, image, video, audio, document, etc.
   - date_filter: last_week, last_month, specific date
   - tags: Any mentioned tags or categories
   - keywords: Quoted or exact search terms

Respond with JSON only.";
    }

    private static string BuildPrompt(string query)
    {
        return $@"Classify this search query:

""{query}""

Respond with JSON:
{{
  ""intent"": ""semantic|metadata_filter|tag_search|similarity|keyword|hybrid"",
  ""confidence"": 0.0-1.0,
  ""secondary_intent"": ""optional secondary intent"",
  ""file_type"": ""optional: pdf, image, etc."",
  ""date_filter"": ""optional: last_week, last_month, or ISO date"",
  ""tags"": [""optional"", ""tag"", ""list""],
  ""keywords"": [""optional"", ""exact"", ""keywords""],
  ""semantic_query"": ""the conceptual part of the query"",
  ""explanation"": ""brief explanation of classification""
}}";
    }

    private static QueryIntent MapResponse(QueryIntentResponse response, string originalQuery)
    {
        var intent = new QueryIntent
        {
            SemanticQuery = response.SemanticQuery ?? originalQuery,
            Confidence = Math.Clamp(response.Confidence, 0.0, 1.0),
            Explanation = response.Explanation ?? ""
        };

        // Map intent type
        intent.Type = response.Intent?.ToLowerInvariant() switch
        {
            "semantic" => QueryIntentType.Semantic,
            "metadata_filter" => QueryIntentType.MetadataFilter,
            "tag_search" => QueryIntentType.TagSearch,
            "similarity" => QueryIntentType.Similarity,
            "keyword" => QueryIntentType.Keyword,
            "hybrid" => QueryIntentType.Hybrid,
            _ => QueryIntentType.Semantic
        };

        // Map secondary intent
        if (!string.IsNullOrEmpty(response.SecondaryIntent))
        {
            intent.SecondaryType = response.SecondaryIntent.ToLowerInvariant() switch
            {
                "semantic" => QueryIntentType.Semantic,
                "metadata_filter" => QueryIntentType.MetadataFilter,
                "tag_search" => QueryIntentType.TagSearch,
                "similarity" => QueryIntentType.Similarity,
                "keyword" => QueryIntentType.Keyword,
                _ => null
            };
        }

        // Map filters
        if (!string.IsNullOrEmpty(response.FileType))
        {
            var fileType = response.FileType.ToLowerInvariant();
            intent.Filters.MimeTypePrefix = fileType switch
            {
                "pdf" => null,
                "image" => "image/",
                "video" => "video/",
                "audio" => "audio/",
                "text" => "text/",
                _ => null
            };
            intent.Filters.MimeType = fileType == "pdf" ? "application/pdf" : null;
        }

        if (!string.IsNullOrEmpty(response.DateFilter))
        {
            intent.Filters.CreatedAfter = response.DateFilter.ToLowerInvariant() switch
            {
                "last_week" => DateTime.UtcNow.AddDays(-7),
                "last_month" => DateTime.UtcNow.AddMonths(-1),
                "last_year" => DateTime.UtcNow.AddYears(-1),
                _ => DateTime.TryParse(response.DateFilter, out var d) ? d : null
            };
        }

        if (response.Tags != null)
        {
            intent.Filters.Tags.AddRange(response.Tags);
        }

        if (response.Keywords != null)
        {
            intent.Filters.ExactKeywords.AddRange(response.Keywords);
        }

        return intent;
    }
}

/// <summary>
/// Response model for LLM query intent classification.
/// </summary>
public class QueryIntentResponse : ChatResponse<QueryIntentResponse>
{
    [JsonPropertyName("intent")]
    public string? Intent { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("secondary_intent")]
    public string? SecondaryIntent { get; set; }

    [JsonPropertyName("file_type")]
    public string? FileType { get; set; }

    [JsonPropertyName("date_filter")]
    public string? DateFilter { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonPropertyName("semantic_query")]
    public string? SemanticQuery { get; set; }

    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }

    [JsonIgnore]
    public override QueryIntentResponse _example => new()
    {
        Intent = "hybrid",
        Confidence = 0.85,
        SecondaryIntent = "tag_search",
        FileType = "pdf",
        Tags = new List<string> { "evidence" },
        SemanticQuery = "climate change",
        Explanation = "Query seeks PDF documents about climate change tagged as evidence"
    };

    [JsonIgnore]
    public override string _signature => "QueryIntentResponse";
}
