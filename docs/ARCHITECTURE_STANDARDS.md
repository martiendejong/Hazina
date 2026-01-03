# Hazina Framework - Architecture Standards and Guidelines

This document defines the mandatory architecture standards for the Hazina framework. All code contributions must follow these rules.

---

## Core Principles

### 1. Prompts Are Always Configurable - NEVER Hardcoded

**MANDATORY**: All prompts must be externally configurable. No prompt text should exist in compiled code.

**Why**:
- Prompts are the primary interface to AI behavior
- Iteration cycles must be fast (no recompile/redeploy)
- Different deployments need different prompts
- Prompt engineering is an ongoing process

**Implementation Pattern**:
```csharp
public class AgentFactory
{
    // CORRECT: Load prompts from configuration
    public async Task<HazinaAgent> CreateAgentAsync(AgentConfig config)
    {
        var systemPrompt = await _promptLoader.LoadAsync(config.PromptPath);
        return new HazinaAgent(config.Name, systemPrompt, ...);
    }
}
```

**Anti-Pattern**:
```csharp
public class AgentFactory
{
    // WRONG: Hardcoded prompt
    public HazinaAgent CreateAgent()
    {
        return new HazinaAgent("assistant",
            "You are a helpful coding assistant...", ...);
    }
}
```

**Prompt Sources** (in order of precedence):
1. Store-specific: `stores/{name}/prompts/*.txt`
2. Agent-specific: `agents.hazina` or `agents.json`
3. Flow-specific: `flows.hazina` or `flows.json`
4. Framework defaults: Should be minimal/generic

---

### 2. Store Tools Are Always Configurable - NEVER Hardcoded

**MANDATORY**: Tool availability must be explicitly configured per agent/store, not compiled into code.

**Why**:
- Security: Least-privilege access control
- Flexibility: Different contexts need different tools
- Auditability: Tool access should be traceable
- Extensibility: Custom tools without framework changes

**Configuration Format** (agents.hazina):
```
Name: code_assistant
Prompt: prompts/code_assistant.txt
Stores: source_code|True
Functions: git,dotnet,npm,build
CallsAgents:
CallsFlows:
```

**Implementation Pattern**:
```csharp
public void ConfigureTools(HazinaAgent agent, AgentConfig config)
{
    // CORRECT: Tools from configuration
    foreach (var toolName in config.Functions.Split(','))
    {
        var tool = _toolRegistry.Get(toolName.Trim());
        if (tool != null)
            agent.AddTool(tool);
    }
}
```

**Anti-Pattern**:
```csharp
public void ConfigureTools(HazinaAgent agent)
{
    // WRONG: Hardcoded tools
    agent.AddTool(new GitTool());
    agent.AddTool(new DotnetTool());
    agent.AddTool(new FileReadTool());
}
```

**Tool Registry Pattern**:
- Register all available tools in `IToolRegistry`
- Agents request tools by name
- Unknown tool names are logged and skipped
- No direct tool instantiation in agent code

---

### 3. Knowledge Storage Is Metadata-First, Embeddings-Secondary

**MANDATORY**: The knowledge database is the primary query layer. Embeddings are optional search accelerators.

**Why**:
- Agents need deterministic queries for structured information
- Semantic similarity is not the only measure of relevance
- Embedding providers may change; data must persist
- Offline/cost-sensitive scenarios require non-embedding search

**Design Principles**:

| Principle | Implementation |
|-----------|---------------|
| Database is truth | SQLite (local) or PostgreSQL (production) stores all queryable data |
| Metadata is primary | Tags, properties, and structure are always indexed and queryable |
| Embeddings are secondary | Optional acceleration layer, not required for function |
| Files are sources | Documents and chunks are inputs, regenerated on demand |

**Implementation Pattern**:
```csharp
public class KnowledgeService
{
    // CORRECT: Metadata-first search with optional embeddings
    public async Task<IEnumerable<Chunk>> SearchAsync(SearchRequest request)
    {
        // Step 1: Filter by metadata (always)
        var filtered = await _db.FilterAsync(request.MetadataFilters);

        // Step 2: Optionally rank by embeddings
        if (request.UseSemanticSearch && _config.EmbeddingsEnabled)
        {
            filtered = await _embeddings.RankAsync(request.Query, filtered);
        }

        return filtered;
    }
}
```

**Anti-Pattern**:
```csharp
public class KnowledgeService
{
    // WRONG: Embeddings as primary (fails without vector store)
    public async Task<IEnumerable<Chunk>> SearchAsync(string query)
    {
        var embedding = await _embedder.EmbedAsync(query);
        return await _vectorStore.SearchAsync(embedding); // No metadata filtering
    }
}
```

