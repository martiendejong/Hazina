# Hazina LLM Helpers Benchmarks

This project contains performance benchmarks for DocumentSplitter and TokenCounter classes.

## Running Benchmarks

```bash
cd Tests/Benchmarks/Hazina.LLMs.Helpers.Benchmarks
dotnet run -c Release
```

## Benchmark Categories

### 1. Document Splitter Benchmarks
Tests splitting performance with documents of various sizes:
- Short document (~100 tokens)
- Medium document (~1,000 tokens)
- Long document (~10,000 tokens)
- Very long document (~100,000 tokens)
- Custom separator splitting

### 2. TokensPerPart Configuration Benchmarks
Compares performance with different TokensPerPart settings:
- 100 tokens per part
- 500 tokens per part
- 1000 tokens per part (default)
- 2000 tokens per part
- 4000 tokens per part

**Recommended Configuration:**
- For short documents (< 1000 tokens): Use default (1000)
- For medium documents (1000-10000 tokens): Use 500-1000
- For long documents (> 10000 tokens): Use 1000-2000
- For very large documents: Use 2000-4000

### 3. Token Counter Benchmarks
Tests token counting performance:
- Short text (~10 tokens)
- Medium text (~100 tokens)
- Long text (~1,000 tokens)
- Very long text (~10,000 tokens)
- Total tokens in document collections
- Token analysis with preview
- Document filtering by token limit

### 4. Token Counter Comparison Benchmarks
Compares different counting strategies:
- Sequential counting of varying length texts
- Total tokens counting

### 5. String Processing Benchmarks
Tests handling of different text formats:
- Text with newlines
- Text with Unicode characters
- Text with special characters

## Performance Considerations

### TokenCounter
- Uses a static encoder (initialized once) for efficiency
- Memory-efficient for large documents
- Approximately O(n) time complexity where n is text length

### DocumentSplitter
- Performance depends on TokensPerPart setting
- Lower TokensPerPart = more splitting = more token counting calls
- Higher TokensPerPart = fewer splits but larger parts
- Trade-off between granularity and performance

## Default Values and Tradeoffs

### TokensPerPart Default: 1000

**Rationale:**
1. **Context Window Balance**: Most LLMs have context windows of 4k-32k tokens
2. **Chunking Efficiency**: 1000 tokens ≈ 750 words, good for semantic coherence
3. **Performance**: Minimizes token counting calls while maintaining granularity
4. **Memory**: Reasonable memory footprint per chunk

**Tradeoffs:**
- **Lower values (100-500)**:
  - Pros: Finer granularity, more precise retrieval
  - Cons: More overhead, more API calls, potentially fragmented context

- **Higher values (2000-4000)**:
  - Pros: Fewer chunks, less overhead, better context preservation
  - Cons: Less granular retrieval, larger memory footprint

## Benchmark Results Summary

After running benchmarks, you'll find detailed results in `BenchmarkDotNet.Artifacts/` including:
- Execution times (mean, median, min, max)
- Memory allocations
- Rank comparisons
- Statistical analysis

## Recommended Settings by Use Case

| Use Case | TokensPerPart | Rationale |
|----------|---------------|-----------|
| RAG/Embeddings | 500-1000 | Good semantic chunk size |
| Streaming to LLMs | 1000-2000 | Efficient batching |
| Document Search | 500-1000 | Balance precision/recall |
| Large Document Processing | 2000-4000 | Minimize splits |
| Interactive Chat | 100-500 | Quick responses |

## Continuous Optimization

These benchmarks should be run:
1. Before major refactoring
2. When changing default values
3. After performance-related changes
4. As part of CI/CD pipeline (optional)

## Related Documentation

- [PartialJsonParser Tests](../../Core/Hazina.LLMs.Helpers.Tests/PartialJsonParserTests.cs)
- [DocumentSplitter Tests](../../Core/Hazina.LLMs.Helpers.Tests/DocumentSplitterTests.cs)
- [TokenCounter Tests](../../Core/Hazina.LLMs.Helpers.Tests/TokenCounterTests.cs)
