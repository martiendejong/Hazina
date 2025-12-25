using SharpToken;


public class TokenCounter
{
    // Static field to ensure we only load the encoding once
    private static readonly GptEncoding Encoder;

    // Static constructor to initialize the encoder
    static TokenCounter()
    {
        Encoder = GptEncoding.GetEncoding("cl100k_base");
    }

    /// <summary>
    /// Counts tokens in a given text using SharpToken
    /// </summary>
    /// <param name="text">The text to count tokens for</param>
    /// <returns>Number of tokens in the text</returns>
    public int CountTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        try
        {
            var tokens = Encoder.Encode(text);
            return tokens.Count;
        }
        catch (Exception ex)
        {
            // Log or handle tokenization errors
            Console.WriteLine($"Token counting error: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Calculates total tokens for a collection of documents
    /// </summary>
    /// <param name="documents">Collection of documents</param>
    /// <returns>Total number of tokens</returns>
    public int CountTotalTokens(IEnumerable<string> documents)
    {
        return documents?.Sum(CountTokens) ?? 0;
    }

    /// <summary>
    /// Provides detailed token analysis for a given text
    /// </summary>
    /// <param name="text">Text to analyze</param>
    /// <returns>Tuple with token count and token preview</returns>
    public (int TokenCount, string TokenPreview) AnalyzeTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (0, string.Empty);

        var tokens = Encoder.Encode(text);

        return (
            tokens.Count,
            string.Join(" ", tokens.Take(10).Select(t => Encoder.Decode(new[] { t }))) +
            (tokens.Count > 10 ? "..." : "")
        );
    }

    /// <summary>
    /// Filters documents to stay within a maximum token limit
    /// </summary>
    /// <param name="documents">List of documents to filter</param>
    /// <param name="maxTokens">Maximum number of tokens allowed</param>
    /// <returns>Filtered list of documents</returns>
    public List<string> FilterDocumentsByTokenLimit(
        List<string> documents,
        int maxTokens)
    {
        var filteredDocuments = new List<string>();
        int currentTokenCount = 0;

        foreach (var document in documents)
        {
            int documentTokenCount = CountTokens(document);

            if (currentTokenCount + documentTokenCount <= maxTokens)
            {
                filteredDocuments.Add(document);
                currentTokenCount += documentTokenCount;
            }
            else
            {
                break;
            }
        }

        return filteredDocuments;
    }
}