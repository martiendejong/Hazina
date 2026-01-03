using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Fast, pattern-based query intent classifier.
/// Uses regex and keyword matching for instant classification.
/// </summary>
public class HeuristicQueryIntentClassifier : IQueryIntentClassifier
{
    // Patterns for metadata filter detection
    private static readonly Regex MimeTypePattern = new(
        @"\b(pdf|image|video|audio|text|document|spreadsheet|presentation)\s*(files?|documents?)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DatePattern = new(
        @"\b(last|past|recent|this)\s+(week|month|year|day|hour)|" +
        @"\b(from|since|after|before)\s+(\d{1,2}[-/]\d{1,2}[-/]\d{2,4}|\d{4}[-/]\d{1,2}[-/]\d{1,2})|" +
        @"\b(yesterday|today|this week|this month|this year)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TagPattern = new(
        @"\b(tagged?|with tag|category|labeled?)\s+['""]?(\w+)['""]?|" +
        @"\b(evidence|research|thesis|source|reference|draft|final)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SimilarityPattern = new(
        @"\b(similar|like|related)\s+(to|documents?)|more like this|find similar",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex KeywordPattern = new(
        @"['""]([^'""]+)['""]|" +
        @"\b(search|find|look)\s+for\s+['""]?(\w+)['""]?|" +
        @"\bcontaining?\s+['""]?(\w+)['""]?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SemanticPattern = new(
        @"\b(what|why|how|explain|describe|summarize|analyze|compare|" +
        @"understand|meaning|concept|idea|argument|thesis|claim|evidence)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // MIME type mappings
    private static readonly Dictionary<string, string> MimeTypeMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = "application/pdf",
        ["image"] = "image/",
        ["video"] = "video/",
        ["audio"] = "audio/",
        ["text"] = "text/",
        ["document"] = "application/",
        ["spreadsheet"] = "application/vnd.openxmlformats-officedocument.spreadsheetml",
        ["presentation"] = "application/vnd.openxmlformats-officedocument.presentationml"
    };

    public Task<QueryIntent> ClassifyAsync(string query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Classify(query));
    }

    public QueryIntent Classify(string query)
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

        var intent = new QueryIntent
        {
            SemanticQuery = query
        };

        var scores = new Dictionary<QueryIntentType, double>
        {
            [QueryIntentType.Semantic] = 0,
            [QueryIntentType.MetadataFilter] = 0,
            [QueryIntentType.TagSearch] = 0,
            [QueryIntentType.Similarity] = 0,
            [QueryIntentType.Keyword] = 0
        };

        var explanations = new List<string>();

        // Check for MIME type patterns
        var mimeMatch = MimeTypePattern.Match(query);
        if (mimeMatch.Success)
        {
            var fileType = mimeMatch.Groups[1].Value.ToLowerInvariant();
            if (MimeTypeMappings.TryGetValue(fileType, out var mimeType))
            {
                if (mimeType.EndsWith("/"))
                {
                    intent.Filters.MimeTypePrefix = mimeType;
                }
                else
                {
                    intent.Filters.MimeType = mimeType;
                }
            }
            scores[QueryIntentType.MetadataFilter] += 0.4;
            explanations.Add($"File type filter detected: {fileType}");

            // Remove matched portion from semantic query
            intent.SemanticQuery = MimeTypePattern.Replace(intent.SemanticQuery, "").Trim();
        }

        // Check for date patterns
        var dateMatch = DatePattern.Match(query);
        if (dateMatch.Success)
        {
            var dateFilter = ParseDateFilter(dateMatch.Value);
            if (dateFilter.HasValue)
            {
                intent.Filters.CreatedAfter = dateFilter;
                scores[QueryIntentType.MetadataFilter] += 0.3;
                explanations.Add($"Date filter detected: after {dateFilter:yyyy-MM-dd}");
            }
            intent.SemanticQuery = DatePattern.Replace(intent.SemanticQuery, "").Trim();
        }

