using BenchmarkDotNet.Attributes;
using Hazina.Store.EmbeddingStore;

namespace Hazina.LLMs.Helpers.Benchmarks;

/// <summary>
/// Benchmarks for EmbeddingStore operations
/// Tests performance of add, search, update, and delete operations using the new async API
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class EmbeddingStoreBenchmarks
{
    private EmbeddingMemoryStore _store = null!;
    private List<Embedding> _smallEmbeddings = null!;
    private List<Embedding> _mediumEmbeddings = null!;
    private List<Embedding> _largeEmbeddings = null!;
    private Embedding _queryEmbedding = null!;

    [GlobalSetup]
    public void Setup()
    {
        _store = new EmbeddingMemoryStore();

        // Create embeddings of various sizes
        var random = new Random(42); // Fixed seed for reproducibility

        // Small: 10 embeddings, 384 dimensions (typical for small models)
        _smallEmbeddings = Enumerable.Range(0, 10)
            .Select(_ => new Embedding(Enumerable.Range(0, 384).Select(_ => random.NextDouble())))
            .ToList();

        // Medium: 100 embeddings, 768 dimensions (typical for BERT-like models)
        _mediumEmbeddings = Enumerable.Range(0, 100)
            .Select(_ => new Embedding(Enumerable.Range(0, 768).Select(_ => random.NextDouble())))
            .ToList();

        // Large: 1000 embeddings, 1536 dimensions (typical for OpenAI ada-002)
        _largeEmbeddings = Enumerable.Range(0, 1000)
            .Select(_ => new Embedding(Enumerable.Range(0, 1536).Select(_ => random.NextDouble())))
            .ToList();

        // Query embedding (768 dimensions for medium benchmarks)
        _queryEmbedding = new Embedding(Enumerable.Range(0, 768).Select(_ => random.NextDouble()));
    }

    #region Add Operation Benchmarks

    [Benchmark(Description = "Add single embedding (384 dim)")]
    public async Task AddSingleEmbedding_Small()
    {
        var store = new EmbeddingMemoryStore();
        await store.StoreAsync("test-id-1", _smallEmbeddings[0], "checksum-1");
    }

    [Benchmark(Description = "Add single embedding (768 dim)")]
    public async Task AddSingleEmbedding_Medium()
    {
        var store = new EmbeddingMemoryStore();
        await store.StoreAsync("test-id-1", _mediumEmbeddings[0], "checksum-1");
    }

    [Benchmark(Description = "Add single embedding (1536 dim)")]
    public async Task AddSingleEmbedding_Large()
    {
        var store = new EmbeddingMemoryStore();
        await store.StoreAsync("test-id-1", _largeEmbeddings[0], "checksum-1");
    }

    [Benchmark(Description = "Add 10 embeddings (batch)")]
    public async Task AddBatch_10Embeddings()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 10; i++)
        {
            await store.StoreAsync($"id-{i}", _smallEmbeddings[i], $"checksum-{i}");
        }
    }

    [Benchmark(Description = "Add 100 embeddings (batch)")]
    public async Task AddBatch_100Embeddings()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 100; i++)
        {
            await store.StoreAsync($"id-{i}", _mediumEmbeddings[i], $"checksum-{i}");
        }
    }

    [Benchmark(Description = "Add 1000 embeddings (batch)")]
    public async Task AddBatch_1000Embeddings()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 1000; i++)
        {
            await store.StoreAsync($"id-{i}", _largeEmbeddings[i % 1000], $"checksum-{i}");
        }
    }

    #endregion

    #region Search Operation Benchmarks

    [Benchmark(Description = "Search in 10 embeddings (top 5)")]
    public async Task<List<ScoredEmbedding>> Search_10Embeddings_Top5()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 10; i++)
        {
            await store.StoreAsync($"id-{i}", _mediumEmbeddings[i], $"checksum-{i}");
        }
        return await store.SearchSimilarAsync(_queryEmbedding, 5);
    }

    [Benchmark(Description = "Search in 100 embeddings (top 5)")]
    public async Task<List<ScoredEmbedding>> Search_100Embeddings_Top5()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 100; i++)
        {
            await store.StoreAsync($"id-{i}", _mediumEmbeddings[i], $"checksum-{i}");
        }
        return await store.SearchSimilarAsync(_queryEmbedding, 5);
    }

    [Benchmark(Description = "Search in 1000 embeddings (top 5)")]
    public async Task<List<ScoredEmbedding>> Search_1000Embeddings_Top5()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 1000; i++)
        {
            await store.StoreAsync($"id-{i}", _mediumEmbeddings[i % 100], $"checksum-{i}");
        }
        return await store.SearchSimilarAsync(_queryEmbedding, 5);
    }

    [Benchmark(Description = "Search top 1 vs top 10 vs top 50")]
    public async Task<(List<ScoredEmbedding>, List<ScoredEmbedding>, List<ScoredEmbedding>)> Search_VariableTopK()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 100; i++)
        {
            await store.StoreAsync($"id-{i}", _mediumEmbeddings[i], $"checksum-{i}");
        }
        var top1 = await store.SearchSimilarAsync(_queryEmbedding, 1);
        var top10 = await store.SearchSimilarAsync(_queryEmbedding, 10);
        var top50 = await store.SearchSimilarAsync(_queryEmbedding, 50);
        return (top1, top10, top50);
    }

    #endregion

    #region Update Operation Benchmarks

    [Benchmark(Description = "Update embedding (10 item store)")]
    public async Task Update_SmallStore()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 10; i++)
        {
            await store.StoreAsync($"id-{i}", _smallEmbeddings[i], $"checksum-{i}");
        }
        await store.StoreAsync("id-5", _smallEmbeddings[0], "checksum-updated");
    }

    [Benchmark(Description = "Update embedding (100 item store)")]
    public async Task Update_MediumStore()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 100; i++)
        {
            await store.StoreAsync($"id-{i}", _mediumEmbeddings[i], $"checksum-{i}");
        }
        await store.StoreAsync("id-50", _mediumEmbeddings[0], "checksum-updated");
    }

    #endregion

    #region Delete Operation Benchmarks

    [Benchmark(Description = "Delete embedding (10 item store)")]
    public async Task Delete_SmallStore()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 10; i++)
        {
            await store.StoreAsync($"id-{i}", _smallEmbeddings[i], $"checksum-{i}");
        }
        await store.RemoveAsync("id-5");
    }

    [Benchmark(Description = "Delete embedding (100 item store)")]
    public async Task Delete_MediumStore()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 100; i++)
        {
            await store.StoreAsync($"id-{i}", _mediumEmbeddings[i], $"checksum-{i}");
        }
        await store.RemoveAsync("id-50");
    }

    #endregion

    #region Memory Footprint Benchmarks

    [Benchmark(Description = "Memory: Store 100 small embeddings (384 dim)")]
    public async Task<EmbeddingMemoryStore> MemoryFootprint_100Small()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 100; i++)
        {
            await store.StoreAsync($"id-{i}", _smallEmbeddings[i % 10], $"checksum-{i}");
        }
        return store;
    }

    [Benchmark(Description = "Memory: Store 100 medium embeddings (768 dim)")]
    public async Task<EmbeddingMemoryStore> MemoryFootprint_100Medium()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 100; i++)
        {
            await store.StoreAsync($"id-{i}", _mediumEmbeddings[i], $"checksum-{i}");
        }
        return store;
    }

    [Benchmark(Description = "Memory: Store 100 large embeddings (1536 dim)")]
    public async Task<EmbeddingMemoryStore> MemoryFootprint_100Large()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 100; i++)
        {
            await store.StoreAsync($"id-{i}", _largeEmbeddings[i % 1000], $"checksum-{i}");
        }
        return store;
    }

    #endregion
}

