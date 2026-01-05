# Hazina Configuration Showcase

Een uitgebreide demonstratie van alle configureerbare componenten in Hazina:
- Storage backends (File-based, Supabase, PostgreSQL, Hybrid)
- LLM provider strategieën (Priority, Cost, Speed, Failover)
- AI componenten integratie (RAG, Neurochain, Agents)
- Production monitoring (Metrics, Health, Cost tracking)

## Quickstart

### 1. Vereiste Environment Variables

**Minimaal (voor basis functionaliteit):**
```bash
set OPENAI_API_KEY=sk-...
```

**Aanbevolen (voor failover):**
```bash
set OPENAI_API_KEY=sk-...
set ANTHROPIC_API_KEY=sk-ant-...
```

**Optioneel (voor cloud storage):**
```bash
set SUPABASE_URL=https://your-project.supabase.co
set SUPABASE_ANON_KEY=eyJ...
set SUPABASE_CONNECTION_STRING=Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=...
```

### 2. Run de Demo

```bash
cd apps/Demos/Hazina.Demo.ConfigurationShowcase
dotnet run
```

## Features Overzicht

### 1. Storage Backend Configuratie

De demo laat zien hoe je kunt schakelen tussen verschillende storage backends:

#### File-based (Default)
```csharp
var config = new HazinaStoreConfig
{
    ApiSettings = new ApiSettings { OpenApiKey = "sk-..." },
    ProjectSettings = new ProjectSettings
    {
        ProjectsFolder = @"C:\HazinaData"
    }
};

var storeSetup = StoreProvider.GetStoreSetup(config, "my-project", 1536);
```

**Wanneer gebruiken:**
- Lokale ontwikkeling
- Prototyping
- Single-user applicaties
- Geen database beschikbaar

**Voor- en nadelen:**
- ✅ Geen database nodig
- ✅ Eenvoudige setup
- ✅ Portable data
- ❌ Beperkte schaalbaarheid
- ❌ Geen concurrent access

#### Supabase (Cloud PostgreSQL)
```csharp
var config = new HazinaStoreConfig
{
    ApiSettings = new ApiSettings { OpenApiKey = "sk-..." },
    SupabaseSettings = new SupabaseSettings
    {
        Enabled = true,
        Url = "https://your-project.supabase.co",
        AnonKey = "your-anon-key",
        ConnectionString = "Host=db.your-project.supabase.co;..."
    }
};

var storeSetup = StoreProvider.GetStoreSetup(config);
```

**Wanneer gebruiken:**
- productie-omgevingen
- Multi-user applicaties
- Cloud deployments
- Schaalbaarheid vereist

**Voor- en nadelen:**
- ✅ Managed service
- ✅ Automatische backups
- ✅ Built-in auth
- ✅ pgvector voor semantic search
- ❌ Cloud account nodig
- ❌ Network dependency

#### PostgreSQL (Self-hosted)
```csharp
var config = new HazinaStoreConfig
{
    ApiSettings = new ApiSettings { OpenApiKey = "sk-..." },
    SupabaseSettings = new SupabaseSettings
    {
        Enabled = true,
        ConnectionString = "Host=localhost;Port=5432;Database=hazina;..."
    }
};
```

**Wanneer gebruiken:**
- Enterprise deployments
- On-premise vereisten
- Data sovereignty
- Volledige controle

**Voor- en nadelen:**
- ✅ Volledige controle
- ✅ Geen vendor lock-in
- ✅ Custom optimalisatie
- ❌ Self-managed
- ❌ Infrastructuur nodig

#### Hybrid (Local files + Cloud embeddings)
```csharp
var config = new HazinaStoreConfig
{
    ApiSettings = new ApiSettings { OpenApiKey = "sk-..." },
    ProjectSettings = new ProjectSettings
    {
        ProjectsFolder = @"C:\HazinaData"  // Local files
    },
    SupabaseSettings = new SupabaseSettings
    {
        Enabled = true,
        ConnectionString = "..."  // Cloud embeddings
    }
};

// Automatisch hybrid mode
var storeSetup = StoreProvider.GetStoreSetup(config, "my-project", 1536);
```

**Wanneer gebruiken:**
- Snelle lokale file toegang nodig
- Semantic search in de cloud
- Beste van beide werelden

**Voor- en nadelen:**
- ✅ Snelle file access
- ✅ Schaalbare vector search
- ✅ Flexibel
- ❌ Complexere configuratie

### 2. LLM Provider Strategieën

De demo toont 6 verschillende provider selectie strategieën:

