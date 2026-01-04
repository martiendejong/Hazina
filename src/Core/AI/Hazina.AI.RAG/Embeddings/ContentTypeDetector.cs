using System.Text.RegularExpressions;

namespace Hazina.AI.RAG.Embeddings;

/// <summary>
/// Detects the content type of text for specialized chunking strategies
/// </summary>
public static class ContentTypeDetector
{
    /// <summary>
    /// Detect content type from text using heuristics
    /// </summary>
    public static DocumentContentType Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return DocumentContentType.Other;

        var scores = new Dictionary<DocumentContentType, int>();

        // Check for code patterns
        if (HasCodePatterns(text))
            scores[DocumentContentType.Code] = GetCodeScore(text);

        // Check for markdown patterns
        if (HasMarkdownPatterns(text))
            scores[DocumentContentType.Markdown] = GetMarkdownScore(text);

        // Check for JSON/structured data
        if (HasJsonPatterns(text))
            scores[DocumentContentType.Json] = GetJsonScore(text);

        // Check for technical documentation
        if (HasTechnicalPatterns(text))
            scores[DocumentContentType.TechnicalDoc] = GetTechnicalScore(text);

        // If no clear type detected or multiple mixed types
        if (scores.Count == 0)
            return DocumentContentType.Prose;

        if (scores.Count > 2)
            return DocumentContentType.Mixed;