        // Check for tag patterns
        var tagMatches = TagPattern.Matches(query);
        if (tagMatches.Count > 0)
        {
            foreach (Match match in tagMatches)
            {
                var tag = match.Groups[2].Success ? match.Groups[2].Value : match.Value;
                tag = tag.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(tag) && tag.Length > 2)
                {
                    intent.Filters.Tags.Add(tag);
                }
            }
            if (intent.Filters.Tags.Count > 0)
            {
                scores[QueryIntentType.TagSearch] += 0.5;
                explanations.Add($"Tags detected: {string.Join(", ", intent.Filters.Tags)}");
            }
        }

        // Check for similarity patterns
        if (SimilarityPattern.IsMatch(query))
        {
            scores[QueryIntentType.Similarity] += 0.6;
            explanations.Add("Similarity search detected");
        }

        // Check for keyword/exact match patterns
        var keywordMatches = KeywordPattern.Matches(query);
        if (keywordMatches.Count > 0)
        {
            foreach (Match match in keywordMatches)
            {
                var keyword = match.Groups[1].Success ? match.Groups[1].Value :
                             match.Groups[3].Success ? match.Groups[3].Value :
                             match.Groups[4].Success ? match.Groups[4].Value : null;
                if (!string.IsNullOrEmpty(keyword))
                {
                    intent.Filters.ExactKeywords.Add(keyword);
                }
            }
            if (intent.Filters.ExactKeywords.Count > 0)
            {
                scores[QueryIntentType.Keyword] += 0.4;
                explanations.Add($"Exact keywords: {string.Join(", ", intent.Filters.ExactKeywords)}");
            }
        }

        // Check for semantic patterns
        var semanticMatches = SemanticPattern.Matches(query);
        if (semanticMatches.Count > 0)
        {
            scores[QueryIntentType.Semantic] += 0.3 * semanticMatches.Count;
            explanations.Add("Semantic/conceptual query detected");
        }

        // If query is a question, lean towards semantic
        if (query.TrimEnd().EndsWith("?"))
        {
            scores[QueryIntentType.Semantic] += 0.2;
            explanations.Add("Question format detected");
        }

        // Determine primary intent
        var topIntent = scores.OrderByDescending(kv => kv.Value).First();
        intent.Type = topIntent.Value > 0 ? topIntent.Key : QueryIntentType.Semantic;
        intent.Confidence = Math.Min(1.0, topIntent.Value + 0.3); // Base confidence

        // Check for hybrid intent
        var secondIntent = scores.OrderByDescending(kv => kv.Value).Skip(1).First();
        if (secondIntent.Value > 0.2 && secondIntent.Key != intent.Type)
        {
            intent.SecondaryType = secondIntent.Key;
            intent.Type = QueryIntentType.Hybrid;
            explanations.Add($"Hybrid query: {topIntent.Key} + {secondIntent.Key}");
        }

        // If no strong signal, default to semantic
        if (topIntent.Value < 0.2)
        {
            intent.Type = QueryIntentType.Semantic;
            intent.Confidence = 0.5;
            explanations.Add("Defaulting to semantic search");
        }

        intent.Explanation = string.Join("; ", explanations);
        return intent;
    }

    private static DateTime? ParseDateFilter(string dateText)
    {
        var lower = dateText.ToLowerInvariant();

        if (lower.Contains("today")) return DateTime.UtcNow.Date;
        if (lower.Contains("yesterday")) return DateTime.UtcNow.Date.AddDays(-1);
        if (lower.Contains("last day") || lower.Contains("past day")) return DateTime.UtcNow.AddDays(-1);
        if (lower.Contains("last week") || lower.Contains("past week") || lower.Contains("this week"))
            return DateTime.UtcNow.AddDays(-7);
        if (lower.Contains("last month") || lower.Contains("past month") || lower.Contains("this month"))
            return DateTime.UtcNow.AddMonths(-1);
        if (lower.Contains("last year") || lower.Contains("past year") || lower.Contains("this year"))
            return DateTime.UtcNow.AddYears(-1);

        // Try to parse explicit dates
        var datePatternMatch = Regex.Match(dateText, @"(\d{1,2}[-/]\d{1,2}[-/]\d{2,4}|\d{4}[-/]\d{1,2}[-/]\d{1,2})");
        if (datePatternMatch.Success && DateTime.TryParse(datePatternMatch.Value, out var parsedDate))
        {
            return parsedDate;
        }

        return null;
    }
}