#### Priority-based (Aanbevolen voor productie)
```csharp
var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    SelectionStrategy = SelectionStrategy.Priority,
    EnableHealthMonitoring = true,
    HealthCheckIntervalSeconds = 60
});

orchestrator.RegisterProvider("openai", new OpenAIClientWrapper(openAIKey), priority: 1);
orchestrator.RegisterProvider("anthropic", new ClaudeClientWrapper(anthropicKey), priority: 2);
orchestrator.RegisterProvider("ollama", new OllamaClientWrapper("http://localhost:11434"), priority: 3);
```

**Gedrag:**
- Gebruikt altijd de hoogste priority provider die healthy is
- Automatische failover naar volgende priority bij problemen
- Circuit breaker voorkomt cascading failures

**Use cases:**
- Productie met primary + backup providers
- SLA-kritische applicaties
- Predictable behavior gewenst

#### Cost-optimized
```csharp
var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    SelectionStrategy = SelectionStrategy.LeastCost,
    EnableCostTracking = true,
    BudgetLimitUSD = 100.0
});
```

**Gedrag:**
- Selecteert altijd de goedkoopste provider
- Real-time cost tracking
- Budget alerts op 50%, 75%, 90%, 95%
- Stopt automatisch bij budget limiet

**Use cases:**
- Budget-constrained applicaties
- Batch processing
- Development/testing

#### Speed-optimized
```csharp
var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    SelectionStrategy = SelectionStrategy.FastestResponse,
    EnablePerformanceTracking = true
});
```

**Gedrag:**
- Gebruikt historische response times
- Selecteert snelste provider
- Adapteert op basis van real-time performance

**Use cases:**
- Low-latency vereisten
- Real-time chat
- Interactive assistants

#### Failover (Hoge beschikbaarheid)
```csharp
// Quick setup helper
var orchestrator = QuickSetup.SetupWithFailover(openAIKey, anthropicKey);
```

**Gedrag:**
- Primaire provider: OpenAI
- Fallback provider: Anthropic
- Automatische retry met exponential backoff
- Circuit breaker pattern

**Use cases:**
- High availability vereist
- Mission-critical applicaties
- 24/7 beschikbaarheid

#### Round-robin (Load balancing)
```csharp
var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    SelectionStrategy = SelectionStrategy.RoundRobin
});
```

**Gedrag:**
- Distribueert requests gelijkmatig
- Rotatie door alle providers
- Load balancing

**Use cases:**
- Testing meerdere providers
- Load distributie
- Avoiding rate limits

#### Random
```csharp
var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    SelectionStrategy = SelectionStrategy.Random
});
```

**Gedrag:**
- Random provider selectie
- Gelijkmatige distributie over tijd

**Use cases:**
- Testing
- Load balancing zonder predictability

### 3. AI Componenten Integratie

#### RAG (Retrieval-Augmented Generation)
```csharp
var ragEngine = new RAGEngine(orchestrator, storeSetup.VectorStore);

// Index documents
await ragEngine.IndexDocumentAsync("doc1", "Content here...");

// Query with context
var response = await ragEngine.QueryAsync("Your question?");
Console.WriteLine(response.Answer);
Console.WriteLine($"Confidence: {response.Confidence:P0}");
```

**Features:**
- Metadata-first filtering (SQL queries)
- Optional semantic search met embeddings
- Automatic context ranking
- Citation support

#### Neurochain (Multi-layer Reasoning)
```csharp
var neurochain = new NeuroChainOrchestrator(new NeuroChainConfig
{
    ParallelExecution = true,
    EnableCrossValidation = true
});

neurochain.AddLayer(new FastReasoningLayer(orchestrator));
neurochain.AddLayer(new DeepReasoningLayer(orchestrator));
neurochain.AddLayer(new VerificationLayer(orchestrator));

var result = await neurochain.ReasonAsync("Complex question?", new ReasoningContext
{
    MinConfidence = 0.9
});
```

**Features:**
- 3 onafhankelijke reasoning layers
- Cross-validation tussen layers
- Consensus engine
- Parallel execution (60% sneller)
- Early stopping (50-90% cost savings)

#### Combined (RAG + Neurochain)
```csharp
// Retrieve context with RAG
var ragResult = await ragEngine.QueryAsync("Question?");

// Reason with Neurochain using context
var answer = await neurochain.ReasonAsync(
    $"Context: {ragResult.RetrievedChunks.First().Text}\n\nQuestion: {question}",
    new ReasoningContext { MinConfidence = 0.95 }
);
```

**Features:**
- Context-aware reasoning
- Multi-validated answers
- Highest confidence (95-99%)
- Citation + reasoning chain

### 4. Production Monitoring

