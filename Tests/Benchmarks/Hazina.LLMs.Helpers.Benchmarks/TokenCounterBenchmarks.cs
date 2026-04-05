using BenchmarkDotNet.Attributes;

namespace Hazina.LLMs.Helpers.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class TokenCounterBenchmarks
{
    private TokenCounter _tokenCounter = null!;
    private string _shortText = null!;
    private string _mediumText = null!;
    private string _longText = null!;
    private string _veryLongText = null!;
    private List<string> _documentCollection = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tokenCounter = new TokenCounter();

        // Short text (~10 tokens)
        _shortText = "Hello, world! This is a test.";

        // Medium text (~100 tokens)
        _mediumText = string.Join(" ", Enumerable.Range(0, 20)
            .Select(i => $"This is sentence {i} with some meaningful content."));

        // Long text (~1000 tokens)
        _longText = string.Join(" ", Enumerable.Range(0, 200)
            .Select(i => $"Sentence {i} contains substantial information about various topics and concepts."));

        // Very long text (~10000 tokens)
        _veryLongText = string.Join(" ", Enumerable.Range(0, 2000)
            .Select(i => $"Line {i}: Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua."));

        // Document collection
        _documentCollection = Enumerable.Range(0, 100)
            .Select(i => $"Document {i} with some content for testing.")
            .ToList();
    }

    #region CountTokens Benchmarks

    [Benchmark(Description = "Count tokens in short text (~10 tokens)")]
    public int CountTokens_ShortText()
    {
        return _tokenCounter.CountTokens(_shortText);
    }

    [Benchmark(Description = "Count tokens in medium text (~100 tokens)")]
    public int CountTokens_MediumText()
    {
        return _tokenCounter.CountTokens(_mediumText);
    }

    [Benchmark(Description = "Count tokens in long text (~1000 tokens)")]
    public int CountTokens_LongText()
    {
        return _tokenCounter.CountTokens(_longText);
    }

    [Benchmark(Description = "Count tokens in very long text (~10000 tokens)")]
    public int CountTokens_VeryLongText()
    {
        return _tokenCounter.CountTokens(_veryLongText);
    }

    #endregion

    #region CountTotalTokens Benchmarks

    [Benchmark(Description = "Count total tokens in 100 documents")]
    public int CountTotalTokens_100Documents()
    {
        return _tokenCounter.CountTotalTokens(_documentCollection);
    }

    #endregion

    #region AnalyzeTokens Benchmarks

    [Benchmark(Description = "Analyze tokens in short text")]
    public (int, string) AnalyzeTokens_ShortText()
    {
        return _tokenCounter.AnalyzeTokens(_shortText);
    }

    [Benchmark(Description = "Analyze tokens in medium text")]
    public (int, string) AnalyzeTokens_MediumText()
    {
        return _tokenCounter.AnalyzeTokens(_mediumText);
    }

    [Benchmark(Description = "Analyze tokens in long text")]
    public (int, string) AnalyzeTokens_LongText()
    {
        return _tokenCounter.AnalyzeTokens(_longText);
    }

    #endregion

    #region FilterDocumentsByTokenLimit Benchmarks

    [Benchmark(Description = "Filter documents by token limit (generous)")]
    public List<string> FilterDocuments_GenerousLimit()
    {
        return _tokenCounter.FilterDocumentsByTokenLimit(_documentCollection, 10000);
    }

    [Benchmark(Description = "Filter documents by token limit (moderate)")]
    public List<string> FilterDocuments_ModerateLimit()
    {
        return _tokenCounter.FilterDocumentsByTokenLimit(_documentCollection, 500);
    }

    [Benchmark(Description = "Filter documents by token limit (restrictive)")]
    public List<string> FilterDocuments_RestrictiveLimit()
    {
        return _tokenCounter.FilterDocumentsByTokenLimit(_documentCollection, 100);
    }

    #endregion

    #region TokenCounter Instance Creation Benchmarks

    [Benchmark(Description = "Create TokenCounter instance")]
    public TokenCounter CreateTokenCounterInstance()
    {
        return new TokenCounter();
    }

    [Benchmark(Description = "Create TokenCounter and count tokens")]
    public int CreateAndCountTokens()
    {
        var counter = new TokenCounter();
        return counter.CountTokens(_shortText);
    }

    #endregion
}

[MemoryDiagnoser]
[RankColumn]
public class TokenCounterComparisonBenchmarks
{
    private TokenCounter _tokenCounter = null!;
    private List<string> _varyingLengthTexts = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tokenCounter = new TokenCounter();

        _varyingLengthTexts = new List<string>
        {
            "Short",
            "Medium length text here",
            string.Join(" ", Enumerable.Range(0, 50).Select(i => $"word{i}")),
            string.Join(" ", Enumerable.Range(0, 500).Select(i => $"word{i}")),
            string.Join(" ", Enumerable.Range(0, 1000).Select(i => $"word{i}"))
        };
    }

    [Benchmark(Description = "Sequential counting of varying length texts")]
    public List<int> SequentialCounting()
    {
        return _varyingLengthTexts.Select(text => _tokenCounter.CountTokens(text)).ToList();
    }

    [Benchmark(Description = "Total tokens of varying length texts")]
    public int TotalTokensCounting()
    {
        return _tokenCounter.CountTotalTokens(_varyingLengthTexts);
    }
}

[MemoryDiagnoser]
[RankColumn]
public class StringProcessingBenchmarks
{
    private TokenCounter _tokenCounter = null!;
    private string _textWithNewlines = null!;
    private string _textWithUnicode = null!;
    private string _textWithSpecialChars = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tokenCounter = new TokenCounter();

        _textWithNewlines = string.Join("\n", Enumerable.Range(0, 100)
            .Select(i => $"Line {i} with content"));

        _textWithUnicode = "Unicode characters: 你好世界 こんにちは مرحبا שלום Здравствуйте " +
                          string.Join(" ", Enumerable.Range(0, 20).Select(i => $"word{i}"));

        _textWithSpecialChars = "Special: @#$%^&*()_+-=[]{}|;:',.<>?/~` " +
                               string.Join(" ", Enumerable.Range(0, 20).Select(i => $"word{i}"));
    }

    [Benchmark(Description = "Count tokens with newlines")]
    public int CountTokens_WithNewlines()
    {
        return _tokenCounter.CountTokens(_textWithNewlines);
    }

    [Benchmark(Description = "Count tokens with Unicode")]
    public int CountTokens_WithUnicode()
    {
        return _tokenCounter.CountTokens(_textWithUnicode);
    }

    [Benchmark(Description = "Count tokens with special characters")]
    public int CountTokens_WithSpecialChars()
    {
        return _tokenCounter.CountTokens(_textWithSpecialChars);
    }
}
