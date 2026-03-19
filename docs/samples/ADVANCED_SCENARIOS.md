# Advanced Scenarios

This guide covers advanced usage patterns for the Hazina AI Framework.

## Custom LLM Providers

### Implementing a Custom Provider

```csharp
using Hazina.AI.Core;
using System.Runtime.CompilerServices;

public class CustomLLMProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly CustomLLMConfig _config;

    public CustomLLMProvider(CustomLLMConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.BaseUrl),
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };
    }

    public async Task<CompletionResponse> CompletionAsync(
        string prompt,
        CompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/completions")
        {
            Content = JsonContent.Create(new
            {
                prompt,
                temperature = options?.Temperature ?? _config.DefaultTemperature,
                max_tokens = options?.MaxTokens ?? _config.DefaultMaxTokens
            })
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CustomLLMResponse>(
            cancellationToken: cancellationToken
        );

        return new CompletionResponse
        {
            Text = result.Choices[0].Text,
            TokensUsed = result.Usage.TotalTokens,
            Model = result.Model
        };
    }

    public async IAsyncEnumerable<StreamChunk> StreamCompletionAsync(
        string prompt,
        CompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/completions")
        {
            Content = JsonContent.Create(new
            {
                prompt,
                stream = true,
                temperature = options?.Temperature ?? _config.DefaultTemperature
            })
        };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                continue;

            var json = line.Substring(6); // Remove "data: " prefix
            if (json == "[DONE]")
                break;

            var chunk = JsonSerializer.Deserialize<CustomLLMStreamChunk>(json);
            yield return new StreamChunk
            {
                Delta = chunk.Choices[0].Delta.Content,
                TokensUsed = chunk.Usage?.TotalTokens
            };
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

public record CustomLLMConfig
{
    public required string BaseUrl { get; init; }
    public required string ApiKey { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public double DefaultTemperature { get; init; } = 0.7;
    public int DefaultMaxTokens { get; init; } = 1000;
}
```

## Custom Embedding Stores

### Implementing a PostgreSQL Embedding Store

```csharp
using Hazina.Store.EmbeddingStore;
using Npgsql;
using Pgvector.Npgsql;

public class PostgresEmbeddingStore : IEmbeddingStore
{
    private readonly string _connectionString;
    private readonly int _dimensions;

    public PostgresEmbeddingStore(string connectionString, int dimensions)
    {
        _connectionString = connectionString;
        _dimensions = dimensions;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Create extension
        await using var cmd1 = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", conn);
        await cmd1.ExecuteNonQueryAsync(ct);

        // Create table
        await using var cmd2 = new NpgsqlCommand($@"
            CREATE TABLE IF NOT EXISTS embeddings (
                id TEXT PRIMARY KEY,
                embedding vector({_dimensions}),
                metadata JSONB,
                created_at TIMESTAMP DEFAULT NOW()
            )", conn);
        await cmd2.ExecuteNonQueryAsync(ct);

        // Create index
        await using var cmd3 = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS embeddings_vector_idx
            ON embeddings USING ivfflat (embedding vector_cosine_ops)
            WITH (lists = 100)", conn);
        await cmd3.ExecuteNonQueryAsync(ct);
    }

    public async Task AddAsync(
        EmbeddingEntry entry,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO embeddings (id, embedding, metadata)
            VALUES ($1, $2, $3)
            ON CONFLICT (id) DO UPDATE
            SET embedding = EXCLUDED.embedding,
                metadata = EXCLUDED.metadata", conn);

        cmd.Parameters.AddWithValue(entry.Id);
        cmd.Parameters.AddWithValue(new Vector(entry.Embedding));
        cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb,
            JsonSerializer.Serialize(entry.Metadata));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ScoredEmbeddingEntry>> SearchAsync(
        float[] queryEmbedding,
        int topK = 10,
        double minScore = 0.0,
        IDictionary<string, object>? filter = null,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT id, embedding, metadata,
                   1 - (embedding <=> $1) AS score
            FROM embeddings
            WHERE 1 - (embedding <=> $1) >= $2";

        if (filter != null && filter.Any())
        {
            sql += " AND metadata @> $3";
        }

        sql += " ORDER BY embedding <=> $1 LIMIT $4";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(new Vector(queryEmbedding));
        cmd.Parameters.AddWithValue(minScore);
        if (filter != null && filter.Any())
        {
            cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb,
                JsonSerializer.Serialize(filter));
        }
        cmd.Parameters.AddWithValue(topK);

        var results = new List<ScoredEmbeddingEntry>();

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ScoredEmbeddingEntry
            {
                Id = reader.GetString(0),
                Embedding = ((Vector)reader.GetValue(1)).ToArray(),
                Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    reader.GetString(2)),
                Score = reader.GetDouble(3)
            });
        }

        return results;
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(
            "DELETE FROM embeddings WHERE id = $1", conn);
        cmd.Parameters.AddWithValue(id);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

## Advanced RAG Patterns

### Multi-Query RAG

```csharp
public class MultiQueryRAG
{
    private readonly ILLMProvider _provider;
    private readonly IEmbeddingStore _store;