        // Return highest scoring type
        return scores.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    /// <summary>
    /// Check if text contains code patterns
    /// </summary>
    private static bool HasCodePatterns(string text)
    {
        var codeIndicators = new[]
        {
            "function ", "class ", "def ", "public ", "private ", "protected ",
            "return ", "import ", "from ", "const ", "let ", "var ",
            "if (", "for (", "while (", "switch (", "case "
        };

        return codeIndicators.Any(indicator =>
            text.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get code score (0-100)
    /// </summary>
    private static int GetCodeScore(string text)
    {
        var score = 0;

        // Function/method definitions
        if (Regex.IsMatch(text, @"(function|def|public|private)\s+\w+\s*\("))
            score += 30;

        // Class definitions
        if (Regex.IsMatch(text, @"class\s+\w+"))
            score += 25;

        // Control structures
        if (Regex.IsMatch(text, @"(if|for|while|switch)\s*\("))
            score += 20;

        // Code block markers
        var codeBlockCount = Regex.Matches(text, @"\{|\}").Count;
        score += Math.Min(codeBlockCount * 2, 25);

        return Math.Min(score, 100);
    }

    /// <summary>
    /// Check if text contains markdown patterns
    /// </summary>
    private static bool HasMarkdownPatterns(string text)
    {
        var markdownIndicators = new[]
        {
            "# ", "## ", "### ", "#### ",
            "```", "**", "__", "* ", "- ",
            "[", "](", "![", "|--"
        };

        return markdownIndicators.Any(indicator => text.Contains(indicator));
    }

    /// <summary>
    /// Get markdown score (0-100)
    /// </summary>
    private static int GetMarkdownScore(string text)
    {
        var score = 0;

        // Headers
        var headerCount = Regex.Matches(text, @"^#{1,6}\s+", RegexOptions.Multiline).Count;
        score += Math.Min(headerCount * 15, 40);

        // Code blocks
        var codeBlockCount = Regex.Matches(text, @"```").Count / 2;
        score += Math.Min((int)codeBlockCount * 10, 20);

        // Lists
        var listCount = Regex.Matches(text, @"^[\*\-\+]\s+", RegexOptions.Multiline).Count;
        score += Math.Min(listCount * 2, 20);

        // Links
        var linkCount = Regex.Matches(text, @"\[.+?\]\(.+?\)").Count;
        score += Math.Min(linkCount * 5, 20);

        return Math.Min(score, 100);
    }

    /// <summary>
    /// Check if text contains JSON patterns
    /// </summary>
    private static bool HasJsonPatterns(string text)
    {
        var trimmed = text.TrimStart();
        return (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
               (trimmed.StartsWith("[") && trimmed.EndsWith("]"));
    }

    /// <summary>
    /// Get JSON score (0-100)
    /// </summary>
    private static int GetJsonScore(string text)
    {
        var score = 0;

        // Starts and ends with JSON delimiters
        var trimmed = text.TrimStart();
        if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
            (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
        {
            score += 50;
        }

        // JSON key-value patterns
        var jsonPairCount = Regex.Matches(text, @"""[^""]+"":\s*[""{\[\d]").Count;
        score += Math.Min(jsonPairCount * 5, 50);

        return Math.Min(score, 100);
    }

    /// <summary>
    /// Check if text contains technical documentation patterns
    /// </summary>
    private static bool HasTechnicalPatterns(string text)
    {
        var techIndicators = new[]
        {
            "API", "method", "parameter", "return", "example",
            "configuration", "syntax", "usage", "see also"
        };

        var count = techIndicators.Count(indicator =>
            text.Contains(indicator, StringComparison.OrdinalIgnoreCase));

        return count >= 3; // At least 3 technical indicators
    }

    /// <summary>
    /// Get technical documentation score (0-100)
    /// </summary>
    private static int GetTechnicalScore(string text)
    {
        var score = 0;

        // Technical terms
        var techTerms = new[]
        {
            "API", "method", "function", "parameter", "argument",
            "return", "example", "syntax", "usage", "configuration"
        };

        var termCount = techTerms.Count(term =>
            text.Contains(term, StringComparison.OrdinalIgnoreCase));
        score += Math.Min(termCount * 10, 60);

        // Code snippets
        if (text.Contains("```") || text.Contains("    ")) // Indented code blocks
            score += 20;

        // References/links
        if (Regex.IsMatch(text, @"see\s+(also|below|above)", RegexOptions.IgnoreCase))
            score += 10;

        // Parameter lists
        if (Regex.IsMatch(text, @"-\s+`\w+`\s*:", RegexOptions.Multiline))
            score += 10;

        return Math.Min(score, 100);
    }

    /// <summary>
    /// Get recommended sentence split pattern for content type
    /// </summary>
    public static string GetRecommendedSplitPattern(DocumentContentType contentType)
    {
        return contentType switch
        {
            DocumentContentType.Code => @"(?<=\n)\s*(?=\n|$)", // Split on double newlines
            DocumentContentType.Markdown => @"(?<=\n)\s*(?=#{1,6}\s+|\n)", // Split on headers or blank lines
            DocumentContentType.Json => @"(?<=\})\s*,?\s*(?=\{|$)", // Split on object boundaries
            DocumentContentType.TechnicalDoc => @"(?<=[.!?])\s+(?=[A-Z])", // Standard sentence split
            DocumentContentType.Prose => @"(?<=[.!?])\s+(?=[A-Z])", // Standard sentence split
            DocumentContentType.Mixed => @"(?<=[.!?])\s+(?=[A-Z])", // Standard sentence split
            _ => @"(?<=[.!?])\s+" // Default
        };
    }

    /// <summary>
    /// Get recommended minimum chunk size for content type
    /// </summary>
    public static int GetRecommendedMinChunkSize(DocumentContentType contentType)
    {
        return contentType switch
        {
            DocumentContentType.Code => 200, // Smaller chunks for code (functions/classes)
            DocumentContentType.Markdown => 300, // Standard
            DocumentContentType.Json => 100, // Smaller for structured data
            DocumentContentType.TechnicalDoc => 400, // Larger for complete explanations
            DocumentContentType.Prose => 300, // Standard
            DocumentContentType.Mixed => 300, // Standard
            _ => 300
        };
    }

    /// <summary>
    /// Get recommended maximum chunk size for content type
    /// </summary>
    public static int GetRecommendedMaxChunkSize(DocumentContentType contentType)
    {
        return contentType switch
        {
            DocumentContentType.Code => 1000, // Smaller for code readability
            DocumentContentType.Markdown => 2000, // Standard
            DocumentContentType.Json => 500, // Smaller for structured data
            DocumentContentType.TechnicalDoc => 2500, // Larger for complete sections
            DocumentContentType.Prose => 2000, // Standard
            DocumentContentType.Mixed => 2000, // Standard
            _ => 2000
        };
    }

    /// <summary>
    /// Apply content-type specific adjustments to chunking options
    /// </summary>
    public static void ApplyContentTypeOptimizations(
        SemanticChunkingOptions options,
        string text)
    {
        var contentType = options.ContentType ?? Detect(text);

        // Apply recommended settings if not explicitly overridden
        if (string.IsNullOrEmpty(options.SentenceSplitPattern) ||
            options.SentenceSplitPattern == new SemanticChunkingOptions().SentenceSplitPattern)
        {
            options.SentenceSplitPattern = GetRecommendedSplitPattern(contentType);
        }

        // Store detected type
        options.ContentType = contentType;
    }
}
