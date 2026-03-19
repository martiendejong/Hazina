# Advanced Scenarios with Hazina

**Complex use cases, patterns, and best practices for production AI applications**

## Table of Contents

1. [Multi-Provider Strategies](#multi-provider-strategies)
2. [High-Confidence RAG with Neurochain](#high-confidence-rag-with-neurochain)
3. [Tool Provider System](#tool-provider-system)
4. [Custom Vector Stores](#custom-vector-stores)
5. [Context Engineering](#context-engineering)
6. [Fault Detection and Recovery](#fault-detection-and-recovery)
7. [Cost Optimization Patterns](#cost-optimization-patterns)
8. [Multi-Agent Architectures](#multi-agent-architectures)
9. [Hybrid Search Strategies](#hybrid-search-strategies)
10. [Production Monitoring and Observability](#production-monitoring-and-observability)

---

## Multi-Provider Strategies

### Dynamic Provider Selection Based on Task Complexity

```csharp
public class AdaptiveProviderOrchestrator
{
    private readonly ProviderOrchestrator _orchestrator;
    private readonly Dictionary<string, ILLMClient> _providers;

    public AdaptiveProviderOrchestrator(
        Dictionary<string, ILLMClient> providers)
    {
        _orchestrator = new ProviderOrchestrator();
        _providers = providers;

        foreach (var (name, client) in providers)
        {
            _orchestrator.AddProvider(name, client);
        }
    }

    public async Task<LLMResponse<string>> GetResponseAsync(
        List<HazinaChatMessage> messages,
        TaskComplexity complexity)
    {
        // Route based on complexity
        string provider = complexity switch
        {
            TaskComplexity.Simple => "fast-model",      // GPT-3.5 Turbo
            TaskComplexity.Moderate => "balanced-model", // GPT-4
            TaskComplexity.Complex => "advanced-model",  // GPT-4-Turbo
            TaskComplexity.Critical => "premium-model",  // Claude Opus
            _ => "balanced-model"
        };

        _orchestrator.SetDefaultProvider(provider);
        return await _orchestrator.GetResponse(messages);
    }

    public TaskComplexity AnalyzeTaskComplexity(string prompt)
    {
        // Heuristic-based complexity analysis
        var wordCount = prompt.Split(' ').Length;
        var hasCodeBlock = prompt.Contains("```");
        var hasMultipleQuestions = prompt.Count(c => c == '?') > 1;

        if (wordCount < 20 && !hasCodeBlock)
            return TaskComplexity.Simple;

        if (wordCount > 200 || hasCodeBlock || hasMultipleQuestions)
            return TaskComplexity.Complex;

        return TaskComplexity.Moderate;
    }
}

public enum TaskComplexity
{
    Simple,
    Moderate,
    Complex,
    Critical
}
```

### Multi-Region Failover with Latency Optimization

```csharp
public class GeoDistributedOrchestrator
{
    private readonly Dictionary<string, (ILLMClient client, string region)> _regionalClients;
    private readonly string _preferredRegion;

    public GeoDistributedOrchestrator(
        string preferredRegion = "us-east")
    {
        _preferredRegion = preferredRegion;
        _regionalClients = new();
    }

    public void AddRegionalProvider(
        string name,
        ILLMClient client,
        string region)
    {
        _regionalClients[name] = (client, region);
    }

    public async Task<LLMResponse<string>> GetResponseWithLatencyOptimization(
        List<HazinaChatMessage> messages)
    {
        // Try preferred region first
        var preferredProviders = _regionalClients
            .Where(p => p.Value.region == _preferredRegion)
            .ToList();

        foreach (var (name, (client, region)) in preferredProviders)
        {
            try
            {
                var response = await client.GetResponse(
                    messages,
                    HazinaChatResponseFormat.Text,
                    null,
                    null,
                    CancellationToken.None);

                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Provider {name} in {region} failed: {ex.Message}");
            }
        }

        // Fallback to other regions
        foreach (var (name, (client, region)) in _regionalClients
            .Where(p => p.Value.region != _preferredRegion))
        {
            try
            {
                return await client.GetResponse(
                    messages,
                    HazinaChatResponseFormat.Text,
                    null,
                    null,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fallback provider {name} in {region} failed: {ex.Message}");
            }
        }

        throw new Exception("All providers across all regions failed");
    }
}
```

### Cost-Aware Provider Selection with Budget Management

```csharp
public class BudgetManagedOrchestrator
{
    private readonly Dictionary<string, (ILLMClient client, decimal costPerToken)> _providers;
    private decimal _remainingBudget;
    private decimal _totalSpent;

    public BudgetManagedOrchestrator(decimal dailyBudget)
    {
        _remainingBudget = dailyBudget;
        _providers = new();
    }

    public void AddProviderWithCost(
        string name,
        ILLMClient client,
        decimal costPerToken)
    {
        _providers[name] = (client, costPerToken);
    }

    public async Task<LLMResponse<string>> GetResponseWithBudget(
        List<HazinaChatMessage> messages,
        int estimatedTokens = 1000)
    {
        // Find cheapest provider that fits budget
        var affordableProviders = _providers
            .Where(p => p.Value.costPerToken * estimatedTokens <= _remainingBudget)
            .OrderBy(p => p.Value.costPerToken)
            .ToList();

        if (!affordableProviders.Any())
        {
            throw new Exception($"No providers available within budget. " +
                $"Remaining: ${_remainingBudget}, Required: ~${_providers.Min(p => p.Value.costPerToken * estimatedTokens)}");
        }

        foreach (var (name, (client, costPerToken)) in affordableProviders)
        {
            try
            {
                var response = await client.GetResponse(
                    messages,
                    HazinaChatResponseFormat.Text,
                    null,
                    null,
                    CancellationToken.None);

                // Track actual cost
                var actualCost = response.TokenUsage.TotalTokens * costPerToken;
                _totalSpent += actualCost;
                _remainingBudget -= actualCost;

                Console.WriteLine($"Used {name}: ${actualCost:F4}, Remaining: ${_remainingBudget:F2}");

                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Provider {name} failed: {ex.Message}");
            }
        }

        throw new Exception("All affordable providers failed");
    }

    public (decimal spent, decimal remaining, decimal budgetUsedPercent) GetBudgetStatus()
    {
        var total = _totalSpent + _remainingBudget;
        var percentUsed = total > 0 ? (_totalSpent / total) * 100 : 0;
        return (_totalSpent, _remainingBudget, percentUsed);
    }
}
```

---

## High-Confidence RAG with Neurochain

### Multi-Layer Reasoning for Critical Decisions

```csharp
public class CriticalDecisionRAG
{
    private readonly RAGEngine _rag;
    private readonly NeuroChainOrchestrator _neurochain;

    public CriticalDecisionRAG(
        ILLMClient llm,
        IVectorStore vectorStore)
    {
        _neurochain = new NeuroChainOrchestrator();

        // Layer 1: Fast initial analysis
        _neurochain.AddLayer(new FastReasoningLayer(llm)
        {
            Temperature = 0.3,
            MaxTokens = 500
        });

        // Layer 2: Deep analysis
        _neurochain.AddLayer(new DeepReasoningLayer(llm)
        {
            Temperature = 0.1,
            MaxTokens = 2000
        });

        // Layer 3: Cross-validation
        _neurochain.AddLayer(new VerificationLayer(llm)
        {
            Temperature = 0.0,  // Deterministic
            RequireEvidence = true
        });

        // Layer 4: Consensus check
        _neurochain.AddLayer(new ConsensusLayer(llm)
        {
            MinAgreementThreshold = 0.85
        });

        _rag = new RAGEngine(llm, vectorStore, _neurochain);
    }

    public async Task<CriticalDecisionResult> MakeCriticalDecision(
        string question,
        List<Document> documents)
    {
        // Index documents
        await _rag.IndexDocumentsAsync(documents);

        // Query with maximum safety
        var response = await _rag.QueryAsync(question, new RAGQueryOptions
        {
            TopK = 20,                   // Retrieve more context
            MinSimilarity = 0.85,        // High similarity required
            UseNeurochain = true,        // Multi-layer reasoning
            MinConfidence = 0.95,        // 95% confidence minimum
            RequireCitation = true,      // Must cite sources
            MaxContextLength = 8000      // More context for better accuracy
        });

        // Validate decision
        if (response.FinalConfidence < 0.95m)
        {
            return new CriticalDecisionResult
            {
                Decision = null,
                Confidence = response.FinalConfidence,
                Reason = "Insufficient confidence for critical decision",
                RequiresHumanReview = true
            };
        }

        // Check for conflicting evidence
        var conflictScore = AnalyzeConflicts(response.RetrievedDocuments);
        if (conflictScore > 0.3)
        {
            return new CriticalDecisionResult
            {
                Decision = response.Answer,
                Confidence = response.FinalConfidence,
                Reason = "Conflicting evidence detected",
                RequiresHumanReview = true,
                ConflictDetails = GetConflictDetails(response.RetrievedDocuments)
            };
        }

        return new CriticalDecisionResult
        {
            Decision = response.Answer,
            Confidence = response.FinalConfidence,
            Reason = "High confidence with supporting evidence",
            RequiresHumanReview = false,
            Citations = response.RetrievedDocuments
                .Select(d => d.Metadata["source"].ToString())
                .Distinct()
                .ToList()
        };
    }

    private double AnalyzeConflicts(List<Document> documents)
    {
        // Analyze semantic similarity between retrieved documents
        // High similarity = consistent evidence, low similarity = conflicts
        // This is a simplified version - production would use embeddings
        return 0.0;
    }

    private string GetConflictDetails(List<Document> documents)
    {
        return "Conflict analysis details...";
    }
}

public class CriticalDecisionResult
{
    public string? Decision { get; set; }
    public decimal Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool RequiresHumanReview { get; set; }
    public List<string> Citations { get; set; } = new();
    public string? ConflictDetails { get; set; }
}
```

### Incremental Context Refinement

```csharp
public class IncrementalRAG
{
    private readonly RAGEngine _rag;

    public async Task<string> QueryWithIncrementalRefinement(
        string question,
        int maxIterations = 3)
    {
        var currentAnswer = "";
        var confidence = 0.0m;
        var iteration = 0;

        while (iteration < maxIterations && confidence < 0.95m)
        {
            iteration++;

            // Adjust retrieval parameters based on previous confidence
            var topK = iteration == 1 ? 5 : 10 + (iteration * 5);
            var minSimilarity = Math.Max(0.6, 0.8 - (iteration * 0.05));

            var response = await _rag.QueryAsync(question, new RAGQueryOptions
            {
                TopK = topK,
                MinSimilarity = minSimilarity,
                PreviousAnswer = currentAnswer,  // Refine based on previous attempt
                UseNeurochain = iteration > 1    // Use Neurochain on later iterations
            });

            currentAnswer = response.Answer;
            confidence = response.FinalConfidence;

            Console.WriteLine($"Iteration {iteration}: Confidence = {confidence:P0}");

            if (confidence >= 0.95m)
            {
                Console.WriteLine($"High confidence achieved in {iteration} iterations");
                break;
            }

            // If confidence not improving, stop
            if (iteration > 1 && confidence < 0.7m)
            {
                Console.WriteLine("Confidence not improving, stopping refinement");
                break;
            }
        }

        return currentAnswer;
    }
}
```

---

## Tool Provider System

### Creating Custom Tools with Validation

```csharp
public class WeatherTool : IToolProvider
{
    public string Name => "weather";
    public string Description => "Get current weather for a location";

    public ToolSchema GetSchema()
    {
        return new ToolSchema
        {
            Name = Name,
            Description = Description,
            Parameters = new Dictionary<string, ToolParameter>
            {
                ["location"] = new()
                {
                    Type = "string",
                    Description = "City name or coordinates",
                    Required = true
                },
                ["units"] = new()
                {
                    Type = "string",
                    Description = "Temperature units (celsius/fahrenheit)",
                    Required = false,
                    Default = "celsius",
                    Enum = new[] { "celsius", "fahrenheit" }
                }
            }
        };
    }

    public async Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        // Validate required parameters
        if (!parameters.ContainsKey("location"))
        {
            return ToolResult.Error("Missing required parameter: location");
        }

        var location = parameters["location"].ToString();
        var units = parameters.GetValueOrDefault("units", "celsius").ToString();

        // Validate enum
        if (units != "celsius" && units != "fahrenheit")
        {
            return ToolResult.Error($"Invalid units: {units}. Must be celsius or fahrenheit");
        }

        try
        {
            // Call weather API
            var weather = await FetchWeatherAsync(location, units, cancellationToken);

            return ToolResult.Success(new
            {
                location = weather.Location,
                temperature = weather.Temperature,
                units = units,
                condition = weather.Condition,
                humidity = weather.Humidity,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Weather API error: {ex.Message}");
        }
    }

    private async Task<WeatherData> FetchWeatherAsync(
        string location,
        string units,
        CancellationToken cancellationToken)
    {
        // Implementation: Call external weather API
        await Task.Delay(100, cancellationToken);
        return new WeatherData
        {
            Location = location,
            Temperature = 22.5,
            Condition = "Partly cloudy",
            Humidity = 65
        };
    }
}

public class WeatherData
{
    public string Location { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public string Condition { get; set; } = string.Empty;
    public int Humidity { get; set; }
}
```

### Tool Sets and Guarded Execution

```csharp
public class GuardedToolExecutor
{
    private readonly Dictionary<string, IToolProvider> _tools;
    private readonly List<IToolGuard> _guards;

    public GuardedToolExecutor()
    {
        _tools = new();
        _guards = new()
        {
            new RateLimitGuard(maxCallsPerMinute: 60),
            new CostLimitGuard(maxCostPerCall: 0.10m),
            new DangerousOperationGuard()
        };
    }

    public void RegisterTool(IToolProvider tool)
    {
        _tools[tool.Name] = tool;
    }

    public async Task<ToolResult> ExecuteToolSafely(
        string toolName,
        Dictionary<string, object> parameters)
    {
        if (!_tools.ContainsKey(toolName))
        {
            return ToolResult.Error($"Tool not found: {toolName}");
        }

        var tool = _tools[toolName];

        // Run guards before execution
        foreach (var guard in _guards)
        {
            var guardResult = await guard.CheckAsync(toolName, parameters);
            if (!guardResult.Allowed)
            {
                return ToolResult.Error($"Guard blocked execution: {guardResult.Reason}");
            }
        }

        // Execute with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var result = await tool.ExecuteAsync(parameters, cts.Token);

            // Log execution
            await LogToolExecution(toolName, parameters, result);

            return result;
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Error($"Tool execution timed out after 30 seconds");
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Tool execution failed: {ex.Message}");
        }
    }

    private async Task LogToolExecution(
        string toolName,
        Dictionary<string, object> parameters,
        ToolResult result)
    {
        // Log to monitoring system
        await Task.CompletedTask;
    }
}

public interface IToolGuard
{
    Task<GuardResult> CheckAsync(string toolName, Dictionary<string, object> parameters);
}

public class RateLimitGuard : IToolGuard
{
    private readonly Dictionary<string, Queue<DateTime>> _callHistory = new();
    private readonly int _maxCallsPerMinute;

    public RateLimitGuard(int maxCallsPerMinute)
    {
        _maxCallsPerMinute = maxCallsPerMinute;
    }

    public Task<GuardResult> CheckAsync(string toolName, Dictionary<string, object> parameters)
    {
        if (!_callHistory.ContainsKey(toolName))
        {
            _callHistory[toolName] = new Queue<DateTime>();
        }

        var history = _callHistory[toolName];
        var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);

        // Remove old entries
        while (history.Count > 0 && history.Peek() < oneMinuteAgo)
        {
            history.Dequeue();
        }

        if (history.Count >= _maxCallsPerMinute)
        {
            return Task.FromResult(new GuardResult
            {
                Allowed = false,
                Reason = $"Rate limit exceeded: {_maxCallsPerMinute} calls/minute"
            });
        }

        history.Enqueue(DateTime.UtcNow);

        return Task.FromResult(new GuardResult { Allowed = true });
    }
}

public class GuardResult
{
    public bool Allowed { get; set; }
    public string Reason { get; set; } = string.Empty;
}
```

---

## Custom Vector Stores

### Implementing a Custom Vector Store

```csharp
public class RedisVectorStore : IVectorStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _indexName;

    public RedisVectorStore(string connectionString, string indexName = "hazina_vectors")
    {
        _redis = ConnectionMultiplexer.Connect(connectionString);
        _indexName = indexName;
    }

    public async Task AddAsync(
        string id,
        float[] embedding,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();

        // Store embedding as binary blob
        var embeddingBytes = ConvertToBytes(embedding);

        // Store metadata as hash
        var hashEntries = metadata
            .Select(kvp => new HashEntry(kvp.Key, JsonSerializer.Serialize(kvp.Value)))
            .Append(new HashEntry("embedding", embeddingBytes))
            .ToArray();

        await db.HashSetAsync($"{_indexName}:{id}", hashEntries);

        // Add to search index
        await db.SetAddAsync($"{_indexName}:ids", id);
    }

    public async Task<List<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();

        // Get all IDs
        var ids = await db.SetMembersAsync($"{_indexName}:ids");

        var results = new List<(string id, float similarity, Dictionary<string, object> metadata)>();

        foreach (var id in ids)
        {
            var hash = await db.HashGetAllAsync($"{_indexName}:{id}");
            var embeddingBytes = (byte[])hash.FirstOrDefault(h => h.Name == "embedding").Value;

            if (embeddingBytes == null) continue;

            var embedding = ConvertFromBytes(embeddingBytes);
            var similarity = CosineSimilarity(queryEmbedding, embedding);

            var metadata = hash
                .Where(h => h.Name != "embedding")
                .ToDictionary(
                    h => h.Name.ToString(),
                    h => JsonSerializer.Deserialize<object>(h.Value!)!
                );

            results.Add((id.ToString(), similarity, metadata));
        }

        return results
            .OrderByDescending(r => r.similarity)
            .Take(topK)
            .Select(r => new VectorSearchResult
            {
                Id = r.id,
                Similarity = r.similarity,
                Metadata = r.metadata
            })
            .ToList();
    }

    private byte[] ConvertToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private float[] ConvertFromBytes(byte[] bytes)
    {
        var embedding = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, embedding, 0, bytes.Length);
        return embedding;
    }

    private float CosineSimilarity(float[] a, float[] b)
    {
        var dotProduct = a.Zip(b, (x, y) => x * y).Sum();
        var magnitudeA = Math.Sqrt(a.Sum(x => x * x));
        var magnitudeB = Math.Sqrt(b.Sum(x => x * x));
        return (float)(dotProduct / (magnitudeA * magnitudeB));
    }
}
```

---

## Context Engineering

### Dynamic Context Window Management

```csharp
public class DynamicContextManager
{
    private readonly int _maxContextTokens;
    private readonly ITokenCounter _tokenCounter;

    public DynamicContextManager(int maxContextTokens = 8000)
    {
        _maxContextTokens = maxContextTokens;
        _tokenCounter = new TokenCounter();
    }

    public async Task<List<HazinaChatMessage>> BuildOptimalContext(
        List<HazinaChatMessage> conversationHistory,
        List<Document> retrievedDocuments,
        string systemPrompt)
    {
        var messages = new List<HazinaChatMessage>();

        // Always include system message
        messages.Add(new HazinaChatMessage
        {
            Role = HazinaMessageRole.System,
            Text = systemPrompt
        });

        var currentTokens = _tokenCounter.CountTokens(systemPrompt);

        // Add most recent user message
        var lastUserMessage = conversationHistory.LastOrDefault(m => m.Role == HazinaMessageRole.User);
        if (lastUserMessage != null)
        {
            messages.Add(lastUserMessage);
            currentTokens += _tokenCounter.CountTokens(lastUserMessage.Text);
        }

        // Add retrieved documents (prioritized by relevance)
        var sortedDocs = retrievedDocuments.OrderByDescending(d => d.Similarity).ToList();
        var documentsContext = new StringBuilder();

        foreach (var doc in sortedDocs)
        {
            var docTokens = _tokenCounter.CountTokens(doc.Content);

            if (currentTokens + docTokens > _maxContextTokens * 0.6) // Reserve 40% for conversation
                break;

            documentsContext.AppendLine($"[Document {doc.Id}]");
            documentsContext.AppendLine(doc.Content);
            documentsContext.AppendLine();

            currentTokens += docTokens;
        }

        if (documentsContext.Length > 0)
        {
            messages.Insert(1, new HazinaChatMessage
            {
                Role = HazinaMessageRole.System,
                Text = $"Relevant context:\n\n{documentsContext}"
            });
        }

        // Add recent conversation history (sliding window)
        var remainingTokens = _maxContextTokens - currentTokens;
        var conversationToInclude = new List<HazinaChatMessage>();

        for (int i = conversationHistory.Count - 2; i >= 0; i--) // -2 to skip last user message (already added)
        {
            var msg = conversationHistory[i];
            var msgTokens = _tokenCounter.CountTokens(msg.Text);

            if (msgTokens > remainingTokens)
                break;

            conversationToInclude.Insert(0, msg);
            remainingTokens -= msgTokens;
        }

        messages.AddRange(conversationToInclude);

        return messages;
    }
}

public interface ITokenCounter
{
    int CountTokens(string text);
}

public class TokenCounter : ITokenCounter
{
    public int CountTokens(string text)
    {
        // Simplified: ~4 characters per token
        return text.Length / 4;
    }
}
```

---

## Fault Detection and Recovery

### Circuit Breaker Pattern for Provider Failures

```csharp
public class CircuitBreakerOrchestrator
{
    private readonly Dictionary<string, CircuitBreaker> _circuitBreakers;
    private readonly ProviderOrchestrator _orchestrator;

    public CircuitBreakerOrchestrator(ProviderOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        _circuitBreakers = new();
    }

    public void AddProvider(string name, ILLMClient client)
    {
        _orchestrator.AddProvider(name, client);
        _circuitBreakers[name] = new CircuitBreaker(
            failureThreshold: 5,
            timeout: TimeSpan.FromMinutes(2)
        );
    }

    public async Task<LLMResponse<string>> GetResponseWithCircuitBreaker(
        List<HazinaChatMessage> messages)
    {
        var availableProviders = _circuitBreakers
            .Where(cb => cb.Value.State != CircuitBreakerState.Open)
            .Select(cb => cb.Key)
            .ToList();

        if (!availableProviders.Any())
        {
            throw new Exception("All providers have open circuit breakers");
        }

        foreach (var provider in availableProviders)
        {
            var breaker = _circuitBreakers[provider];

            if (breaker.State == CircuitBreakerState.Open)
                continue;

            try
            {
                _orchestrator.SetDefaultProvider(provider);
                var response = await _orchestrator.GetResponse(messages);

                breaker.RecordSuccess();
                return response;
            }
            catch (Exception ex)
            {
                breaker.RecordFailure();
                Console.WriteLine($"Provider {provider} failed (Circuit: {breaker.State}): {ex.Message}");

                if (breaker.State == CircuitBreakerState.Open)
                {
                    Console.WriteLine($"Circuit breaker opened for {provider}");
                }
            }
        }

        throw new Exception("All available providers failed");
    }
}

public class CircuitBreaker
{
    private int _failureCount;
    private readonly int _failureThreshold;
    private readonly TimeSpan _timeout;
    private DateTime? _openedAt;

    public CircuitBreakerState State { get; private set; }

    public CircuitBreaker(int failureThreshold, TimeSpan timeout)
    {
        _failureThreshold = failureThreshold;
        _timeout = timeout;
        State = CircuitBreakerState.Closed;
    }

    public void RecordSuccess()
    {
        _failureCount = 0;
        State = CircuitBreakerState.Closed;
        _openedAt = null;
    }

    public void RecordFailure()
    {
        _failureCount++;

        if (_failureCount >= _failureThreshold)
        {
            State = CircuitBreakerState.Open;
            _openedAt = DateTime.UtcNow;
        }
    }

    public void TryReset()
    {
        if (State == CircuitBreakerState.Open &&
            _openedAt.HasValue &&
            DateTime.UtcNow - _openedAt.Value > _timeout)
        {
            State = CircuitBreakerState.HalfOpen;
            _failureCount = 0;
        }
    }
}

public enum CircuitBreakerState
{
    Closed,    // Normal operation
    Open,      // Failures exceeded threshold, blocking requests
    HalfOpen   // Testing if service recovered
}
```

---

## Cost Optimization Patterns

### Intelligent Caching Strategy

```csharp
public class IntelligentCacheOrchestrator
{
    private readonly ProviderOrchestrator _orchestrator;
    private readonly IMemoryCache _cache;
    private readonly ICacheKeyGenerator _keyGenerator;

    public IntelligentCacheOrchestrator(
        ProviderOrchestrator orchestrator,
        IMemoryCache cache)
    {
        _orchestrator = orchestrator;
        _cache = cache;
        _keyGenerator = new SemanticCacheKeyGenerator();
    }

    public async Task<LLMResponse<string>> GetResponseWithCache(
        List<HazinaChatMessage> messages,
        CacheStrategy strategy = CacheStrategy.Semantic)
    {
        var cacheKey = strategy switch
        {
            CacheStrategy.Exact => GenerateExactKey(messages),
            CacheStrategy.Semantic => await _keyGenerator.GenerateSemanticKeyAsync(messages),
            CacheStrategy.Aggressive => GenerateAggressiveKey(messages),
            _ => GenerateExactKey(messages)
        };

        // Try cache first
        if (_cache.TryGetValue(cacheKey, out LLMResponse<string> cachedResponse))
        {
            Console.WriteLine($"Cache hit! Saved API call (${EstimateCost(messages):F4})");
            return cachedResponse;
        }

        // Cache miss - call API
        var response = await _orchestrator.GetResponse(messages);

        // Cache with appropriate expiration
        var cacheExpiration = strategy switch
        {
            CacheStrategy.Exact => TimeSpan.FromHours(24),
            CacheStrategy.Semantic => TimeSpan.FromHours(6),
            CacheStrategy.Aggressive => TimeSpan.FromHours(1),
            _ => TimeSpan.FromHours(12)
        };

        _cache.Set(cacheKey, response, cacheExpiration);

        return response;
    }

    private string GenerateExactKey(List<HazinaChatMessage> messages)
    {
        var combined = string.Join("|", messages.Select(m => $"{m.Role}:{m.Text}"));
        return ComputeSHA256Hash(combined);
    }

    private string GenerateAggressiveKey(List<HazinaChatMessage> messages)
    {
        // Only use last user message
        var lastUserMessage = messages.LastOrDefault(m => m.Role == HazinaMessageRole.User);
        return ComputeSHA256Hash(lastUserMessage?.Text ?? "");
    }

    private string ComputeSHA256Hash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private decimal EstimateCost(List<HazinaChatMessage> messages)
    {
        var totalTokens = messages.Sum(m => m.Text.Length / 4);
        return totalTokens * 0.00002m; // GPT-4 pricing estimate
    }
}

public enum CacheStrategy
{
    Exact,      // Cache exact message sequence
    Semantic,   // Cache semantically similar queries
    Aggressive  // Cache based on last user message only
}

public interface ICacheKeyGenerator
{
    Task<string> GenerateSemanticKeyAsync(List<HazinaChatMessage> messages);
}

public class SemanticCacheKeyGenerator : ICacheKeyGenerator
{
    public async Task<string> GenerateSemanticKeyAsync(List<HazinaChatMessage> messages)
    {
        // In production: generate embedding and find nearest neighbor
        // Simplified version: use last user message + normalize
        var lastUserMessage = messages.LastOrDefault(m => m.Role == HazinaMessageRole.User);
        if (lastUserMessage == null) return string.Empty;

        var normalized = NormalizeQuery(lastUserMessage.Text);
        return ComputeHash(normalized);
    }

    private string NormalizeQuery(string query)
    {
        // Remove punctuation, lowercase, trim
        return query.ToLowerInvariant()
            .Trim()
            .Replace("?", "")
            .Replace("!", "")
            .Replace(".", "");
    }

    private string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
```

---

## Multi-Agent Architectures

### Hierarchical Agent System

```csharp
public class HierarchicalAgentSystem
{
    private readonly Agent _supervisor;
    private readonly Dictionary<string, Agent> _specialists;

    public HierarchicalAgentSystem(ILLMClient llm)
    {
        // Supervisor agent coordinates specialists
        _supervisor = new Agent(
            "Supervisor",
            "Coordinates specialist agents and synthesizes results",
            llm
        );

        // Specialist agents
        _specialists = new Dictionary<string, Agent>
        {
            ["research"] = new Agent("Researcher", "Researches topics thoroughly", llm),
            ["writer"] = new Agent("Writer", "Writes clear, engaging content", llm),
            ["critic"] = new Agent("Critic", "Reviews and critiques work", llm),
            ["editor"] = new Agent("Editor", "Edits and polishes final output", llm)
        };
    }

    public async Task<string> ExecuteHierarchicalTask(string task)
    {
        // Step 1: Supervisor plans the work
        var plan = await _supervisor.ExecuteAsync($@"
            Task: {task}

            Create a plan delegating work to these specialists:
            - Researcher: Gather information
            - Writer: Create content
            - Critic: Review quality
            - Editor: Polish final output

            Output a JSON plan with steps.");

        // Step 2: Execute specialist work in sequence
        var researchResult = await _specialists["research"].ExecuteAsync(
            $"Research this topic: {task}");

        var draftResult = await _specialists["writer"].ExecuteAsync(
            $"Write content based on research:\n\n{researchResult.Result}");

        var critiqueResult = await _specialists["critic"].ExecuteAsync(
            $"Critique this draft:\n\n{draftResult.Result}");

        var finalResult = await _specialists["editor"].ExecuteAsync($@"
            Original draft:
            {draftResult.Result}

            Critique:
            {critiqueResult.Result}

            Edit the draft addressing the critique.");

        // Step 3: Supervisor synthesizes
        var synthesis = await _supervisor.ExecuteAsync($@"
            Task: {task}

            Specialist outputs:
            Research: {researchResult.Result}
            Draft: {draftResult.Result}
            Critique: {critiqueResult.Result}
            Final: {finalResult.Result}

            Synthesize the final output.");

        return synthesis.Result;
    }
}
```

---

## Production Monitoring and Observability

### Comprehensive Monitoring System

```csharp
public class ProductionMonitoringSystem
{
    private readonly ILogger _logger;
    private readonly IMetricsCollector _metrics;
    private readonly ProviderOrchestrator _orchestrator;

    public ProductionMonitoringSystem(
        ProviderOrchestrator orchestrator,
        ILogger logger,
        IMetricsCollector metrics)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _metrics = metrics;

        // Subscribe to events
        _orchestrator.OnProviderCall += LogProviderCall;
        _orchestrator.OnProviderFailure += LogProviderFailure;
        _orchestrator.OnCostIncurred += TrackCost;
    }

    private void LogProviderCall(object? sender, ProviderCallEventArgs e)
    {
        _logger.LogInformation($"Provider call: {e.ProviderName}, Tokens: {e.TokensUsed}, Latency: {e.Latency}ms");

        _metrics.Increment("llm.calls.total", tags: new[] { $"provider:{e.ProviderName}" });
        _metrics.Histogram("llm.latency", e.Latency, tags: new[] { $"provider:{e.ProviderName}" });
        _metrics.Increment("llm.tokens.total", e.TokensUsed, tags: new[] { $"provider:{e.ProviderName}" });
    }

    private void LogProviderFailure(object? sender, ProviderFailureEventArgs e)
    {
        _logger.LogError($"Provider failure: {e.ProviderName}, Error: {e.Error}");

        _metrics.Increment("llm.errors.total", tags: new[]
        {
            $"provider:{e.ProviderName}",
            $"error_type:{e.ErrorType}"
        });
    }

    private void TrackCost(object? sender, CostEventArgs e)
    {
        _metrics.Gauge("llm.cost.current", e.CurrentCost);
        _metrics.Gauge("llm.cost.daily", e.DailyCost);

        if (e.BudgetUsedPercent > 80)
        {
            _logger.LogWarning($"Budget {e.BudgetUsedPercent:F1}% used!");
        }
    }
}

public interface IMetricsCollector
{
    void Increment(string metric, long value = 1, string[]? tags = null);
    void Histogram(string metric, double value, string[]? tags = null);
    void Gauge(string metric, double value, string[]? tags = null);
}
```

---

## Best Practices Summary

1. **Multi-Provider**: Always configure fallback providers for production
2. **Caching**: Use semantic caching to reduce costs by 60-80%
3. **Circuit Breakers**: Implement circuit breakers to handle provider outages gracefully
4. **Monitoring**: Track latency, costs, and errors in production
5. **Context Management**: Dynamically manage context windows to optimize token usage
6. **Tool Validation**: Always validate tool parameters and implement guards
7. **Budget Management**: Set daily budgets and monitor in real-time
8. **High Confidence**: Use Neurochain for critical decisions requiring 95%+ confidence
9. **Incremental Refinement**: Use iterative approaches for complex queries
10. **Hierarchical Agents**: Coordinate specialist agents with a supervisor for complex tasks

---

## Next Steps

- [Getting Started](GETTING_STARTED.md) - Basic setup and concepts
- [RAG Guide](RAG_GUIDE.md) - Complete RAG documentation
- [Agents Guide](AGENTS_GUIDE.md) - Agent systems and workflows
- [Production Monitoring](PRODUCTION_MONITORING_GUIDE.md) - Observability and metrics
- [API Reference](apidoc/api/index.html) - Complete API documentation

**Ready to build advanced AI applications? Start with a scenario above and adapt it to your needs!**