```csharp
// Enable monitoring in orchestrator
var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    EnableHealthMonitoring = true,
    EnableCostTracking = true,
    EnablePerformanceTracking = true,
    BudgetLimitUSD = 50.0
});

// Custom metrics
var metrics = new MetricsCollector();

using (metrics.TimeOperation("llm_request", tags))
{
    var result = await orchestrator.GetResponse(...);

    metrics.IncrementCounter("requests_total", new Dictionary<string, string>
    {
        ["status"] = "success",
        ["provider"] = "openai"
    });
}

// Export for Prometheus
var prometheusMetrics = metrics.ExportPrometheus();
```

**Features:**
- Health monitoring (Healthy/Degraded/Unhealthy)
- Cost tracking met budget alerts
- Performance metrics (P50, P95, P99)
- Success rate tracking
- Prometheus export

## Configuration Best Practices

### Environment Variables

**Productie setup:**
```bash
# LLM Providers (minimaal 2 voor failover)
OPENAI_API_KEY=sk-...
ANTHROPIC_API_KEY=sk-ant-...

# Storage (Supabase aanbevolen voor productie)
SUPABASE_URL=https://your-project.supabase.co
SUPABASE_ANON_KEY=eyJ...
SUPABASE_CONNECTION_STRING=Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=...

# Optional: Local PostgreSQL
POSTGRES_CONNECTION_STRING=Host=localhost;Port=5432;Database=hazina;Username=hazina;Password=...
```

### Security Checklist

- ✅ Gebruik environment variables, NOOIT hardcoded keys
- ✅ Roteer API keys elke 90 dagen
- ✅ Gebruik service role keys alleen server-side
- ✅ Enable SSL/TLS voor alle connecties
- ✅ Implementeer rate limiting
- ✅ Monitor ongebruikelijke usage patterns
- ✅ Gebruik least-privilege database credentials
- ✅ Backup strategy gedefinieerd

### Productie Checklist

- □ Storage: Supabase of PostgreSQL (niet file-based)
- □ Providers: Minimaal 2 voor failover
- □ Health monitoring: Enabled
- □ Cost tracking: Enabled met budget limits
- □ Circuit breaker: Enabled
- □ Metrics collection: Geïntegreerd
- □ Error handling: Comprehensive logging
- □ Environment variables: Securely managed
- □ API keys: Rotatie proces
- □ Backup: Database en file backups

## Demo Scenarios

### Scenario 1: Startup/MVP
```
Storage: File-based
Provider: OpenAI alleen
Features: Basis RAG
Monitoring: Cost tracking

Reden: Minimale setup, lage kosten, snel starten
```

### Scenario 2: Growing SaaS
```
Storage: Hybrid (Local + Supabase)
Providers: OpenAI + Anthropic (priority)
Features: RAG + Neurochain (fast + deep)
Monitoring: Health + Cost + Performance

Reden: Schaalbaar, betrouwbaar, cost-aware
```

### Scenario 3: Enterprise
```
Storage: PostgreSQL (self-hosted)
Providers: OpenAI + Anthropic + Azure OpenAI (priority)
Features: Full Neurochain (alle layers), RAG, Agents
Monitoring: Complete metrics + alerting

Reden: Data sovereignty, SLA vereisten, full control
```

### Scenario 4: Cost-sensitive
```
Storage: File-based
Providers: OpenAI + Ollama (cost-optimized)
Features: RAG only, early stopping
Monitoring: Strict budget limits ($10)

Reden: Minimale kosten, local inference waar mogelijk
```

## Troubleshooting

### "OPENAI_API_KEY not set"
```bash
set OPENAI_API_KEY=sk-...
```

### "Supabase connection failed"
1. Check SUPABASE_CONNECTION_STRING format
2. Verify database credentials
3. Ensure pgvector extension is installed:
   ```sql
   CREATE EXTENSION IF NOT EXISTS vector;
   ```

### "Provider unhealthy"
- Check API key validity
- Verify network connectivity
- Check provider status page
- Review rate limits

### "Budget limit exceeded"
```csharp
// Verhoog budget in config
BudgetLimitUSD = 200.0

// Of reset cost tracking
orchestrator.ResetCostTracking();
```

## Resources

- [Hazina Documentation](../../../docs/)
- [Supabase Setup Guide](../../../docs/SUPABASE_SETUP.md)
- [Neurochain Guide](../../../docs/NEUROCHAIN_GUIDE.md)
- [RAG Guide](../../../docs/RAG_GUIDE.md)
- [Production Monitoring Guide](../../../docs/PRODUCTION_MONITORING_GUIDE.md)

## Licentie

Hazina is onderdeel van het Brand2Boost ecosystem.
