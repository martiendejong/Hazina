using Hazina.AI.Compression.Interfaces;
using SharpToken;

namespace Hazina.AI.Compression.TokenCounting;

/// <summary>
/// OpenAI-specific token counter using tiktoken (SharpToken)
/// </summary>
public class OpenAITokenCounter : ITokenCounter
{
    private readonly Dictionary<string, GptEncoding> _encodingCache = new();
    private readonly object _lock = new();

    public string ProviderName => "OpenAI";

    public int CountTokens(string text, string? modelName = null)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var encoding = GetEncoding(modelName ?? "gpt-4o");
        return encoding.CountTokens(text);
    }

    public int CountTokens(IEnumerable<string> texts, string? modelName = null)
    {
        return texts.Sum(text => CountTokens(text, modelName));
    }

    public bool SupportsModel(string modelName)
    {
        var normalized = modelName.ToLowerInvariant();
        return normalized.Contains("gpt") ||
               normalized.Contains("text-davinci") ||
               normalized.Contains("text-embedding");
    }

    public string TruncateToTokenLimit(string text, int maxTokens, string? modelName = null)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var encoding = GetEncoding(modelName ?? "gpt-4o");
        var tokens = encoding.Encode(text);

        if (tokens.Count <= maxTokens)
            return text;

        var truncated = tokens.Take(maxTokens).ToList();
        return encoding.Decode(truncated);
    }

    private GptEncoding GetEncoding(string modelName)
    {
        lock (_lock)
        {
            if (_encodingCache.TryGetValue(modelName, out var cached))
                return cached;

            // Map model to encoding
            var encodingName = GetEncodingNameForModel(modelName);
            var encoding = GptEncoding.GetEncoding(encodingName);

            _encodingCache[modelName] = encoding;
            return encoding;
        }
    }

    private static string GetEncodingNameForModel(string modelName)
    {
        var normalized = modelName.ToLowerInvariant();

        // GPT-4 and later use cl100k_base
        if (normalized.Contains("gpt-4") || normalized.Contains("gpt-3.5-turbo"))
            return "cl100k_base";

        // GPT-3 text models use p50k_base
        if (normalized.Contains("text-davinci"))
            return "p50k_base";

        // Embeddings use cl100k_base
        if (normalized.Contains("text-embedding"))
            return "cl100k_base";

        // Default to cl100k_base for newer models
        return "cl100k_base";
    }
}
