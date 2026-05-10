# Hazina LLM Helpers Benchmarks

Comprehensive performance benchmarking suite for Hazina LLM helper components using BenchmarkDotNet.

## Benchmark Suites

### 1. DocumentSplitter Benchmarks
- Document sizes: Short (~100), Medium (~1K), Long (~10K), Very Long (~100K tokens)
- Custom separator handling
- TokensPerPart configuration comparison (100-4000)

### 2. TokenCounter Benchmarks  
- Token counting performance (4 text sizes)
- Collection processing (100 documents)
- Special characters (Unicode, newlines)

### 3. EmbeddingStore Benchmarks (NEW)
- **Add**: Single/batch additions (10-1000 embeddings)
- **Search**: Similarity search (10-1000 store sizes, variable topK)
- **Update/Delete**: CRUD operation performance
- **Memory**: Footprint analysis (384/768/1536 dimensions)
- **Parallel**: Sequential vs parallel search comparison

## Running Benchmarks

```bash
dotnet run -c Release --project Tests/Benchmarks/Hazina.LLMs.Helpers.Benchmarks
# Select option 1-8
```

## Results Location

Results saved to `BenchmarkDotNet.Artifacts/results/`

## CI Integration Ready

```bash
dotnet run -c Release --project Tests/Benchmarks/Hazina.LLMs.Helpers.Benchmarks -- --filter * --exporters json
```