    public async Task<RAGResponse> QueryWithMultiplePerspectivesAsync(
        string userQuery,
        CancellationToken ct = default)
    {
        // Generate multiple query variations
        var queryVariations = await GenerateQueryVariationsAsync(userQuery, ct);

        // Search with all variations
        var allResults = new List<ScoredEmbeddingEntry>();
        foreach (var variation in queryVariations)
        {
            var embedding = await _provider.GetEmbeddingAsync(variation, ct);
            var results = await _store.SearchAsync(embedding, topK: 5, ct: ct);
            allResults.AddRange(results);
        }

        // Deduplicate and re-rank
        var uniqueResults = allResults
            .GroupBy(r => r.Id)
            .Select(g => new
            {
                Entry = g.First(),
                Score = g.Average(r => r.Score) // Average score across queries
            })
            .OrderByDescending(r => r.Score)
            .Take(10)
            .ToList();

        // Generate response with context
        var context = string.Join("\n\n", uniqueResults.Select(r =>
            $"[Score: {r.Score:F3}] {r.Entry.Metadata["text"]}"));

        var prompt = $@"
Context:
{context}

Question: {userQuery}

Provide a comprehensive answer based on the context above.";

        var response = await _provider.CompletionAsync(prompt, ct);

        return new RAGResponse
        {
            Answer = response.Text,
            Sources = uniqueResults.Select(r => r.Entry.Id).ToList(),
            Scores = uniqueResults.Select(r => r.Score).ToList()
        };
    }

    private async Task<List<string>> GenerateQueryVariationsAsync(
        string query,
        CancellationToken ct)
    {
        var prompt = $@"
Generate 3 different ways to phrase this question, each from a different perspective:

Original: {query}

Variations:
1.";

        var response = await _provider.CompletionAsync(prompt, ct);

        return response.Text
            .Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.TrimStart('1', '2', '3', '.', ' '))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
    }
}
```

### Hierarchical RAG

```csharp
public class HierarchicalRAG
{
    private readonly ILLMProvider _provider;
    private readonly IEmbeddingStore _chunkStore;
    private readonly IEmbeddingStore _documentStore;

