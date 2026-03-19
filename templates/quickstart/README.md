# Hazina QuickStart Templates

Production-ready code templates to get you started with Hazina in minutes.

---

## Available Templates

### 1. BasicRAGSetup.cs.template

**Use Case:** Build a RAG system that answers questions from your documents
**Time to Production:** 30 minutes
**Difficulty:** Beginner

**Features:**
- One-line AI setup
- Automatic document chunking
- Semantic search and retrieval
- Context-aware answers with citations

**Quick Start:**
```bash
# Copy template
cp BasicRAGSetup.cs.template MyRAG.cs

# Replace {{NAMESPACE}} with your namespace
sed -i 's/{{NAMESPACE}}/MyApp.AI/g' MyRAG.cs

# Run
dotnet run
```

---

### 2. MultiProviderSetup.cs.template

**Use Case:** Production resilience with automatic failover between providers
**Time to Production:** 45 minutes
**Difficulty:** Intermediate

**Features:**
- Multi-provider orchestration (OpenAI, Anthropic, Ollama)
- Automatic failover on provider failure
- Cost optimization strategies
- Speed vs. cost tradeoffs

**Quick Start:**
```bash
cp MultiProviderSetup.cs.template MyMultiProvider.cs
sed -i 's/{{NAMESPACE}}/MyApp.AI/g' MyMultiProvider.cs

# Set API keys
export OPENAI_API_KEY="sk-..."
export ANTHROPIC_API_KEY="sk-ant-..."

dotnet run
```

---

### 3. AgenticWorkflow.cs.template

**Use Case:** Multi-agent collaboration for complex tasks
**Time to Production:** 1 hour
**Difficulty:** Advanced

**Features:**
- Research → Write → Review pipeline
- Sequential, parallel, and iterative workflows
- Quality gates with automatic revisions
- Agent specialization

**Quick Start:**
```bash
cp AgenticWorkflow.cs.template MyWorkflow.cs
sed -i 's/{{NAMESPACE}}/MyApp.Agents/g' MyWorkflow.cs

dotnet run
```

**Example Output:**
```
=== Iteration 1 ===
Research completed: 2840 chars
Draft created: 1523 chars
Review completed
⚠ Needs revision. Feedback: [...]

=== Iteration 2 ===
Research completed: 2840 chars
Draft created: 1687 chars
Review completed
✓ Approved after 2 iteration(s)
```

---

### 4. ProductionDeployment.cs.template

**Use Case:** Enterprise-grade AI application with full observability
**Time to Production:** 2 hours
**Difficulty:** Advanced

**Features:**
- Cost tracking and budget enforcement
- Rate limiting per user
- Circuit breaker for resilience
- Comprehensive logging (Application Insights)
- Metrics collection (Prometheus)
- Health checks
- Secure configuration (Azure Key Vault)

**Quick Start:**
```bash
cp ProductionDeployment.cs.template MyProductionAI.cs
sed -i 's/{{NAMESPACE}}/MyApp.Production/g' MyProductionAI.cs

# Configure appsettings.json (see template comments)
dotnet run
```

---

## Template Usage

### Step 1: Choose Template

Pick the template that matches your use case:

| Template | Best For |
|----------|----------|
| BasicRAGSetup | Document Q&A, knowledge bases |
| MultiProviderSetup | Production resilience, cost optimization |
| AgenticWorkflow | Research, writing, analysis pipelines |
| ProductionDeployment | Enterprise applications, SaaS products |

### Step 2: Copy Template

```bash
cp templates/quickstart/[TEMPLATE].cs.template src/[YourFile].cs
```

### Step 3: Replace Placeholders

All templates use `{{NAMESPACE}}` placeholder:

```bash
# Unix/Mac
sed -i 's/{{NAMESPACE}}/YourApp.AI/g' src/YourFile.cs

# Windows PowerShell
(Get-Content src/YourFile.cs) -replace '{{NAMESPACE}}', 'YourApp.AI' | Set-Content src/YourFile.cs
```

### Step 4: Add Dependencies

Each template lists required NuGet packages in comments. Add them:

```bash
dotnet add package Hazina.AI.FluentAPI --version 1.0.1
dotnet add package Hazina.AI.RAG --version 1.0.1
# ... (see template comments for full list)
```

### Step 5: Configure

Set API keys and configuration:

```bash
# Environment variables (recommended)
export OPENAI_API_KEY="sk-..."

# Or appsettings.json
{
  "OpenAI": {
    "ApiKey": "sk-..."
  }
}

# Or Azure Key Vault (production)
az keyvault secret set --vault-name MyVault --name OpenAIApiKey --value "sk-..."
```

### Step 6: Run

```bash
dotnet build
dotnet run
```

---

## Customization Guide

### Change LLM Provider

