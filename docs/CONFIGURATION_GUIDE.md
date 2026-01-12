# Hazina Configuration Guide

Complete gids voor het configureren van alle aspecten van Hazina: storage backends, LLM providers, AI componenten, en production monitoring.

## Inhoudsopgave

1. [Storage Backend Configuratie](#storage-backend-configuratie)
2. [LLM Provider Configuratie](#llm-provider-configuratie)
3. [AI Componenten Setup](#ai-componenten-setup)
4. [Production Monitoring](#production-monitoring)
5. [Environment Variables](#environment-variables)
6. [Best Practices](#best-practices)
7. [Deployment Scenarios](#deployment-scenarios)

---

## Storage Backend Configuratie

Hazina ondersteunt 4 storage configuraties: File-based, Supabase, PostgreSQL, en Hybrid mode.

### Overzicht Storage Modes

| Mode | Configuration | Files | Embeddings | Database | Use Case |
|------|--------------|-------|------------|----------|----------|
| **File-based** | `ProjectsFolder` only | Local | Local (JSON) | None | Development, prototyping |
| **Supabase** | `SupabaseSettings.Enabled=true` | Cloud | Cloud (pgvector) | Supabase | Production, cloud |
| **PostgreSQL** | `SupabaseSettings.Enabled=true` | Database | Database (pgvector) | Self-hosted | Enterprise, on-premise |
| **Hybrid** | Both enabled | Local | Cloud (pgvector) | Supabase/PG | Best performance |

### 1. File-based Storage (Default)

**Wanneer gebruiken:**
- Lokale ontwikkeling en prototyping
- Single-user applicaties
- Geen database beschikbaar
- Portable data gewenst

**Configuratie:**
```csharp
using Hazina.Tools.Core.Config;
using Hazina.Tools.Data;

var config = new HazinaStoreConfig
{
    ApiSettings = new ApiSettings
    {
        OpenApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!
    },
    ProjectSettings = new ProjectSettings
    {
        ProjectsFolder = @"C:\HazinaData"
    }
};

// Store setup - automatically uses file-based
var storeSetup = StoreProvider.GetStoreSetup(
    config,
    projectName: "my-project",
    embeddingDimension: 1536
);

// Use the stores
var vectorStore = storeSetup.VectorStore;      // EmbeddingFileStore
var documentStore = storeSetup.DocumentStore;  // TextFileStore
var metadataStore = storeSetup.MetadataStore;  // QueryableMetadataFileStore
```

**Directory Structure:**
```
C:\HazinaData\
├── my-project\
│   ├── embeddings\
│   │   └── embeddings.json
│   ├── documents\
│   │   └── document-text.json
│   ├── chunks\
│   │   └── chunks.json
│   └── metadata\
│       └── metadata.json
```

**Voor- en nadelen:**
- ✅ Geen database configuratie
- ✅ Eenvoudige setup
- ✅ Portable data (backup = copy folder)
- ✅ Geen network dependency
- ❌ Beperkte schaalbaarheid
- ❌ Geen concurrent access support
- ❌ Lineaire search performance (geen vector indexes)

---

### 2. Supabase Storage (Aanbevolen voor Productie)

**Wanneer gebruiken:**
- Productie deployments
- Multi-user applicaties
- Cloud-native architectuur
- Schaalbaarheid vereist
- Managed service gewenst

**Setup Supabase:**

1. **Create Supabase project:**
   - Ga naar https://supabase.com
   - Create new project
   - Noteer URL en keys

2. **Enable pgvector extension:**
   ```sql
   -- In Supabase SQL Editor
   CREATE EXTENSION IF NOT EXISTS vector;
   ```

3. **Get connection details:**
   - Project URL: `https://your-project.supabase.co`
   - Anon key: In Project Settings → API
   - Connection string: In Project Settings → Database

**Configuratie:**
```csharp
var config = new HazinaStoreConfig
{
    ApiSettings = new ApiSettings
    {
        OpenApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!
    },
    SupabaseSettings = new SupabaseSettings
    {
        Enabled = true,
        Url = "https://your-project.supabase.co",
        AnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        ConnectionString = "Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password"
    }
};

// Initialize schema (one-time)
await SupabaseStoreProvider.InitializeSupabaseSchemaAsync(
    config.SupabaseSettings.ConnectionString,
    embeddingDimension: 1536
);

// Test connection
var isConnected = await SupabaseStoreProvider.TestConnectionAsync(
    config.SupabaseSettings.ConnectionString
);

// Get store setup - automatically uses Supabase
var storeSetup = SupabaseStoreProvider.GetSupabaseStoreSetup(
    config.SupabaseSettings.ConnectionString,
    embeddingDimension: 1536
);

// Or use automatic detection
var storeSetup = StoreProvider.GetStoreSetup(config);
```

**Database Schema:**
```sql
-- Created automatically by InitializeSupabaseSchemaAsync()

-- Vector embeddings with IVFFlat index
CREATE TABLE embeddings (
    id TEXT PRIMARY KEY,
    embedding vector(1536),
    metadata TEXT
);
CREATE INDEX idx_embeddings_ivfflat ON embeddings
    USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 100);

-- Document chunks
CREATE TABLE document_chunks (
    id TEXT PRIMARY KEY,
    document_id TEXT NOT NULL,
    chunk_index INTEGER NOT NULL,
    chunk_text TEXT NOT NULL,
    metadata TEXT
);

-- Document metadata (JSONB for flexible queries)
CREATE TABLE document_metadata (
    id TEXT PRIMARY KEY,
    metadata JSONB NOT NULL
);
CREATE INDEX idx_metadata_jsonb ON document_metadata USING gin(metadata);

-- Text storage
CREATE TABLE texts (
    id TEXT PRIMARY KEY,
    text TEXT NOT NULL
);
```

**Environment Variables:**
```bash
# Windows (cmd)
set SUPABASE_URL=https://your-project.supabase.co
set SUPABASE_ANON_KEY=eyJ...
set SUPABASE_CONNECTION_STRING=Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password

# Linux/Mac (bash)
export SUPABASE_URL="https://your-project.supabase.co"
export SUPABASE_ANON_KEY="eyJ..."
export SUPABASE_CONNECTION_STRING="Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=your-password"
```

**Voor- en nadelen:**
- ✅ Managed PostgreSQL + pgvector
- ✅ Automatische backups
- ✅ Schaalbaar (vertical + horizontal)
- ✅ Built-in auth en realtime
- ✅ Vector similarity search met indexes
- ✅ JSONB voor metadata queries
- ❌ Cloud account vereist
- ❌ Network latency
- ❌ Kosten bij high volume

---

### 3. PostgreSQL Storage (Self-hosted)

**Wanneer gebruiken:**
- Enterprise deployments
- On-premise vereisten
- Data sovereignty
- Volledige controle gewenst
- Custom optimalisatie nodig

**Setup PostgreSQL:**

1. **Install PostgreSQL 15+:**
   ```bash
   # Ubuntu/Debian
   sudo apt install postgresql-15 postgresql-contrib-15

   # Windows: Download from postgresql.org
   # Mac: brew install postgresql@15
   ```

2. **Install pgvector extension:**
   ```bash
   # Ubuntu/Debian
   sudo apt install postgresql-15-pgvector

   # Or compile from source
   git clone https://github.com/pgvector/pgvector.git
   cd pgvector
   make
   sudo make install
   ```

3. **Create database:**
   ```sql
   CREATE DATABASE hazina;
   CREATE USER hazina_user WITH PASSWORD 'secure_password';
   GRANT ALL PRIVILEGES ON DATABASE hazina TO hazina_user;

   \c hazina
   CREATE EXTENSION vector;
   ```

**Configuratie:**
```csharp
var config = new HazinaStoreConfig
{
    ApiSettings = new ApiSettings
    {
        OpenApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!
    },
    SupabaseSettings = new SupabaseSettings
    {
        Enabled = true,
        ConnectionString = "Host=localhost;Port=5432;Database=hazina;Username=hazina_user;Password=secure_password"
    }
};

// Initialize schema
await SupabaseStoreProvider.InitializeSupabaseSchemaAsync(
    config.SupabaseSettings.ConnectionString,
    embeddingDimension: 1536
);

// Get store setup
var storeSetup = SupabaseStoreProvider.GetSupabaseStoreSetup(
    config.SupabaseSettings.ConnectionString,
    embeddingDimension: 1536
);
```

**Performance Tuning:**
```sql
-- Optimize for vector operations
ALTER DATABASE hazina SET maintenance_work_mem = '1GB';
ALTER DATABASE hazina SET max_parallel_workers_per_gather = 4;

-- Adjust IVFFlat lists based on data size
-- Rule of thumb: lists = sqrt(number_of_rows)
DROP INDEX IF EXISTS idx_embeddings_ivfflat;
CREATE INDEX idx_embeddings_ivfflat ON embeddings
    USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 1000);  -- For ~1M rows

-- Or use HNSW for better accuracy (slower build, faster query)
CREATE INDEX idx_embeddings_hnsw ON embeddings
    USING hnsw (embedding vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);
```

**Voor- en nadelen:**
- ✅ Volledige controle
- ✅ Geen vendor lock-in
- ✅ Custom performance tuning
- ✅ On-premise deployment
- ✅ Cost predictability
- ❌ Self-managed (updates, backups, monitoring)
- ❌ Infrastructure vereist
- ❌ DBA expertise nodig

---

### 4. Hybrid Mode (Beste Performance)

**Wanneer gebruiken:**
- Snelle file access EN semantic search
- Large documents met frequent access
- Optimale performance vereist
- Flexibiliteit gewenst

**Concept:**
- **Local files:** Document text opgeslagen lokaal voor snelle toegang
- **Cloud embeddings:** Vector embeddings in Supabase/PostgreSQL voor schaalbare semantic search
- **Best of both worlds:** Fast file I/O + Powerful vector search

**Configuratie:**
```csharp
var config = new HazinaStoreConfig
{
    ApiSettings = new ApiSettings
    {
        OpenApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!
    },
    ProjectSettings = new ProjectSettings
    {
        ProjectsFolder = @"C:\HazinaData"  // Local files
    },
    SupabaseSettings = new SupabaseSettings
    {
        Enabled = true,  // Cloud embeddings
        ConnectionString = "Host=db.your-project.supabase.co;..."
    }
};

// Automatic hybrid mode detection
var storeSetup = StoreProvider.GetStoreSetup(
    config,
    projectName: "my-project",
    embeddingDimension: 1536
);

// Or explicit hybrid setup
var hybridSetup = SupabaseStoreProvider.GetHybridStoreSetup(
    config.SupabaseSettings.ConnectionString,
    config.ProjectSettings.ProjectsFolder,
    projectName: "my-project",
    embeddingDimension: 1536
);
```

**Resultaat:**
```csharp
// Vector store: PostgreSQL (cloud) - PgVectorStore
storeSetup.VectorStore

// Document store: File-based (local) - TextFileStore
storeSetup.DocumentStore

// Metadata store: PostgreSQL (cloud) - PostgresDocumentMetadataStore
storeSetup.MetadataStore

// Chunk store: File-based (local) - ChunkFileStore
storeSetup.ChunkStore
```

**Voor- en nadelen:**
- ✅ Snelle file toegang (local)
- ✅ Schaalbare vector search (cloud)
- ✅ Flexibel per component
- ✅ Offline file access mogelijk
- ❌ Complexere configuratie
- ❌ Sync tussen local/cloud nodig
- ❌ Backup strategie voor beide

---

## LLM Provider Configuratie

Hazina ondersteunt 8+ LLM providers met verschillende selectie strategieën.

### Supported Providers

| Provider | Models | Features | Use Case |
|----------|--------|----------|----------|
| **OpenAI** | GPT-4o, GPT-4o-mini, GPT-3.5 | Chat, embeddings, images | Algemeen gebruik |
| **Anthropic** | Claude 3.5 Sonnet/Opus/Haiku | Chat, 200K context | Complex reasoning |
| **Ollama** | Llama, Mistral, Phi (local) | Local inference | Privacy, cost |
| **Gemini** | Gemini Pro/Flash | Multimodal | Google ecosystem |
| **Azure OpenAI** | GPT-4, GPT-3.5 | Enterprise features | Corporate |
| **HuggingFace** | 100K+ models | Open source | Research |
| **Mistral** | Mistral Large/Medium | European AI | GDPR compliance |
| **Cohere** | Command, Embed | Production APIs | Enterprise |

### Provider Configuration Classes (HazinaConfigBase)

**⚠️ BREAKING CHANGE (v2.0):** All provider config classes now inherit from `HazinaConfigBase` and use object initializer pattern.

All LLM provider configuration classes (`OpenAIConfig`, `AnthropicConfig`, `OllamaConfig`, etc.) now share common functionality through the `HazinaConfigBase` abstract base class. This reduces ~400 lines of duplicated code and provides consistent configuration loading.

#### Common Properties (from HazinaConfigBase)

```csharp
public abstract class HazinaConfigBase
{
    public string ApiKey { get; set; }        // Required for cloud providers
    public string Model { get; set; }         // Model name (e.g., "gpt-4o-mini")
    public string? Endpoint { get; set; }     // Custom endpoint (optional)
    public string? LogPath { get; set; }      // Request/response logging path
}
```

#### Configuration Methods

**Method 1: Object Initializer (Recommended)**
```csharp
using Hazina.LLMs.OpenAI;

var config = new OpenAIConfig
{
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
    Model = "gpt-4o-mini",
    LogPath = "logs/openai-requests.log"  // Optional
};
```

**Method 2: Simple Constructor**
```csharp
// Minimal setup (uses default model)
var config = new OpenAIConfig(apiKey: "sk-...");

// Or set properties after
config.Model = "gpt-4o";
config.LogPath = "logs/openai.log";
```

**Method 3: Load from appsettings.json**
```csharp
// appsettings.json:
// {
//   "OpenAI": {
//     "ApiKey": "sk-...",
//     "Model": "gpt-4o-mini",
//     "LogPath": "logs/openai.log"
//   }
// }

var config = OpenAIConfig.Load();  // Automatic loading

// OR from IConfiguration instance
var config = OpenAIConfig.FromConfiguration(configuration);
```

#### Provider-Specific Properties

Each provider extends HazinaConfigBase with provider-specific properties:

```csharp
// OpenAI - Image and TTS models
var openaiConfig = new OpenAIConfig
{
    ApiKey = "sk-...",
    Model = "gpt-4o-mini",
    EmbeddingModel = "text-embedding-3-small",
    ImageModel = "dall-e-3",
    TtsModel = "gpt-4o-mini-tts"
};

// Anthropic - Extended context
var anthropicConfig = new AnthropicConfig
{
    ApiKey = "sk-ant-...",
    Model = "claude-3-5-sonnet-20241022"
    // Supports up to 200K context window
};

// Ollama - Local endpoint
var ollamaConfig = new OllamaConfig
{
    Endpoint = "http://localhost:11434",
    Model = "llama3:8b",
    EmbeddingModel = "nomic-embed-text"
};
```

#### Configuration Validation

All configs have built-in validation:

```csharp
var config = new OpenAIConfig();  // Missing ApiKey

var errors = config.Validate();
if (errors.Any())
{
    foreach (var error in errors)
    {
        Console.WriteLine($"Config error: {error}");
    }
    // Output: "OpenAIConfig: ApiKey is required"
}
```

#### ⚠️ Migration from v1.x

If upgrading from Hazina v1.x, update your code:

**OLD (v1.x - Constructor parameters):**
```csharp
// ❌ This no longer works
var config = new OpenAIConfig(
    apiKey: "sk-...",
    model: "gpt-4o-mini",
    endpoint: "https://api.openai.com/v1",
    logPath: "logs/openai.log"
);
```

**NEW (v2.0 - Object initializer):**
```csharp
// ✅ Use this instead
var config = new OpenAIConfig
{
    ApiKey = "sk-...",
    Model = "gpt-4o-mini",
    Endpoint = "https://api.openai.com/v1",
    LogPath = "logs/openai.log"
};
```

**Or use simple constructor (backwards compatible):**
```csharp
// ✅ Also works
var config = new OpenAIConfig("sk-...");
config.Model = "gpt-4o-mini";
```

See [docs/API_CHANGELOG.md](API_CHANGELOG.md) for complete list of breaking changes.

---

### Provider Selection Strategies

#### 1. Priority-based (Aanbevolen voor Productie)

Gebruikt altijd de hoogste priority provider die healthy is.

**Configuratie:**
```csharp
using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Configuration;
using Hazina.AI.Providers.Selection;
using Hazina.LLMs.OpenAI;
using Hazina.LLMs.Anthropic;

var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    SelectionStrategy = SelectionStrategy.Priority,
    EnableHealthMonitoring = true,
    HealthCheckIntervalSeconds = 60,
    CircuitBreakerThreshold = 5,
    CircuitBreakerTimeoutSeconds = 300
});

// Register providers (lagere priority number = hogere prioriteit)
orchestrator.RegisterProvider(
    name: "openai",
    client: new OpenAIClientWrapper(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!),
    priority: 1,  // Hoogste prioriteit
    costPer1KTokens: 0.002
);

orchestrator.RegisterProvider(
    name: "anthropic",
    client: new ClaudeClientWrapper(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!),
    priority: 2,  // Fallback
    costPer1KTokens: 0.003
);

orchestrator.RegisterProvider(
    name: "ollama",
    client: new OllamaClientWrapper("http://localhost:11434"),
    priority: 3,  // Last resort (local)
    costPer1KTokens: 0.0  // Free (local)
);

// Use
var response = await orchestrator.GetResponse("Hello!", "gpt-4o-mini", 0.7);
Console.WriteLine($"Used: {orchestrator.GetLastUsedProvider()}");  // "openai"

// Health status
var health = orchestrator.GetHealthStatus();
foreach (var (provider, status) in health)
{
    Console.WriteLine($"{provider}: {status}");  // Healthy/Degraded/Unhealthy
}
```

**Use cases:**
- Productie met primary + fallback
- SLA-kritische applicaties
- Predictable behavior

---

#### 2. Cost-optimized

Selecteert altijd de goedkoopste beschikbare provider.

**Configuratie:**
```csharp
var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    SelectionStrategy = SelectionStrategy.LeastCost,
    EnableCostTracking = true,
    BudgetLimitUSD = 100.0
});

// Register providers with costs
orchestrator.RegisterProvider("openai", new OpenAIClientWrapper(key), costPer1KTokens: 0.002);
orchestrator.RegisterProvider("anthropic", new ClaudeClientWrapper(key), costPer1KTokens: 0.003);
orchestrator.RegisterProvider("ollama", new OllamaClientWrapper(url), costPer1KTokens: 0.0);

// Automatic cost tracking
var response = await orchestrator.GetResponse("Generate code", model, 0.7);

// Check cost
var totalCost = orchestrator.GetTotalCost();
var budget = orchestrator.GetRemainingBudget();
Console.WriteLine($"Spent: ${totalCost:F6}, Remaining: ${budget:F6}");

// Budget alerts (automatic at 50%, 75%, 90%, 95%)
orchestrator.BudgetAlertTriggered += (sender, alert) =>
{
    Console.WriteLine($"⚠ Budget alert: {alert.PercentageUsed:F1}% used!");

    if (alert.PercentageUsed >= 95)
    {
        // Take action: switch to cheaper model, notify admin, etc.
    }
};
```

**Use cases:**
- Budget-constrained applicaties
- Batch processing waar speed niet kritisch is
- Development/testing

---

#### 3. Speed-optimized

Kiest provider met snelste response time (op basis van historische data).

**Configuratie:**
```csharp
var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    SelectionStrategy = SelectionStrategy.FastestResponse,
    EnablePerformanceTracking = true
});

orchestrator.RegisterProvider("openai", new OpenAIClientWrapper(key));
orchestrator.RegisterProvider("anthropic", new ClaudeClientWrapper(key));

// First request: random selection (no history yet)
var response1 = await orchestrator.GetResponse("Quick question", model, 0.7);

// Subsequent requests: uses fastest based on history
var response2 = await orchestrator.GetResponse("Another question", model, 0.7);

// Performance stats
var stats = orchestrator.GetPerformanceStats();
foreach (var (provider, avgMs) in stats)
{
    Console.WriteLine($"{provider}: {avgMs:F0}ms average");
}
```

**Use cases:**
- Low-latency vereisten
- Real-time chat
- Interactive assistants

---

#### 4. Failover (High Availability)

Quick setup met automatic failover.

**Configuratie:**
```csharp
using Hazina.AI.FluentAPI.Configuration;

// Quick setup met failover
var orchestrator = QuickSetup.SetupWithFailover(
    openAIKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
    anthropicKey: Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!
);

// Configure as default for Fluent API
Hazina.ConfigureDefaultOrchestrator(orchestrator);

// Use anywhere
var answer = await Hazina.AskAsync("What is the capital of France?");
```

**Behavior:**
- Primary: OpenAI (priority 1)
- Fallback: Anthropic (priority 2)
- Circuit breaker: 5 failures → switch
- Retry: Exponential backoff (1s, 2s, 4s, 8s)
- Health monitoring: Every 60 seconds

**Use cases:**
- High availability vereist
- 24/7 beschikbaarheid
- Mission-critical applicaties

---

#### 5. Round-robin (Load Balancing)

Distribueert requests gelijkmatig over alle providers.

**Configuratie:**
```csharp
var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    SelectionStrategy = SelectionStrategy.RoundRobin
});

orchestrator.RegisterProvider("openai", new OpenAIClientWrapper(key));
orchestrator.RegisterProvider("anthropic", new ClaudeClientWrapper(key));

// Requests rotate: openai → anthropic → openai → anthropic → ...
for (int i = 0; i < 4; i++)
{
    await orchestrator.GetResponse($"Request {i}", model, 0.7);
    Console.WriteLine($"Request {i} → {orchestrator.GetLastUsedProvider()}");
}
```

**Use cases:**
- Load distribution
- Rate limit avoidance
- Testing meerdere providers

---

#### 6. Random

Random provider selectie.

**Configuratie:**
```csharp
var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    SelectionStrategy = SelectionStrategy.Random
});
```

**Use cases:**
- Testing
- Unpredictable load balancing

---

### Quick Setup Helpers

#### Single Provider
```csharp
using Hazina.AI.FluentAPI.Configuration;

var orchestrator = QuickSetup.SetupOpenAI(
    apiKey: "sk-...",
    defaultModel: "gpt-4o-mini"
);
```

#### With Failover
```csharp
var orchestrator = QuickSetup.SetupWithFailover(
    openAIKey: "sk-...",
    anthropicKey: "sk-ant-..."
);
```

#### Cost-optimized
```csharp
var orchestrator = QuickSetup.SetupCostOptimized(
    openAIKey: "sk-...",
    anthropicKey: "sk-ant-...",
    ollamaUrl: "http://localhost:11434"
);
```

#### Complete Configuration
```csharp
var orchestrator = QuickSetup.SetupAndConfigure(
    openAIKey: "sk-...",
    anthropicKey: "sk-ant-..."
);

// Sets as default orchestrator
Hazina.ConfigureDefaultOrchestrator(orchestrator);
```

---

## AI Componenten Setup

### RAG (Retrieval-Augmented Generation)

**Configuratie:**
```csharp
using Hazina.AI.RAG.Core;
using Hazina.AI.RAG.Embeddings;
using Hazina.AI.RAG.Retrieval;

// Setup storage
var storeSetup = StoreProvider.GetStoreSetup(config, "my-project", 1536);

// Create RAG engine
var ragEngine = new RAGEngine(
    llmClient: orchestrator,
    vectorStore: storeSetup.VectorStore!,
    config: new RAGConfig
    {
        TopK = 5,
        MinimumSimilarity = 0.7,
        RerankerStrategy = RerankerStrategy.Hybrid,
        ChunkingStrategy = ChunkingStrategy.Semantic,
        ChunkSize = 512,
        ChunkOverlap = 50
    }
);

// Index documents
await ragEngine.IndexDocumentAsync(
    documentId: "doc1",
    content: "Document content here...",
    metadata: new Dictionary<string, string>
    {
        ["author"] = "John Doe",
        ["category"] = "technical",
        ["date"] = "2025-01-05"
    }
);

// Query
var response = await ragEngine.QueryAsync(
    query: "What is the main topic?",
    filters: new Dictionary<string, string>
    {
        ["category"] = "technical"
    }
);

Console.WriteLine($"Answer: {response.Answer}");
Console.WriteLine($"Confidence: {response.Confidence:P0}");
Console.WriteLine($"Sources: {response.RetrievedChunks.Count}");

foreach (var chunk in response.RetrievedChunks)
{
    Console.WriteLine($"  - {chunk.DocumentId} (similarity: {chunk.Similarity:F2})");
}
```

---

### Neurochain (Multi-layer Reasoning)

**Configuratie:**
```csharp
using Hazina.Neurochain.Core.Core;
using Hazina.Neurochain.Core.Layers;

// Create Neurochain
var neurochain = new NeuroChainOrchestrator(new NeuroChainConfig
{
    ParallelExecution = true,
    EnableCrossValidation = true,
    EnableEarlyStopping = true,
    EarlyStoppingConfidence = 0.95
});

// Add layers
neurochain.AddLayer(new FastReasoningLayer(orchestrator));
neurochain.AddLayer(new DeepReasoningLayer(orchestrator));
neurochain.AddLayer(new VerificationLayer(orchestrator));

// Reason
var result = await neurochain.ReasonAsync(
    question: "What is the square root of 256?",
    context: new ReasoningContext
    {
        MinConfidence = 0.9,
        Domain = "mathematics",
        GroundTruth = new Dictionary<string, string>
        {
            ["sqrt_256"] = "16"
        }
    }
);

Console.WriteLine($"Answer: {result.FinalAnswer}");
Console.WriteLine($"Confidence: {result.FinalConfidence:P0}");
Console.WriteLine($"Consensus: {result.CrossValidation?.ConsensusReached}");
Console.WriteLine($"Cost: ${result.TotalCost:F6}");
Console.WriteLine($"Time: {result.TotalDurationMs}ms");

// Detailed breakdown
Console.WriteLine(result.GetDetailedBreakdown());
```

---

### Agents (Autonomous Workflows)

**Configuratie:**
```csharp
using Hazina.AI.Agents.Core;
using Hazina.AI.Agents.Tools;
using Hazina.AI.Agents.Workflows;

// Create agent
var agent = new Agent(
    name: "Assistant",
    role: "Helpful AI assistant",
    llmClient: orchestrator
);

// Register tools
agent.RegisterTool(new CalculatorTool());

// Execute task
var response = await agent.ExecuteAsync("Calculate 123 * 456");
Console.WriteLine(response);

// Workflow
var workflow = new WorkflowEngine(orchestrator);

workflow.AddStep(new AgentTaskStep
{
    AgentName = "Researcher",
    Task = "Research the topic",
    OutputVariable = "research_result"
});

workflow.AddStep(new AgentTaskStep
{
    AgentName = "Writer",
    Task = "Write article based on: {{research_result}}",
    OutputVariable = "article"
});

var result = await workflow.ExecuteAsync(new WorkflowContext
{
    Variables = new Dictionary<string, string>
    {
        ["topic"] = "AI in healthcare"
    }
});
```

---

## Production Monitoring

**Complete monitoring setup:**

```csharp
using Hazina.Production.Monitoring.Metrics;
using Hazina.Production.Monitoring.Performance;
using Hazina.Production.Monitoring.Diagnostics;

// 1. Metrics collector
var metrics = new MetricsCollector();

// 2. Performance profiler
var profiler = new PerformanceProfiler();

// 3. Diagnostics
var diagnostics = new DiagnosticsCollector(new DiagnosticsConfig
{
    EnableMemoryMonitoring = true,
    EnableCpuMonitoring = true,
    MemoryThresholdMB = 1000,
    CpuThresholdPercent = 80
});

// 4. Use in request handling
async Task<string> HandleRequest(string prompt)
{
    using (metrics.TimeOperation("llm_request"))
    using (profiler.Profile("request_handling"))
    {
        try
        {
            var result = await orchestrator.GetResponse(prompt, "gpt-4o-mini", 0.7);

            metrics.IncrementCounter("requests_total", new Dictionary<string, string>
            {
                ["status"] = "success"
            });

            metrics.RecordValue("request_tokens", result.TokensUsed);

            return result.Text;
        }
        catch (Exception ex)
        {
            metrics.IncrementCounter("requests_total", new Dictionary<string, string>
            {
                ["status"] = "error"
            });

            throw;
        }
    }
}

// 5. Health checks
var health = diagnostics.RunHealthCheck();
if (health.Status == HealthStatus.Unhealthy)
{
    Console.WriteLine($"⚠ System unhealthy: {health.Message}");
}

// 6. Export metrics (Prometheus)
var prometheusMetrics = metrics.ExportPrometheus();
// Expose on /metrics endpoint

// 7. Generate performance report
var report = profiler.GenerateMarkdownReport();
Console.WriteLine(report);
```

---

## Environment Variables

### Complete List

```bash
# ==========================================
# LLM PROVIDERS
# ==========================================

# OpenAI (Required for most features)
OPENAI_API_KEY=sk-...

# Anthropic (Optional, recommended for failover)
ANTHROPIC_API_KEY=sk-ant-...

# Ollama (Optional, for local inference)
OLLAMA_URL=http://localhost:11434

# Google Gemini (Optional)
GEMINI_API_KEY=...

# Azure OpenAI (Optional)
AZURE_OPENAI_KEY=...
AZURE_OPENAI_ENDPOINT=https://...
AZURE_OPENAI_DEPLOYMENT=...

# ==========================================
# STORAGE: SUPABASE
# ==========================================

SUPABASE_URL=https://your-project.supabase.co
SUPABASE_ANON_KEY=eyJ...
SUPABASE_SERVICE_ROLE_KEY=eyJ...  # Server-side only!
SUPABASE_CONNECTION_STRING=Host=db.your-project.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=...

# Or shorthand:
SUPABASE_DB_URL=postgresql://postgres:password@db.your-project.supabase.co:5432/postgres

# ==========================================
# STORAGE: POSTGRESQL
# ==========================================

POSTGRES_CONNECTION_STRING=Host=localhost;Port=5432;Database=hazina;Username=hazina_user;Password=...

# Or shorthand:
DATABASE_URL=postgresql://hazina_user:password@localhost:5432/hazina

# ==========================================
# STORAGE: FILE-BASED
# ==========================================

HAZINA_PROJECTS_FOLDER=C:\HazinaData
# Or
HAZINA_DATA_DIR=/var/lib/hazina

# ==========================================
# PRODUCTION SETTINGS
# ==========================================

# Budget limits
HAZINA_BUDGET_USD=100.0

# Health monitoring
HAZINA_HEALTH_CHECK_INTERVAL=60

# Performance
HAZINA_MAX_PARALLEL_REQUESTS=10
HAZINA_REQUEST_TIMEOUT_SECONDS=30

# Logging
HAZINA_LOG_LEVEL=Information  # Trace, Debug, Information, Warning, Error, Critical
HAZINA_LOG_FILE=hazina.log

# ==========================================
# SECURITY
# ==========================================

# JWT for API authentication (if building API)
JWT_SECRET=...
JWT_ISSUER=...
JWT_AUDIENCE=...

# CORS (if building web API)
CORS_ORIGINS=https://yourdomain.com,https://api.yourdomain.com

# ==========================================
# OPTIONAL: ADVANCED FEATURES
# ==========================================

# Neurochain
NEUROCHAIN_PARALLEL_EXECUTION=true
NEUROCHAIN_ENABLE_CROSS_VALIDATION=true

# RAG
RAG_CHUNK_SIZE=512
RAG_CHUNK_OVERLAP=50
RAG_TOP_K=5
RAG_MIN_SIMILARITY=0.7

# Agents
AGENTS_MAX_ITERATIONS=10
AGENTS_ENABLE_TOOL_CALLING=true
```

---

## Best Practices

### 1. Security

**DO:**
- ✅ Gebruik environment variables voor API keys
- ✅ Roteer API keys elke 90 dagen
- ✅ Gebruik service role keys alleen server-side
- ✅ Enable SSL/TLS voor alle database connecties
- ✅ Implementeer rate limiting
- ✅ Log security events
- ✅ Gebruik least-privilege database credentials

**DON'T:**
- ❌ Hardcode API keys in source code
- ❌ Commit .env files to git
- ❌ Expose service role keys client-side
- ❌ Gebruik default passwords
- ❌ Skip input validation

**Example:**
```csharp
// BAD
var apiKey = "sk-proj-abc123...";  // Hardcoded!

// GOOD
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY not configured");
```

---

### 2. Cost Management

**Strategies:**
```csharp
// 1. Set budget limits
var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    EnableCostTracking = true,
    BudgetLimitUSD = 100.0
});

// 2. Use cost-optimized strategy for batch work
if (isBatchJob)
{
    config.SelectionStrategy = SelectionStrategy.LeastCost;
}

// 3. Enable early stopping in Neurochain
var neurochain = new NeuroChainOrchestrator(new NeuroChainConfig
{
    EnableEarlyStopping = true,
    EarlyStoppingConfidence = 0.90  // Skip expensive layers if confidence is high
});

// 4. Monitor costs
orchestrator.BudgetAlertTriggered += (sender, alert) =>
{
    if (alert.PercentageUsed >= 90)
    {
        // Switch to cheaper models
        // Notify admin
        // Throttle requests
    }
};

// 5. Use local models for simple tasks
orchestrator.RegisterProvider("ollama", ollamaClient, priority: 10, costPer1KTokens: 0.0);
```

---

### 3. Performance

**Optimization checklist:**
- ✅ Use hybrid storage for best performance
- ✅ Enable parallel execution in Neurochain
- ✅ Implement caching for frequent queries
- ✅ Use connection pooling for database
- ✅ Monitor and tune vector index parameters
- ✅ Use streaming for long responses
- ✅ Implement retry with exponential backoff

**Example:**
```csharp
// 1. Parallel execution
var neurochain = new NeuroChainOrchestrator(new NeuroChainConfig
{
    ParallelExecution = true  // 60% faster!
});

// 2. Connection pooling (automatic in Npgsql)
var connectionString = "Host=...;Maximum Pool Size=100;Minimum Pool Size=10";

// 3. Vector index tuning
// For ~1M rows: lists = sqrt(1000000) = 1000
CREATE INDEX idx ON embeddings USING ivfflat (embedding vector_cosine_ops) WITH (lists = 1000);

// 4. Streaming for long responses
await foreach (var chunk in orchestrator.GetResponseStream(prompt, model, 0.7))
{
    Console.Write(chunk);
}
```

---

### 4. Reliability

**Checklist:**
- ✅ Use priority-based strategy with failover
- ✅ Enable health monitoring
- ✅ Implement circuit breaker
- ✅ Set reasonable timeouts
- ✅ Log all errors
- ✅ Monitor system metrics
- ✅ Have backup providers

**Example:**
```csharp
var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    SelectionStrategy = SelectionStrategy.Priority,
    EnableHealthMonitoring = true,
    HealthCheckIntervalSeconds = 60,
    CircuitBreakerThreshold = 5,
    CircuitBreakerTimeoutSeconds = 300,
    RequestTimeoutSeconds = 30
});

// At least 2 providers
orchestrator.RegisterProvider("openai", openaiClient, priority: 1);
orchestrator.RegisterProvider("anthropic", anthropicClient, priority: 2);

// Health check endpoint
app.MapGet("/health", () =>
{
    var health = orchestrator.GetHealthStatus();
    var allHealthy = health.All(h => h.Value == ProviderHealth.Healthy);

    return allHealthy ? Results.Ok(health) : Results.StatusCode(503);
});
```

---

## Deployment Scenarios

### Scenario 1: Startup/MVP

**Requirements:**
- Minimale kosten
- Snelle setup
- Single-user of low traffic

**Configuration:**
```csharp
// Storage: File-based
var config = new HazinaStoreConfig
{
    ApiSettings = new ApiSettings { OpenApiKey = openAIKey },
    ProjectSettings = new ProjectSettings
    {
        ProjectsFolder = @"C:\HazinaData"
    }
};

// Provider: OpenAI alleen
var orchestrator = QuickSetup.SetupOpenAI(openAIKey, "gpt-4o-mini");

// Features: Basic RAG
var ragEngine = new RAGEngine(orchestrator, storeSetup.VectorStore!);

// Monitoring: Cost tracking
orchestrator.GetTotalCost();  // Check periodically
```

**Cost:** ~$5-20/month

---

### Scenario 2: Growing SaaS

**Requirements:**
- Multi-user
- Schaalbaarheid
- Betrouwbaarheid
- Cost-aware

**Configuration:**
```csharp
// Storage: Hybrid
var config = new HazinaStoreConfig
{
    ProjectSettings = new ProjectSettings { ProjectsFolder = "/data/hazina" },
    SupabaseSettings = new SupabaseSettings
    {
        Enabled = true,
        ConnectionString = supabaseConnStr
    }
};

// Providers: OpenAI + Anthropic (failover)
var orchestrator = QuickSetup.SetupWithFailover(openAIKey, anthropicKey);

// Features: RAG + Neurochain (fast + deep)
var neurochain = new NeuroChainOrchestrator(new NeuroChainConfig
{
    ParallelExecution = true
});
neurochain.AddLayer(new FastReasoningLayer(orchestrator));
neurochain.AddLayer(new DeepReasoningLayer(orchestrator));

// Monitoring: Full stack
metrics.EnableAll();
orchestrator.EnableHealthMonitoring();
```

**Cost:** ~$50-500/month

---

### Scenario 3: Enterprise

**Requirements:**
- High availability
- Data sovereignty
- SLA vereisten
- Full control
- Compliance

**Configuration:**
```csharp
// Storage: Self-hosted PostgreSQL
var config = new HazinaStoreConfig
{
    SupabaseSettings = new SupabaseSettings
    {
        Enabled = true,
        ConnectionString = "Host=pgcluster.internal;..."  // HA cluster
    }
};

// Providers: Multiple with priority
var orchestrator = new ProviderOrchestrator(new ProviderOrchestratorConfig
{
    SelectionStrategy = SelectionStrategy.Priority,
    EnableHealthMonitoring = true,
    BudgetLimitUSD = 10000.0
});

orchestrator.RegisterProvider("azure-openai", azureClient, priority: 1);
orchestrator.RegisterProvider("anthropic", anthropicClient, priority: 2);
orchestrator.RegisterProvider("openai", openaiClient, priority: 3);

// Features: Full stack
var neurochain = new NeuroChainOrchestrator(new NeuroChainConfig
{
    ParallelExecution = true,
    EnableCrossValidation = true
});
neurochain.AddLayer(new FastReasoningLayer(orchestrator));
neurochain.AddLayer(new DeepReasoningLayer(orchestrator));
neurochain.AddLayer(new VerificationLayer(orchestrator));

// Monitoring: Enterprise-grade
app.MapGet("/metrics", () => metrics.ExportPrometheus());
app.MapGet("/health", () => diagnostics.RunHealthCheck());
```

**Cost:** $1000+/month

---

## Resources

- **Guides:**
  - [Supabase Setup](SUPABASE_SETUP.md)
  - [Neurochain Guide](NEUROCHAIN_GUIDE.md)
  - [RAG Guide](RAG_GUIDE.md)
  - [Agents Guide](AGENTS_GUIDE.md)
  - [Production Monitoring](PRODUCTION_MONITORING_GUIDE.md)

- **Demos:**
  - [Configuration Showcase](../apps/Demos/Hazina.Demo.ConfigurationShowcase/)
  - [Supabase Demo](../apps/Demos/Hazina.Demo.Supabase/)

- **External:**
  - [OpenAI Documentation](https://platform.openai.com/docs)
  - [Anthropic Documentation](https://docs.anthropic.com)
  - [Supabase Documentation](https://supabase.com/docs)
  - [pgvector Documentation](https://github.com/pgvector/pgvector)
