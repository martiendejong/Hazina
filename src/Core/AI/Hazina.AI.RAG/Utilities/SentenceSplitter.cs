using System.Text.RegularExpressions;

namespace Hazina.AI.RAG.Utilities;

/// <summary>
/// Advanced sentence tokenization utility
/// Splits text into sentences using configurable patterns and rules
/// </summary>
public static class SentenceSplitter
{
    // Common abbreviations that should not trigger sentence boundaries
    private static readonly HashSet<string> _abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "dr", "mr", "mrs", "ms", "prof", "sr", "jr",
        "etc", "vs", "inc", "ltd", "co", "corp",
        "jan", "feb", "mar", "apr", "jun", "jul", "aug", "sep", "sept", "oct", "nov", "dec",
        "st", "ave", "blvd", "rd", "dept", "est", "fig", "vol", "no", "pp"
    };

    /// <summary>
    /// Split text into sentences using regex pattern
    /// </summary>
    /// <param name="text">Text to split</param>
    /// <param name="pattern">Regex pattern for splitting (default: split on .!? followed by whitespace)</param>
    /// <param name="minLength">Minimum sentence length to include</param>
    /// <param name="maxLength">Maximum sentence length (split if exceeded)</param>
    /// <returns>List of sentences</returns>
    public static List<string> Split(
        string text,
        string? pattern = null,
        int minLength = 10,
        int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        pattern ??= @"(?<=[.!?])\s+";

        var sentences = new List<string>();

        // Split using regex
        var rawSentences = Regex.Split(text, pattern)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        // Post-process to handle abbreviations and merge short sentences
        var processedSentences = PostProcess(rawSentences, minLength);

        // Handle max length constraint
        if (maxLength.HasValue)
        {
            processedSentences = EnforceMaxLength(processedSentences, maxLength.Value);
        }

        return processedSentences;
    }

    /// <summary>
    /// Post-process sentences to handle abbreviations and merge short sentences
    /// </summary>
    private static List<string> PostProcess(List<string> rawSentences, int minLength)
    {
        var processed = new List<string>();
        var current = "";

        foreach (var sentence in rawSentences)
        {
            // Check if previous sentence ended with abbreviation
            if (!string.IsNullOrEmpty(current) && EndsWithAbbreviation(current))
            {
                // Merge with next sentence
                current += " " + sentence;
            }
            else
            {
                // Start new sentence
                if (!string.IsNullOrEmpty(current))
                {
                    processed.Add(current);
                }
                current = sentence;
            }
        }

        // Add final sentence
        if (!string.IsNullOrEmpty(current))
        {
            processed.Add(current);
        }

        // Merge sentences shorter than minLength
        return MergeShortSentences(processed, minLength);
    }

    /// <summary>
    /// Check if sentence ends with known abbreviation
    /// </summary>
    private static bool EndsWithAbbreviation(string sentence)
    {
        var words = sentence.TrimEnd('.', '!', '?').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return false;

        var lastWord = words[^1].TrimEnd('.').ToLower();
        return _abbreviations.Contains(lastWord);
    }

    /// <summary>
    /// Merge sentences shorter than minimum length with adjacent sentences
    /// </summary>
    private static List<string> MergeShortSentences(List<string> sentences, int minLength)
    {
        if (sentences.Count == 0) return sentences;

        var merged = new List<string>();
        var buffer = "";

        foreach (var sentence in sentences)
        {
            if (string.IsNullOrEmpty(buffer))
            {
                buffer = sentence;
            }
            else if (buffer.Length < minLength)
            {
                // Merge with current buffer
                buffer += " " + sentence;
            }
            else
            {
                // Buffer is long enough, add it and start new buffer
                merged.Add(buffer);
                buffer = sentence;
            }
        }

        // Add final buffer
        if (!string.IsNullOrEmpty(buffer))
        {
            merged.Add(buffer);
        }

        return merged;
    }

    /// <summary>
    /// Enforce maximum sentence length by splitting long sentences
    /// </summary>
    private static List<string> EnforceMaxLength(List<string> sentences, int maxLength)
    {
        var result = new List<string>();

        foreach (var sentence in sentences)
        {
            if (sentence.Length <= maxLength)
            {
                result.Add(sentence);
            }
            else
            {
                // Split long sentence at natural boundaries (commas, semicolons, conjunctions)
                var fragments = SplitLongSentence(sentence, maxLength);
                result.AddRange(fragments);
            }
        }

        return result;
    }

    /// <summary>
    /// Split a long sentence at natural boundaries
    /// </summary>
    private static List<string> SplitLongSentence(string sentence, int maxLength)
    {
        var fragments = new List<string>();

        // Try to split at commas, semicolons, or conjunctions
        var splitPoints = new[] { ", and ", ", but ", ", or ", "; ", ": " };

        var remaining = sentence;
        while (remaining.Length > maxLength)
        {
            var bestSplitIndex = -1;
            var bestSplitLength = 0;

            // Find the best split point before maxLength
            foreach (var splitPoint in splitPoints)
            {
                var index = remaining.LastIndexOf(splitPoint, Math.Min(maxLength, remaining.Length - 1), StringComparison.Ordinal);
                if (index > bestSplitIndex)
                {
                    bestSplitIndex = index;
                    bestSplitLength = splitPoint.Length;
                }
            }

            if (bestSplitIndex > 0)
            {
                // Split at found point
                fragments.Add(remaining.Substring(0, bestSplitIndex + bestSplitLength).Trim());
                remaining = remaining.Substring(bestSplitIndex + bestSplitLength).Trim();
            }
            else
            {
                // No natural split point found, split at space nearest to maxLength
                var spaceIndex = remaining.LastIndexOf(' ', Math.Min(maxLength, remaining.Length - 1));
                if (spaceIndex > 0)
                {
                    fragments.Add(remaining.Substring(0, spaceIndex).Trim());
                    remaining = remaining.Substring(spaceIndex + 1).Trim();
                }
                else
                {
                    // No space found, force split at maxLength
                    fragments.Add(remaining.Substring(0, maxLength).Trim());
                    remaining = remaining.Substring(maxLength).Trim();
                }
            }
        }

        // Add remaining fragment
        if (!string.IsNullOrEmpty(remaining))
        {
            fragments.Add(remaining);
        }

        return fragments;
    }

    /// <summary>
    /// Split text into sentences with default settings
    /// </summary>
    public static List<string> SplitDefault(string text)
    {
        return Split(text, pattern: null, minLength: 10, maxLength: null);
    }
}