    public async Task<RAGResponse> QueryWithHierarchyAsync(
        string query,
        CancellationToken ct = default)
    {
        // First: Search at document level
        var queryEmbedding = await _provider.GetEmbeddingAsync(query, ct);
        var relevantDocs = await _documentStore.SearchAsync(
            queryEmbedding,
            topK: 5,
            ct: ct
        );

        // Second: Search chunks within relevant documents
        var relevantChunks = new List<ScoredEmbeddingEntry>();
        foreach (var doc in relevantDocs)
        {
            var docId = doc.Id;
            var chunks = await _chunkStore.SearchAsync(
                queryEmbedding,
                topK: 3,
                filter: new Dictionary<string, object> { ["document_id"] = docId },
                ct: ct
            );
            relevantChunks.AddRange(chunks);
        }

        // Re-rank chunks
        var rankedChunks = relevantChunks
            .OrderByDescending(c => c.Score)
            .Take(10)
            .ToList();

        // Generate response
        var context = string.Join("\n\n", rankedChunks.Select(c =>
            $"From: {c.Metadata["document_title"]}\n{c.Metadata["text"]}"));

        var prompt = $@"
Context from multiple documents:
{context}

Question: {query}

Provide a comprehensive answer with citations.";

        var response = await _provider.CompletionAsync(prompt, ct);

        return new RAGResponse
        {
            Answer = response.Text,
            Sources = rankedChunks.Select(c => c.Metadata["document_title"].ToString()).Distinct().ToList(),
            ChunkIds = rankedChunks.Select(c => c.Id).ToList()
        };
    }
}
```

## Multi-Agent Orchestration

### Supervisor-Worker Pattern

```csharp
public class SupervisorWorkerOrchestration
{
    private readonly ILLMProvider _supervisorProvider;
    private readonly Dictionary<string, Agent> _workers;

    public async Task<OrchestratedResponse> ExecuteAsync(
        string task,
        CancellationToken ct = default)
    {
        // Supervisor plans the work
        var plan = await PlanTaskAsync(task, ct);

        // Execute subtasks in parallel
        var subtaskResults = await Task.WhenAll(
            plan.Subtasks.Select(async subtask =>
            {
                var worker = _workers[subtask.WorkerType];
                var result = await worker.ExecuteAsync(subtask.Description, ct);
                return new SubtaskResult
                {
                    Subtask = subtask,
                    Result = result
                };
            })
        );

        // Supervisor synthesizes results
        var synthesis = await SynthesizeResultsAsync(task, subtaskResults, ct);

        return new OrchestratedResponse
        {
            OriginalTask = task,
            Plan = plan,
            Results = subtaskResults.ToList(),
            Synthesis = synthesis
        };
    }

    private async Task<TaskPlan> PlanTaskAsync(string task, CancellationToken ct)
    {
        var availableWorkers = string.Join("\n", _workers.Select(w =>
            $"- {w.Key}: {w.Value.Description}"));

        var prompt = $@"
Available workers:
{availableWorkers}

Task: {task}

Break this task into subtasks and assign each to the most appropriate worker.
Format: JSON array of {{worker: string, description: string}}";

        var response = await _supervisorProvider.CompletionAsync(prompt, ct);

        var subtasks = JsonSerializer.Deserialize<List<Subtask>>(response.Text);

        return new TaskPlan
        {
            OriginalTask = task,
            Subtasks = subtasks
        };
    }

    private async Task<string> SynthesizeResultsAsync(
        string task,
        IEnumerable<SubtaskResult> results,
        CancellationToken ct)
    {
        var resultsText = string.Join("\n\n", results.Select(r =>
            $"Subtask: {r.Subtask.Description}\nResult: {r.Result}"));

        var prompt = $@"
Original task: {task}

Subtask results:
{resultsText}

Synthesize these results into a comprehensive answer to the original task.";

        var response = await _supervisorProvider.CompletionAsync(prompt, ct);
        return response.Text;
    }
}

public record TaskPlan
{
    public required string OriginalTask { get; init; }
    public required List<Subtask> Subtasks { get; init; }
}

public record Subtask
{
    public required string WorkerType { get; init; }
    public required string Description { get; init; }
}

public record SubtaskResult
{
    public required Subtask Subtask { get; init; }
    public required string Result { get; init; }
}
```

## Performance Optimization

### Caching Strategy

```csharp
using Microsoft.Extensions.Caching.Memory;

