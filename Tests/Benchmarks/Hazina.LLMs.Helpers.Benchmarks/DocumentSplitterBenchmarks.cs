using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Hazina.LLMs.Helpers.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class DocumentSplitterBenchmarks
{
    private DocumentSplitter _splitter = null!;
    private string _shortDocument = null!;
    private string _mediumDocument = null!;
    private string _longDocument = null!;
    private string _veryLongDocument = null!;

    [GlobalSetup]
    public void Setup()
    {
        _splitter = new DocumentSplitter { TokensPerPart = 1000 };

        // Short document (~100 tokens)
        _shortDocument = string.Join("\n", Enumerable.Range(0, 10)
            .Select(i => $"This is line {i} with some content."));

        // Medium document (~1000 tokens)
        _mediumDocument = string.Join("\n", Enumerable.Range(0, 100)
            .Select(i => $"This is line {i} with some meaningful content that makes it longer."));

        // Long document (~10000 tokens)
        _longDocument = string.Join("\n", Enumerable.Range(0, 1000)
            .Select(i => $"This is line {i} with substantial content that simulates a real document with multiple sentences and various information."));

        // Very long document (~100000 tokens)
        _veryLongDocument = string.Join("\n", Enumerable.Range(0, 10000)
            .Select(i => $"Line {i}: Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua."));
    }

    [Benchmark(Description = "Split short document (~100 tokens)")]
    public List<string> SplitShortDocument()
    {
        return _splitter.SplitDocument(_shortDocument);
    }

    [Benchmark(Description = "Split medium document (~1000 tokens)")]
    public List<string> SplitMediumDocument()
    {
        return _splitter.SplitDocument(_mediumDocument);
    }

    [Benchmark(Description = "Split long document (~10000 tokens)")]
    public List<string> SplitLongDocument()
    {
        return _splitter.SplitDocument(_longDocument);
    }

    [Benchmark(Description = "Split very long document (~100000 tokens)")]
    public List<string> SplitVeryLongDocument()
    {
        return _splitter.SplitDocument(_veryLongDocument);
    }

    [Benchmark(Description = "Split with custom separator")]
    public List<string> SplitWithCustomSeparator()
    {
        var document = string.Join("|", Enumerable.Range(0, 100)
            .Select(i => $"Part {i} with content"));
        return _splitter.SplitDocument(document, "|");
    }
}

[MemoryDiagnoser]
[RankColumn]
public class TokensPerPartBenchmarks
{
    private string _document = null!;

    [GlobalSetup]
    public void Setup()
    {
        _document = string.Join("\n", Enumerable.Range(0, 1000)
            .Select(i => $"This is line {i} with some content for testing token counting performance."));
    }

    [Benchmark(Description = "TokensPerPart = 100")]
    public List<string> TokensPerPart_100()
    {
        var splitter = new DocumentSplitter { TokensPerPart = 100 };
        return splitter.SplitDocument(_document);
    }

    [Benchmark(Description = "TokensPerPart = 500")]
    public List<string> TokensPerPart_500()
    {
        var splitter = new DocumentSplitter { TokensPerPart = 500 };
        return splitter.SplitDocument(_document);
    }

    [Benchmark(Description = "TokensPerPart = 1000 (default)")]
    public List<string> TokensPerPart_1000()
    {
        var splitter = new DocumentSplitter { TokensPerPart = 1000 };
        return splitter.SplitDocument(_document);
    }

    [Benchmark(Description = "TokensPerPart = 2000")]
    public List<string> TokensPerPart_2000()
    {
        var splitter = new DocumentSplitter { TokensPerPart = 2000 };
        return splitter.SplitDocument(_document);
    }

    [Benchmark(Description = "TokensPerPart = 4000")]
    public List<string> TokensPerPart_4000()
    {
        var splitter = new DocumentSplitter { TokensPerPart = 4000 };
        return splitter.SplitDocument(_document);
    }
}