/// <summary>
/// Benchmarks comparing sequential vs parallel operations
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class EmbeddingStoreParallelBenchmarks
{
    private List<Embedding> _embeddings = null!;
    private List<Embedding> _queries = null!;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _embeddings = Enumerable.Range(0, 1000)
            .Select(_ => new Embedding(Enumerable.Range(0, 768).Select(_ => random.NextDouble())))
            .ToList();
        _queries = Enumerable.Range(0, 10)
            .Select(_ => new Embedding(Enumerable.Range(0, 768).Select(_ => random.NextDouble())))
            .ToList();
    }

    [Benchmark(Description = "Sequential add 1000 embeddings")]
    public async Task<EmbeddingMemoryStore> SequentialAdd()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 1000; i++)
        {
            await store.StoreAsync($"id-{i}", _embeddings[i], $"checksum-{i}");
        }
        return store;
    }

    [Benchmark(Description = "Sequential search 10 queries")]
    public async Task<List<List<ScoredEmbedding>>> SequentialSearch()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 100; i++)
        {
            await store.StoreAsync($"id-{i}", _embeddings[i], $"checksum-{i}");
        }

        var results = new List<List<ScoredEmbedding>>();
        foreach (var query in _queries)
        {
            results.Add(await store.SearchSimilarAsync(query, 5));
        }
        return results;
    }

    [Benchmark(Description = "Parallel search 10 queries")]
    public async Task<List<List<ScoredEmbedding>>> ParallelSearch()
    {
        var store = new EmbeddingMemoryStore();
        for (int i = 0; i < 100; i++)
        {
            await store.StoreAsync($"id-{i}", _embeddings[i], $"checksum-{i}");
        }

        var tasks = _queries.Select(query => store.SearchSimilarAsync(query, 5)).ToList();
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }
}
