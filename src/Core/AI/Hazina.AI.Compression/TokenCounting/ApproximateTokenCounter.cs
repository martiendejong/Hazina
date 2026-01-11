using Hazina.AI.Compression.Interfaces;

namespace Hazina.AI.Compression.TokenCounting;

/// <summary>
/// Approximation-based token counter for providers without specific tokenizers.
/// Uses character/word-based heuristics calibrated for common LLMs.
/// </summary>
public class ApproximateTokenCounter : ITokenCounter
{
    private readonly string _providerName;
    private readonly double _charsPerToken;

    public string ProviderName => _providerName;

    public ApproximateTokenCounter(string providerName, double charsPerToken = 4.0)
    {
        _providerName = providerName;
        _charsPerToken = charsPerToken;
    }

    public int CountTokens(string text, string? modelName = null)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Basic approximation: ~4 characters per token for English text
        // Adjust for different providers:
        // - Claude: ~3.8 chars/token
        // - Gemini: ~4.2 chars/token
        var charCount = text.Length;
        var tokenEstimate = (int)Math.Ceiling(charCount / _charsPerToken);

        // Add ~10% overhead for message formatting, special tokens
        return (int)(tokenEstimate * 1.1);
    }

    public int CountTokens(IEnumerable<string> texts, string? modelName = null)
    {
        return texts.Sum(text => CountTokens(text, modelName));
    }

    public bool SupportsModel(string modelName)
    {
        // Fallback counter supports any model
        return true;
    }

    public string TruncateToTokenLimit(string text, int maxTokens, string? modelName = null)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var currentTokens = CountTokens(text, modelName);
        if (currentTokens <= maxTokens)
            return text;

        // Estimate character limit
        var targetChars = (int)(maxTokens * _charsPerToken * 0.9); // 10% safety margin

        if (text.Length <= targetChars)
            return text;

        // Truncate at word boundary
        var truncated = text.Substring(0, Math.Min(targetChars, text.Length));
        var lastSpace = truncated.LastIndexOf(' ');

        if (lastSpace > targetChars / 2) // Don't truncate too aggressively
            truncated = truncated.Substring(0, lastSpace);

        return truncated + "...";
    }
}

/// <summary>
/// Claude-specific approximation (3.8 chars/token)
/// </summary>
public class ClaudeTokenCounter : ApproximateTokenCounter
{
    public ClaudeTokenCounter() : base("Claude", 3.8) { }
}

/// <summary>
/// Gemini-specific approximation (4.2 chars/token)
/// </summary>
public class GeminiTokenCounter : ApproximateTokenCounter
{
    public GeminiTokenCounter() : base("Gemini", 4.2) { }
}
