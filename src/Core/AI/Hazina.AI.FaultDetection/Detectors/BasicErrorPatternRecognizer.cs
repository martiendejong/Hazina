using Hazina.AI.FaultDetection.Core;
using System.Text.RegularExpressions;

namespace Hazina.AI.FaultDetection.Detectors;

/// <summary>
/// Basic error pattern recognizer with learning capability
/// </summary>
public class BasicErrorPatternRecognizer : IErrorPatternRecognizer
{
    private readonly List<ErrorPattern> _patterns = new();
    private readonly object _lock = new();

    public BasicErrorPatternRecognizer()
    {
        // Initialize with common error patterns
        InitializeCommonPatterns();
    }

    public async Task<ErrorPatternResult> RecognizeAsync(
        string response,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new ErrorPatternResult();

        lock (_lock)
        {
            foreach (var pattern in _patterns)
            {
                var isMatch = pattern.Type switch
                {
                    PatternType.Regex => IsRegexMatch(response, pattern),
                    PatternType.TextMatch => IsTextMatch(response, pattern),
                    PatternType.Semantic => IsSemanticMatch(response, pattern),
                    PatternType.Structural => IsStructuralMatch(response, pattern),
                    _ => false
                };

                if (isMatch)
                {
                    result.MatchedPatterns.Add(new MatchedErrorPattern
                    {
                        Pattern = pattern,
                        MatchConfidence = 0.8,
                        MatchedText = response.Length > 100 ? response.Substring(0, 100) : response
                    });

                    // Update pattern statistics
                    pattern.OccurrenceCount++;
                    pattern.LastSeen = DateTime.UtcNow;
                }
            }
        }

        result.ContainsErrorPattern = result.MatchedPatterns.Any();
        return await Task.FromResult(result);
    }

    public async Task LearnPatternAsync(ErrorPattern pattern)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        lock (_lock)
        {
            // Check if similar pattern already exists
            var existing = _patterns.FirstOrDefault(p =>
                p.Name == pattern.Name ||
                p.Pattern == pattern.Pattern);

            if (existing != null)
            {
                // Update existing pattern
                existing.OccurrenceCount++;
                existing.LastSeen = DateTime.UtcNow;
                existing.Description = pattern.Description; // Update description
            }
            else
            {
                // Add new pattern
                _patterns.Add(pattern);
            }
        }

        await Task.CompletedTask;
    }

    public IEnumerable<ErrorPattern> GetKnownPatterns()
    {
        lock (_lock)
        {
            return _patterns.ToList();
        }
    }

    private void InitializeCommonPatterns()
    {
        _patterns.AddRange(new[]
        {
            new ErrorPattern
            {
                Name = "EmptyResponse",
                Description = "Response is empty or whitespace only",
                Type = PatternType.TextMatch,
                Pattern = "",
                Severity = IssueSeverity.Critical
            },
            new ErrorPattern
            {
                Name = "ApologyPattern",
                Description = "Response starts with an apology",
                Type = PatternType.Regex,
                Pattern = @"^(I apologize|I'm sorry|Sorry)",
                Severity = IssueSeverity.Warning
            },
            new ErrorPattern
            {
                Name = "CannotDoPattern",
                Description = "LLM states it cannot perform the task",
                Type = PatternType.Regex,
                Pattern = @"I (cannot|can't|am unable to)",
                Severity = IssueSeverity.Error
            },
            new ErrorPattern
            {
                Name = "NoInformationPattern",
                Description = "LLM claims no information available",
                Type = PatternType.Regex,
                Pattern = @"(no information|don't have|not aware of)",
                Severity = IssueSeverity.Error
            },
            new ErrorPattern
            {
                Name = "JSONParseError",
                Description = "Invalid JSON structure",
                Type = PatternType.Structural,
                Pattern = "json",
                Severity = IssueSeverity.Error
            },
            new ErrorPattern
            {
                Name = "ExceptionMention",
                Description = "Response mentions exceptions or errors",
                Type = PatternType.Regex,
                Pattern = @"\b(exception|error|failed|failure)\b",
                Severity = IssueSeverity.Warning
            }
        });
    }

    private bool IsRegexMatch(string response, ErrorPattern pattern)
    {
        try
        {
            return Regex.IsMatch(response, pattern.Pattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private bool IsTextMatch(string response, ErrorPattern pattern)
    {
        if (string.IsNullOrEmpty(pattern.Pattern))
        {
            return string.IsNullOrWhiteSpace(response);
        }

        return response.Contains(pattern.Pattern, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSemanticMatch(string response, ErrorPattern pattern)
    {
        // Simple semantic matching based on keyword overlap
        var patternKeywords = GetKeywords(pattern.Pattern);
        var responseKeywords = GetKeywords(response);

        var overlap = patternKeywords.Intersect(responseKeywords).Count();
        var similarity = (double)overlap / Math.Max(patternKeywords.Count, 1);

        return similarity > 0.6; // 60% similarity threshold
    }

    private bool IsStructuralMatch(string response, ErrorPattern pattern)
    {
        return pattern.Pattern.ToLowerInvariant() switch
        {
            "json" => !IsValidJson(response),
            "xml" => !IsValidXml(response),
            "code" => !IsValidCode(response),
            _ => false
        };
    }

    private bool IsValidJson(string text)
    {
        try
        {
            System.Text.Json.JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidXml(string text)
    {
        try
        {
            System.Xml.Linq.XDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidCode(string text)
    {
        // Simple heuristic: check for code indicators
        var codeIndicators = new[] { "{", "}", "(", ")", "function", "class", "var", "const", "let" };
        return codeIndicators.Count(indicator => text.Contains(indicator)) >= 3;
    }

    private HashSet<string> GetKeywords(string text)
    {
        return text
            .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant().Trim())
            .Where(w => w.Length > 2)
            .ToHashSet();
    }
}