public class CachedLLMProvider : ILLMProvider
{
    private readonly ILLMProvider _innerProvider;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration;

    public CachedLLMProvider(
        ILLMProvider innerProvider,
        IMemoryCache cache,
        TimeSpan? cacheDuration = null)
    {
        _innerProvider = innerProvider;
        _cache = cache;
        _cacheDuration = cacheDuration ?? TimeSpan.FromHours(1);
    }

    public async Task<CompletionResponse> CompletionAsync(
        string prompt,
        CompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = ComputeCacheKey(prompt, options);

        if (_cache.TryGetValue<CompletionResponse>(cacheKey, out var cached))
        {
            return cached;
        }

        var response = await _innerProvider.CompletionAsync(
            prompt,
            options,
            cancellationToken
        );

        _cache.Set(cacheKey, response, _cacheDuration);

        return response;
    }

    private string ComputeCacheKey(string prompt, CompletionOptions? options)
    {
        var key = $"{prompt}|{options?.Temperature}|{options?.MaxTokens}";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hash);
    }

    // Streaming not cached - always delegate
    public IAsyncEnumerable<StreamChunk> StreamCompletionAsync(
        string prompt,
        CompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _innerProvider.StreamCompletionAsync(prompt, options, cancellationToken);
    }

    public void Dispose() => _innerProvider?.Dispose();
}
```

### Batch Processing

```csharp
public class BatchProcessor
{
    private readonly ILLMProvider _provider;
    private readonly int _batchSize;
    private readonly int _maxConcurrency;

    public async Task<List<BatchResult>> ProcessBatchAsync(
        IEnumerable<string> items,
        Func<string, string> promptBuilder,
        CancellationToken ct = default)
    {
        var results = new ConcurrentBag<BatchResult>();
        var semaphore = new SemaphoreSlim(_maxConcurrency);

        var batches = items
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / _batchSize)
            .Select(g => g.Select(x => x.item).ToList());

        await Parallel.ForEachAsync(
            batches,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _maxConcurrency,
                CancellationToken = ct
            },
            async (batch, token) =>
            {
                await semaphore.WaitAsync(token);
                try
                {
                    foreach (var item in batch)
                    {
                        var prompt = promptBuilder(item);
                        var response = await _provider.CompletionAsync(prompt, ct: token);

                        results.Add(new BatchResult
                        {
                            Input = item,
                            Output = response.Text,
                            TokensUsed = response.TokensUsed
                        });
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }
        );

        return results.OrderBy(r => r.Input).ToList();
    }
}

public record BatchResult
{
    public required string Input { get; init; }
    public required string Output { get; init; }
    public int TokensUsed { get; init; }
}
```

## Testing

### Mocking LLM Providers

```csharp
using Moq;

public class LLMProviderTests
{
    [Fact]
    public async Task TestRAGWithMockedProvider()
    {
        // Arrange
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider
            .Setup(p => p.CompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CompletionOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompletionResponse
            {
                Text = "Mocked response",
                TokensUsed = 10
            });

        mockProvider
            .Setup(p => p.GetEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 1536).Select(_ => 0.1f).ToArray());

        var mockStore = new Mock<IEmbeddingStore>();
        mockStore
            .Setup(s => s.SearchAsync(
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScoredEmbeddingEntry>
            {
                new()
                {
                    Id = "doc1",
                    Score = 0.95,
                    Metadata = new Dictionary<string, object>
                    {
                        ["text"] = "Test document"
                    }
                }
            });

        var rag = new RAGPipeline(mockProvider.Object, mockStore.Object);

        // Act
        var result = await rag.QueryAsync("test query");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Mocked response", result.Response);
        mockProvider.Verify(
            p => p.CompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CompletionOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
```

## See Also

- [Getting Started Guide](./GETTING_STARTED.md)
- [API Stability Guidelines](../API_STABILITY.md)
- [Architecture Documentation](../ARCHITECTURE.md)