**From OpenAI to Anthropic:**
```csharp
// BEFORE
var ai = QuickSetup.SetupOpenAI(openAiKey);

// AFTER
using Hazina.LLMs.Anthropic;
var config = new AnthropicConfig { ApiKey = anthropicKey, Model = "claude-3-5-sonnet-20241022" };
var ai = new ClaudeClientWrapper(config);
```

**To Local Ollama:**
```csharp
using Hazina.LLMs.Ollama;
var config = new OllamaConfig { Endpoint = "http://localhost:11434", Model = "llama3:8b" };
var ai = new OllamaClientWrapper(config);
```

### Add Vector Store

**From In-Memory to PostgreSQL:**
```csharp
// BEFORE
var vectorStore = new InMemoryVectorStore();

// AFTER
var vectorStore = new PostgresVectorStore(
    connectionString: "Host=localhost;Database=mydb;Username=user;Password=pass"
);
```

### Enable Monitoring

```csharp
// Add to any template
using Hazina.Production.Monitoring;

// Cost tracking
ai.EnableCostTracking(budgetLimit: 100.00m);
var cost = await ai.GetTotalCostAsync();

// Health monitoring
ai.EnableHealthMonitoring();
var health = await ai.GetHealthStatusAsync();

// Metrics
var metrics = new PrometheusMetricsCollector();
metrics.RecordLatency("ai_request", duration.TotalMilliseconds);
```

---

## Migration Paths

### From Template to Production

Each template is designed to scale:

1. **BasicRAGSetup → Production RAG**
   - Replace InMemoryVectorStore with PostgresVectorStore
   - Add caching layer (Redis)
   - Enable monitoring and cost tracking
   - Implement API authentication

2. **MultiProviderSetup → Production Orchestrator**
   - Add circuit breaker
   - Implement retry with exponential backoff
   - Enable comprehensive logging
   - Add metrics collection

3. **AgenticWorkflow → Production Agents**
   - Persist agent state (database)
   - Add tool integration (web search, APIs)
   - Implement agent authorization
   - Enable workflow monitoring

4. **ProductionDeployment → Enterprise Scale**
   - Horizontal scaling (load balancer)
   - Distributed caching (Redis Cluster)
   - Async processing (message queues)
   - Multi-region deployment

---

## Troubleshooting

### Error: Missing NuGet Package

**Symptom:**
```
error CS0246: The type or namespace name 'Hazina' could not be found
```

**Fix:**
```bash
dotnet add package Hazina.AI.FluentAPI --version 1.0.1
dotnet restore
```

---

### Error: API Key Not Found

**Symptom:**
```
InvalidOperationException: OPENAI_API_KEY not set
```

**Fix:**
```bash
# Set environment variable
export OPENAI_API_KEY="sk-..."

# Or add to appsettings.json
{
  "OpenAI": {
    "ApiKey": "sk-..."
  }
}
```

---

### Error: Namespace Not Replaced

**Symptom:**
```
error CS0246: The type or namespace name '{{NAMESPACE}}' could not be found
```

**Fix:**
```bash
# Replace placeholder
sed -i 's/{{NAMESPACE}}/YourApp.AI/g' YourFile.cs
```

---

## Examples Gallery

### Example 1: Document Q&A

**Input:** "What is Hazina?"
**Output:** "Based on the documents, Hazina is a .NET AI framework for production applications. It provides RAG, multi-provider orchestration, and agentic workflows."

### Example 2: Multi-Agent Research

**Input:** "Write an article about quantum computing"
**Output (after 3 iterations):**
- Research: 2840 chars of findings
- Draft 1: 1523 chars → Needs revision
- Draft 2: 1687 chars → Approved
- Final: Polished article with introduction, content, conclusion

### Example 3: Production Monitoring

**Metrics Collected:**
- Request latency: 1.2s (p50), 2.5s (p95)
- Success rate: 99.8%
- Cost per request: $0.003
- Daily budget: $87.50 / $100.00

---

## Next Steps

1. **Read Documentation**
   - [README.md](../../README.md) - Framework overview
   - [MIGRATION_GUIDE.md](../../docs/MIGRATION_GUIDE.md) - Version upgrades
   - [UPDATESTORE_SAFETY_POLICIES.md](../../docs/UPDATESTORE_SAFETY_POLICIES.md) - Safety guidelines

2. **Explore Examples**
   - [samples/](../../samples/) - Complete example projects
   - [docs/RAG_GUIDE.md](../../docs/RAG_GUIDE.md) - RAG deep dive
   - [docs/AGENTS_GUIDE.md](../../docs/AGENTS_GUIDE.md) - Agentic patterns

3. **Join Community**
   - GitHub Issues: Report bugs, request features
   - Discussions: Ask questions, share experiences
   - Contributing: Submit PRs, improve docs

---

**Last Updated:** 2026-03-19
**Hazina Version:** 1.0.1
**Templates Version:** 1.0