**Guarantee**:
```
If embeddings are disabled, Hazina must continue to function correctly.
```

See [Knowledge Storage & Search Model](KNOWLEDGE_STORAGE.md) for complete architecture.

---

### 4. LLM Providers and Data Storage Are Always Configurable

**MANDATORY**: Provider selection and storage backends must be runtime-configurable.

**Why**:
- Cost optimization: Switch providers based on task
- Reliability: Failover between providers
- Compliance: Data residency requirements
- Testing: Mock providers for unit tests

**Provider Configuration** (appsettings.json):
```json
{
  "LLM": {
    "DefaultProvider": "openai",
    "Providers": {
      "openai": {
        "ApiKey": "${OPENAI_API_KEY}",
        "Model": "gpt-4.1",
        "EmbeddingModel": "text-embedding-ada-002"
      },
      "anthropic": {
        "ApiKey": "${ANTHROPIC_API_KEY}",
        "Model": "claude-sonnet-4-20250514"
      }
    }
  }
}
```

**Storage Configuration**:
```json
{
  "Storage": {
    "Backend": "supabase",
    "Embeddings": {
      "Provider": "pgvector",
      "ConnectionString": "${SUPABASE_CONNECTION_STRING}"
    },
    "Documents": {
      "Provider": "filesystem",
      "BasePath": "c:\\stores"
    }
  }
}
```

**Implementation Pattern**:
```csharp
public class LLMClientFactory
{
    // CORRECT: Factory pattern with configuration
    public ILLMClient Create(string providerName)
    {
        var config = _configuration.GetSection($"LLM:Providers:{providerName}");
        return providerName switch
        {
            "openai" => new OpenAIClientWrapper(config.Get<OpenAIConfig>()),
            "anthropic" => new AnthropicClient(config.Get<AnthropicConfig>()),
            _ => throw new InvalidOperationException($"Unknown provider: {providerName}")
        };
    }
}
```

**Anti-Pattern**:
```csharp
// WRONG: Hardcoded provider
var client = new OpenAIClientWrapper(new OpenAIConfig
{
    ApiKey = "sk-...",
    Model = "gpt-4"
});
```

---

## Configuration File Formats

### .hazina Format
Simple text format for human editing:
```
Name: my_agent
Description: Does something useful
Prompt: path/to/prompt.txt
Stores: store1|False,store2|True
Functions: tool1,tool2
```

### JSON Format
Structured format for programmatic access:
```json
{
  "name": "my_agent",
  "description": "Does something useful",
  "promptPath": "path/to/prompt.txt",
  "stores": [
    { "name": "store1", "writable": false },
    { "name": "store2", "writable": true }
  ],
  "functions": ["tool1", "tool2"]
}
```

---

## Environment Variable Pattern

Sensitive values must use environment variables:
```json
{
  "ApiKey": "${OPENAI_API_KEY}",
  "ConnectionString": "${DATABASE_URL}"
}
```

The framework resolves `${VAR_NAME}` syntax at runtime.

---

## Validation Requirements

### Startup Validation
- All configured prompts must exist
- All configured stores must be accessible
- All configured tools must be registered
- Provider credentials must be valid

### Runtime Validation
- Log warnings for missing optional configuration
- Fail fast for missing required configuration
- Never fall back to hardcoded defaults silently

---

## Code Review Checklist

- [ ] No prompt strings in C# code (except logging/errors)
- [ ] No tool instantiation outside factory/registry
- [ ] No direct provider instantiation (use factory)
- [ ] No hardcoded connection strings
- [ ] Configuration loading uses standard patterns
- [ ] Environment variables for secrets
- [ ] Knowledge queries work without embeddings enabled
- [ ] Metadata filtering precedes embedding search
- [ ] No assumptions that vector store is always available

---

## Related Files

- `src/Core/Agents/Hazina.AgentFactory/` - Agent creation with configuration
- `src/Core/Storage/Hazina.Store.EmbeddingStore/` - Embedding storage implementations
- `src/Core/Storage/Hazina.Store.DocumentStore/` - Document and metadata storage
- `src/Tools/Foundation/Hazina.Tools.Core/Config/` - Configuration classes
- `src/Tools/Services/Hazina.Tools.Services.Prompts/` - Prompt loading services
- `docs/KNOWLEDGE_STORAGE.md` - Knowledge storage architecture
- `docs/README.md` - Framework overview

---

*Last updated: 2026-01-03*
