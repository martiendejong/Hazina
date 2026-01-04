using Hazina.AI.RAG.Utilities;

namespace Hazina.AI.RAG.Embeddings;

/// <summary>
/// Extracts basic metadata from text chunks using rule-based methods (no LLM required)
/// - TF-IDF keywords
/// - Extractive summaries
/// - Basic statistics
/// Cost: FREE
/// </summary>
public class BasicMetadataExtractor
{
    private readonly SemanticChunkingOptions _options;

    // Common English stop words to filter out
    private static readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
        "of", "with", "by", "from", "as", "is", "was", "are", "were", "be",
        "been", "being", "have", "has", "had", "do", "does", "did", "will",
        "would", "could", "should", "may", "might", "must", "can", "this",
        "that", "these", "those", "i", "you", "he", "she", "it", "we", "they",
        "what", "which", "who", "when", "where", "why", "how", "all", "each",
        "every", "both", "few", "more", "most", "other", "some", "such", "no",
        "nor", "not", "only", "own", "same", "so", "than", "too", "very", "just"
    };

    public BasicMetadataExtractor(SemanticChunkingOptions? options = null)
    {
        _options = options ?? new SemanticChunkingOptions();
    }

    /// <summary>
    /// Extract basic metadata from a text chunk
    /// </summary>
    public Dictionary<string, object> ExtractMetadata(string chunkText)
    {
        var metadata = new Dictionary<string, object>
        {
            ["word_count"] = CountWords(chunkText),
            ["sentence_count"] = CountSentences(chunkText),
            ["char_count"] = chunkText.Length
        };

        // Extract keywords using TF-IDF approximation
        var keywords = ExtractKeywordsTFIDF(chunkText, _options.TopKeywordsCount);
        if (keywords.Count > 0)
        {
            metadata["keywords"] = keywords;
        }

        // Generate extractive summary
        var summary = ExtractSummary(chunkText);
        if (!string.IsNullOrEmpty(summary))
        {
            metadata["summary"] = summary;
        }

        // Detect basic content characteristics
        metadata["has_code"] = DetectCode(chunkText);
        metadata["has_urls"] = DetectUrls(chunkText);
        metadata["has_numbers"] = DetectNumbers(chunkText);

        return metadata;
    }

    /// <summary>
    /// Extract keywords using TF-IDF (Term Frequency-Inverse Document Frequency)
    /// Simplified version: just uses term frequency within the chunk
    /// </summary>
    public List<string> ExtractKeywordsTFIDF(string text, int topK = 5)
    {
        // Tokenize and clean words
        var words = TokenizeAndClean(text);

        if (words.Count == 0)
            return new List<string>();

        // Calculate term frequency
        var termFreq = words
            .GroupBy(w => w, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key.ToLower(), g => g.Count());

        // Simple TF scoring (could be enhanced with IDF if we had document corpus)
        // Longer words get slight boost (more likely to be meaningful)
        var scores = termFreq
            .Select(kvp => new
            {
                Word = kvp.Key,
                Score = kvp.Value * (1 + (kvp.Key.Length / 100.0)) // Boost longer words
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Word)
            .ToList();

        return scores;
    }

    /// <summary>
    /// Generate extractive summary (first sentence or most representative sentence)
    /// </summary>
    public string ExtractSummary(string text)
    {
        var sentences = SentenceSplitter.Split(text, minLength: 20);

        if (sentences.Count == 0)
            return text.Substring(0, Math.Min(100, text.Length));

        // Return first sentence as summary (common extractive method)
        var firstSentence = sentences[0];

        // If first sentence is very short, include second sentence too
        if (firstSentence.Length < 50 && sentences.Count > 1)
        {
            firstSentence += " " + sentences[1];
        }

        // Truncate if too long
        if (firstSentence.Length > 200)
        {
            firstSentence = firstSentence.Substring(0, 197) + "...";
        }

        return firstSentence;
    }

    /// <summary>
    /// Count words in text
    /// </summary>
    private int CountWords(string text)
    {
        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Count sentences in text
    /// </summary>
    private int CountSentences(string text)
    {
        return SentenceSplitter.Split(text, minLength: 5).Count;
    }

    /// <summary>
    /// Tokenize text and clean (remove punctuation, stop words, etc.)
    /// </summary>
    private List<string> TokenizeAndClean(string text)
    {
        // Split on whitespace and punctuation
        var words = text.Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']', '{', '}', '<', '>', '/', '\\', '|', '-', '_', '+', '=', '*', '&', '^', '%', '$', '#', '@', '~', '`' },
            StringSplitOptions.RemoveEmptyEntries);

        // Filter and clean
        var cleaned = words
            .Select(w => w.ToLower().Trim())
            .Where(w => w.Length > 2) // Filter very short words
            .Where(w => !_stopWords.Contains(w)) // Filter stop words
            .Where(w => !int.TryParse(w, out _)) // Filter pure numbers
            .Where(w => w.All(c => char.IsLetter(c) || c == '-')) // Only letters and hyphens
            .ToList();

        return cleaned;
    }

    /// <summary>
    /// Detect if text contains code (heuristic)
    /// </summary>
    private bool DetectCode(string text)
    {
        var codeIndicators = new[]
        {
            "function ", "class ", "def ", "public ", "private ", "protected ",
            "return ", "import ", "from ", "const ", "let ", "var ",
            "{", "}", "=>", "//", "/*", "*/"
        };

        return codeIndicators.Any(indicator => text.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Detect if text contains URLs
    /// </summary>
    private bool DetectUrls(string text)
    {
        return text.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("www.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Detect if text contains significant numbers/statistics
    /// </summary>
    private bool DetectNumbers(string text)
    {
        var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var numberCount = words.Count(w => double.TryParse(w.Trim(',', '.', '%', '$'), out _));

        // Consider significant if more than 5% of words are numbers
        return numberCount > 0 && (double)numberCount / words.Length > 0.05;
    }
}
