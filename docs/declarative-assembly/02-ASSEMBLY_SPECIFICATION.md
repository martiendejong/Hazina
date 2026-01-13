# Assembly Specification - Implementation Plan

**Parent Document:** [README.md](./README.md)
**Status:** Planning
**Created:** 2026-01-13

---

## Overview

The Assembly Specification is the **declarative format** that developers use to define complete AI applications without writing code. It references components from the Component Registry and describes how they connect together.

### Design Goals

1. **Human-Readable** - YAML format, easy to write and review
2. **Validated** - JSON Schema ensures correctness before generation
3. **Complete** - Express all aspects of a Hazina application
4. **Extensible** - Support custom components and configurations

---

## Specification Format

### Top-Level Structure

```yaml
# hazina-app.assembly.yaml
version: "1.0"                    # Specification version

metadata:                          # Application metadata
  name: my-rag-assistant
  version: 1.0.0
  description: Production RAG assistant
  author: Development Team

variables:                         # Reusable variables
  embedding_model: text-embedding-3-large
  chunk_size: 512

providers:                         # Infrastructure providers
  llm: { ... }
  embedding: { ... }
  storage: { ... }

pipelines:                         # Processing pipelines
  ingestion: [ ... ]
  retrieval: [ ... ]
  graph: [ ... ]

modules:                           # Application modules
  - { type: module.rag-query, ... }
  - { type: module.document-ingest, ... }

features:                          # Cross-cutting features
  auth: { ... }
  observability: { ... }
  security: { ... }

output:                            # Output configuration
  type: webapi
  framework: aspnet
  settings: { ... }
```

---

## Section Specifications

### 1. Metadata Section

```yaml
metadata:
  # Required
  name: my-rag-assistant           # Project name (alphanumeric, dashes)
  version: 1.0.0                    # Semantic version

  # Optional
  description: |
    Multi-line description of the application.
    Supports markdown formatting.
  author: Team Name
  license: MIT
  repository: https://github.com/org/repo
  tags:
    - rag
    - production
    - enterprise
```

### 2. Variables Section

Variables provide a way to reuse values and reference environment variables.

```yaml
variables:
  # Literal values
  embedding_dimensions: 1536
  chunk_size: 512
  max_tokens: 4096

  # Environment variable references
  openai_key: ${OPENAI_API_KEY}
  supabase_url: ${SUPABASE_URL}
  db_connection: ${DB_CONNECTION_STRING}

  # Computed values (from other variables)
  overlap_size: ${chunk_size / 10}

  # Conditional values
  environment: ${ENV:-development}
  debug: ${DEBUG:-false}
```

Variable usage:
```yaml
providers:
  llm:
    primary:
      type: llm.openai
      config:
        apiKey: ${openai_key}         # Reference variable
        maxTokens: ${max_tokens}
```

### 3. Providers Section

#### LLM Providers

```yaml
providers:
  llm:
    # Single provider
    primary:
      type: llm.openai
      config:
        apiKey: ${OPENAI_API_KEY}
        model: gpt-4o
        temperature: 0.7
        maxTokens: 4096

    # Failover chain
    fallback:
      - type: llm.anthropic
        config:
          apiKey: ${ANTHROPIC_API_KEY}
          model: claude-3-5-sonnet
      - type: llm.ollama
        config:
          model: llama3.1
          endpoint: http://localhost:11434

    # Orchestration settings
    orchestration:
      strategy: priority              # priority, leastCost, fastestResponse, roundRobin
      healthCheck:
        enabled: true
        interval: 30s
      circuitBreaker:
        enabled: true
        failureThreshold: 3
        resetTimeout: 60s
      budget:
        monthly: 500.00
        alertThreshold: 0.8
```

#### Embedding Providers

```yaml
providers:
  embedding:
    type: embedding.openai
    config:
      apiKey: ${OPENAI_API_KEY}
      model: text-embedding-3-large
      dimensions: 1536
      batchSize: 100
```

#### Storage Providers

```yaml
providers:
  storage:
    documents:
      type: storage.local
      config:
        rootPath: ./data/documents
        maxFileSizeMB: 100

    vectors:
      type: vector.supabase
      config:
        url: ${SUPABASE_URL}
        key: ${SUPABASE_KEY}
        table: embeddings
        dimensions: ${embedding_dimensions}

    metadata:
      type: storage.postgres
      config:
        connectionString: ${DB_CONNECTION_STRING}
        schema: hazina

    graph:
      type: graph.sqlite
      config:
        path: ./data/knowledge.db
        enableFTS: true
```

### 4. Pipelines Section

#### Ingestion Pipeline

```yaml
pipelines:
  ingestion:
    # Stage 1: Parse documents
    - stage: parse
      steps:
        - type: parse.auto           # Auto-detect document type
          config:
            supportedTypes: [pdf, docx, html, md, txt]
            extractImages: true
            ocrEnabled: false

    # Stage 2: Chunk documents
    - stage: chunk
      steps:
        - type: chunk.semantic
          config:
            targetSize: ${chunk_size}
            overlap: 50
            minSize: 100
            maxSize: 1000
            preserveHeaders: true

    # Stage 3: Generate embeddings
    - stage: embed
      steps:
        - type: pipeline.embed
          provider: embedding        # Reference providers.embedding

    # Stage 4: Index to store
    - stage: index
      steps:
        - type: pipeline.store
          targets:
            - vectors               # Store embeddings
            - metadata              # Store document metadata

    # Stage 5: Build knowledge graph (optional)
    - stage: graph
      enabled: ${build_graph:-false}
      steps:
        - type: entity.llm
          config:
            model: gpt-4o-mini
            maxEntitiesPerChunk: 20
        - type: relation.llm
          config:
            model: gpt-4o-mini
            maxRelationsPerChunk: 30
        - type: pipeline.store
          targets:
            - graph
```

#### Retrieval Pipeline

```yaml
pipelines:
  retrieval:
    # Stage 1: Query understanding
    - stage: understand
      enabled: ${query_expansion:-false}
      steps:
        - type: query.expand
          config:
            strategy: llm
            expansions: 3

    # Stage 2: Retrieve candidates
    - stage: retrieve
      steps:
        - type: retrieval.hybrid
          config:
            vectorWeight: 0.7
            keywordWeight: 0.3
            topK: 20
            minSimilarity: 0.5

    # Stage 3: Rerank results
    - stage: rerank
      steps:
        - type: rerank.llm
          config:
            model: gpt-4o-mini
            topN: 5
            promptTemplate: |
              Given the query: {query}
              Rate the relevance of this document (1-10):
              {document}

    # Stage 4: Context assembly
    - stage: assemble
      steps:
        - type: context.pack
          config:
            maxTokens: 8000
            format: markdown
            includeCitations: true
```

#### Graph RAG Pipeline

```yaml
pipelines:
  graph-rag:
    - stage: entity-retrieval
      steps:
        - type: retrieval.entity
          config:
            topK: 10
            types: [Person, Organization, Concept]

    - stage: graph-traverse
      steps:
        - type: graph.traverse
          config:
            depth: 2
            relationTypes: [related_to, part_of, causes]
            maxNodes: 50

    - stage: context-merge
      steps:
        - type: context.merge
          config:
            sources: [retrieval, graph-traverse]
            strategy: weighted
            weights:
              retrieval: 0.6
              graph: 0.4
```

### 5. Modules Section

```yaml
modules:
  # RAG Query Module
  - type: module.rag-query
    enabled: true
    config:
      endpoint: /api/query
      methods: [POST]
      pipeline: retrieval           # Reference pipelines.retrieval
      llm: primary                  # Reference providers.llm.primary
      options:
        streaming: true
        citations: true
        confidence: true
        maxTokens: 2000
      rateLimit:
        requests: 100
        period: 1m

  # Document Ingestion Module
  - type: module.document-ingest
    enabled: true
    config:
      endpoint: /api/ingest
      methods: [POST, PUT]
      pipeline: ingestion
      options:
        maxFileSizeMB: 50
        allowedTypes: [pdf, docx, txt, md, html]
        asyncProcessing: true
        webhookOnComplete: ${WEBHOOK_URL:-}
      auth:
        required: true
        scopes: [documents:write]

  # Search Module
  - type: module.search
    enabled: true
    config:
      endpoint: /api/search
      methods: [GET, POST]
      options:
        defaultLimit: 10
        maxLimit: 100
        facets: [type, author, date]

  # Chat Module (WebSocket)
  - type: module.chat
    enabled: true
    config:
      endpoint: /api/chat
      transport: websocket
      options:
        historyLimit: 50
        sessionTimeout: 30m
        memory:
          type: brain.sqlite
          config:
            path: ./data/memory.db

  # Health Check Module
  - type: module.health
    enabled: true
    config:
      endpoint: /health
      checks:
        - llm
        - storage
        - database
```

### 6. Features Section

```yaml
features:
  # Authentication
  auth:
    type: auth.jwt
    config:
      issuer: ${JWT_ISSUER}
      audience: ${JWT_AUDIENCE}
      secretKey: ${JWT_SECRET}
      expirationMinutes: 60
    # Alternative: API Key auth
    # type: auth.apikey
    # config:
    #   headerName: X-API-Key
    #   keys:
    #     - name: production
    #       key: ${API_KEY_PROD}
    #       scopes: [*]

  # Observability
  observability:
    logging:
      level: ${LOG_LEVEL:-Information}
      format: json
      outputs:
        - type: console
        - type: file
          path: ./logs/app.log
        - type: seq
          serverUrl: ${SEQ_URL:-}

    metrics:
      enabled: true
      endpoint: /metrics
      format: prometheus
      include:
        - llm_requests_total
        - llm_tokens_total
        - llm_cost_total
        - retrieval_latency_seconds
        - document_count

    tracing:
      enabled: true
      exporter: otlp
      endpoint: ${OTEL_ENDPOINT:-}
      samplingRate: 0.1

  # Security
  security:
    cors:
      enabled: true
      origins:
        - ${FRONTEND_URL}
        - http://localhost:3000
      methods: [GET, POST, PUT, DELETE, OPTIONS]
      headers: [Authorization, Content-Type]

    rateLimit:
      enabled: true
      global:
        requests: 1000
        period: 1m
      perUser:
        requests: 100
        period: 1m

    inputValidation:
      enabled: true
      maxRequestSizeMB: 10
      promptInjectionDetection: true

  # Caching
  caching:
    enabled: true
    provider: memory              # memory, redis, distributed
    config:
      defaultExpiration: 5m
      maxSize: 1000
    strategies:
      embeddings:
        enabled: true
        expiration: 24h
      llmResponses:
        enabled: false            # Usually not recommended
```

### 7. Output Section

```yaml
output:
  type: webapi                    # webapi, console, worker

  framework: aspnet               # aspnet (default)

  project:
    name: MyRagApp
    namespace: MyCompany.RagApp
    sdk: net9.0
    nullable: enable
    implicitUsings: true

  structure:
    style: clean                  # clean, minimal, layered
    folders:
      - Controllers
      - Services
      - Models
      - Configuration

  docker:
    enabled: true
    baseImage: mcr.microsoft.com/dotnet/aspnet:9.0
    exposePort: 8080

  settings:
    generateAppSettings: true
    environments:
      - Development
      - Production

  documentation:
    swagger:
      enabled: true
      title: ${metadata.name} API
      version: ${metadata.version}
    readme:
      enabled: true
```

---

## JSON Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://hazina.ai/schemas/assembly-1.0.json",
  "title": "Hazina Assembly Specification",
  "description": "Declarative specification for Hazina AI applications",
  "type": "object",
  "required": ["version", "metadata", "providers"],
  "properties": {
    "version": {
      "type": "string",
      "enum": ["1.0"],
      "description": "Specification version"
    },
    "metadata": {
      "$ref": "#/definitions/metadata"
    },
    "variables": {
      "$ref": "#/definitions/variables"
    },
    "providers": {
      "$ref": "#/definitions/providers"
    },
    "pipelines": {
      "$ref": "#/definitions/pipelines"
    },
    "modules": {
      "type": "array",
      "items": { "$ref": "#/definitions/module" }
    },
    "features": {
      "$ref": "#/definitions/features"
    },
    "output": {
      "$ref": "#/definitions/output"
    }
  },
  "definitions": {
    "metadata": {
      "type": "object",
      "required": ["name", "version"],
      "properties": {
        "name": {
          "type": "string",
          "pattern": "^[a-z][a-z0-9-]*$"
        },
        "version": {
          "type": "string",
          "pattern": "^\\d+\\.\\d+\\.\\d+$"
        },
        "description": { "type": "string" },
        "author": { "type": "string" },
        "license": { "type": "string" },
        "repository": { "type": "string", "format": "uri" },
        "tags": {
          "type": "array",
          "items": { "type": "string" }
        }
      }
    },
    "variables": {
      "type": "object",
      "additionalProperties": {
        "oneOf": [
          { "type": "string" },
          { "type": "number" },
          { "type": "boolean" }
        ]
      }
    },
    "providers": {
      "type": "object",
      "properties": {
        "llm": { "$ref": "#/definitions/llmProvider" },
        "embedding": { "$ref": "#/definitions/embeddingProvider" },
        "storage": { "$ref": "#/definitions/storageProviders" }
      }
    },
    "llmProvider": {
      "type": "object",
      "properties": {
        "primary": { "$ref": "#/definitions/providerConfig" },
        "fallback": {
          "type": "array",
          "items": { "$ref": "#/definitions/providerConfig" }
        },
        "orchestration": { "$ref": "#/definitions/orchestrationConfig" }
      }
    },
    "providerConfig": {
      "type": "object",
      "required": ["type"],
      "properties": {
        "type": { "type": "string" },
        "config": { "type": "object" }
      }
    },
    "pipelines": {
      "type": "object",
      "properties": {
        "ingestion": { "$ref": "#/definitions/pipeline" },
        "retrieval": { "$ref": "#/definitions/pipeline" },
        "graph": { "$ref": "#/definitions/pipeline" },
        "graph-rag": { "$ref": "#/definitions/pipeline" }
      }
    },
    "pipeline": {
      "type": "array",
      "items": { "$ref": "#/definitions/pipelineStage" }
    },
    "pipelineStage": {
      "type": "object",
      "required": ["stage", "steps"],
      "properties": {
        "stage": { "type": "string" },
        "enabled": { "type": "boolean", "default": true },
        "steps": {
          "type": "array",
          "items": { "$ref": "#/definitions/pipelineStep" }
        }
      }
    },
    "pipelineStep": {
      "type": "object",
      "required": ["type"],
      "properties": {
        "type": { "type": "string" },
        "config": { "type": "object" }
      }
    },
    "module": {
      "type": "object",
      "required": ["type"],
      "properties": {
        "type": { "type": "string" },
        "enabled": { "type": "boolean", "default": true },
        "config": { "type": "object" }
      }
    },
    "features": {
      "type": "object",
      "properties": {
        "auth": { "type": "object" },
        "observability": { "type": "object" },
        "security": { "type": "object" },
        "caching": { "type": "object" }
      }
    },
    "output": {
      "type": "object",
      "required": ["type"],
      "properties": {
        "type": {
          "type": "string",
          "enum": ["webapi", "console", "worker"]
        },
        "framework": {
          "type": "string",
          "default": "aspnet"
        },
        "project": { "type": "object" },
        "docker": { "type": "object" },
        "documentation": { "type": "object" }
      }
    }
  }
}
```

---

## Implementation Classes

### File Structure

```
src/Core/AI/Hazina.AI.Assembly/
├── Specification/
│   ├── AssemblySpec.cs
│   ├── MetadataSpec.cs
│   ├── ProvidersSpec.cs
│   ├── PipelinesSpec.cs
│   ├── ModulesSpec.cs
│   ├── FeaturesSpec.cs
│   ├── OutputSpec.cs
│   └── VariableResolver.cs
├── Parsing/
│   ├── SpecificationParser.cs
│   ├── YamlSpecParser.cs
│   └── JsonSpecParser.cs
├── Validation/
│   ├── SpecificationValidator.cs
│   ├── ComponentReferenceValidator.cs
│   ├── DependencyValidator.cs
│   └── ValidationResult.cs
└── Schema/
    ├── assembly-1.0.json
    └── SchemaLoader.cs
```

### Core Model Classes

```csharp
// AssemblySpec.cs
namespace Hazina.AI.Assembly.Specification;

public record AssemblySpec
{
    public required string Version { get; init; }
    public required MetadataSpec Metadata { get; init; }
    public Dictionary<string, object>? Variables { get; init; }
    public required ProvidersSpec Providers { get; init; }
    public PipelinesSpec? Pipelines { get; init; }
    public List<ModuleSpec>? Modules { get; init; }
    public FeaturesSpec? Features { get; init; }
    public OutputSpec? Output { get; init; }
}

public record MetadataSpec
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public string? Author { get; init; }
    public string? License { get; init; }
    public string? Repository { get; init; }
    public List<string>? Tags { get; init; }
}

public record ProvidersSpec
{
    public LlmProviderSpec? Llm { get; init; }
    public ProviderConfigSpec? Embedding { get; init; }
    public StorageProvidersSpec? Storage { get; init; }
}

public record LlmProviderSpec
{
    public ProviderConfigSpec? Primary { get; init; }
    public List<ProviderConfigSpec>? Fallback { get; init; }
    public OrchestrationSpec? Orchestration { get; init; }
}

public record ProviderConfigSpec
{
    public required string Type { get; init; }
    public Dictionary<string, object>? Config { get; init; }
}

public record PipelineSpec
{
    public required string Stage { get; init; }
    public bool Enabled { get; init; } = true;
    public required List<PipelineStepSpec> Steps { get; init; }
}

public record PipelineStepSpec
{
    public required string Type { get; init; }
    public Dictionary<string, object>? Config { get; init; }
}

public record ModuleSpec
{
    public required string Type { get; init; }
    public bool Enabled { get; init; } = true;
    public Dictionary<string, object>? Config { get; init; }
}
```

### Variable Resolution

```csharp
// VariableResolver.cs
namespace Hazina.AI.Assembly.Specification;

public class VariableResolver
{
    private readonly Dictionary<string, object> _variables;
    private static readonly Regex VariablePattern = new(@"\$\{([^}]+)\}");

    public VariableResolver(Dictionary<string, object>? variables)
    {
        _variables = variables ?? new Dictionary<string, object>();

        // Add environment variables with ENV_ prefix
        foreach (var env in Environment.GetEnvironmentVariables())
        {
            if (env is DictionaryEntry entry &&
                entry.Key is string key &&
                entry.Value is string value)
            {
                _variables[$"ENV_{key}"] = value;

                // Also add without prefix for direct ${VAR_NAME} references
                if (!_variables.ContainsKey(key))
                {
                    _variables[key] = value;
                }
            }
        }
    }

    public string Resolve(string template)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        return VariablePattern.Replace(template, match =>
        {
            var expression = match.Groups[1].Value;
            return EvaluateExpression(expression);
        });
    }

    public T Resolve<T>(T obj) where T : class
    {
        // Deep resolve all string properties in object
        var json = JsonSerializer.Serialize(obj);
        var resolved = Resolve(json);
        return JsonSerializer.Deserialize<T>(resolved)!;
    }

    private string EvaluateExpression(string expression)
    {
        // Handle default values: ${VAR:-default}
        if (expression.Contains(":-"))
        {
            var parts = expression.Split(":-", 2);
            var varName = parts[0].Trim();
            var defaultValue = parts[1];

            return _variables.TryGetValue(varName, out var value)
                ? value.ToString() ?? defaultValue
                : defaultValue;
        }

        // Handle simple variable reference
        if (_variables.TryGetValue(expression.Trim(), out var result))
        {
            return result.ToString() ?? "";
        }

        throw new VariableNotFoundException(expression);
    }
}
```

### Specification Parsing

```csharp
// SpecificationParser.cs
namespace Hazina.AI.Assembly.Parsing;

public class SpecificationParser
{
    private readonly IComponentRegistry _registry;
    private readonly SpecificationValidator _validator;
    private readonly ILogger<SpecificationParser> _logger;

    public async Task<AssemblySpec> ParseAsync(string path)
    {
        var content = await File.ReadAllTextAsync(path);
        return Parse(content, Path.GetExtension(path));
    }

    public AssemblySpec Parse(string content, string format)
    {
        AssemblySpec spec = format.ToLower() switch
        {
            ".yaml" or ".yml" => ParseYaml(content),
            ".json" => ParseJson(content),
            _ => throw new UnsupportedFormatException(format)
        };

        // Validate against schema
        var schemaResult = _validator.ValidateSchema(spec);
        if (!schemaResult.IsValid)
        {
            throw new SchemaValidationException(schemaResult.Errors);
        }

        // Resolve variables
        var resolver = new VariableResolver(spec.Variables);
        spec = resolver.Resolve(spec);

        // Validate component references
        var refResult = _validator.ValidateComponentReferences(spec, _registry);
        if (!refResult.IsValid)
        {
            throw new ComponentReferenceException(refResult.Errors);
        }

        // Validate dependencies
        var depResult = _validator.ValidateDependencies(spec, _registry);
        if (!depResult.IsValid)
        {
            throw new DependencyValidationException(depResult.Errors);
        }

        return spec;
    }

    private AssemblySpec ParseYaml(string content)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<AssemblySpec>(content);
    }

    private AssemblySpec ParseJson(string content)
    {
        return JsonSerializer.Deserialize<AssemblySpec>(content,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;
    }
}
```

---

## Validation Rules

### Schema Validation
- All required fields present
- Types match schema definitions
- Enum values are valid
- Patterns match (names, versions)

### Component Reference Validation
- All `type` fields reference valid component IDs
- Components exist in registry
- Component versions are compatible

### Dependency Validation
- Provider dependencies satisfied (e.g., embedding requires LLM for some operations)
- Pipeline dependencies resolved (e.g., retrieval needs storage)
- No circular dependencies

### Configuration Validation
- Required config properties present
- Config values match expected types
- Secrets have environment variable references
- Numeric values within bounds

---

## Example Specifications

### Minimal RAG App

```yaml
version: "1.0"
metadata:
  name: minimal-rag
  version: 1.0.0

providers:
  llm:
    primary:
      type: llm.openai
      config:
        apiKey: ${OPENAI_API_KEY}
        model: gpt-4o

  embedding:
    type: embedding.openai
    config:
      apiKey: ${OPENAI_API_KEY}
      model: text-embedding-3-small

  storage:
    vectors:
      type: vector.memory

modules:
  - type: module.rag-query
  - type: module.document-ingest
```

### Production Enterprise RAG

```yaml
version: "1.0"
metadata:
  name: enterprise-rag
  version: 2.0.0
  description: Production-grade RAG with multi-provider failover

variables:
  chunk_size: 512
  embedding_dims: 1536

providers:
  llm:
    primary:
      type: llm.openai
      config:
        apiKey: ${OPENAI_API_KEY}
        model: gpt-4o
    fallback:
      - type: llm.anthropic
        config:
          apiKey: ${ANTHROPIC_API_KEY}
          model: claude-3-5-sonnet
    orchestration:
      strategy: priority
      healthCheck:
        enabled: true
        interval: 30s
      budget:
        monthly: 1000.00

  embedding:
    type: embedding.openai
    config:
      apiKey: ${OPENAI_API_KEY}
      model: text-embedding-3-large
      dimensions: ${embedding_dims}

  storage:
    documents:
      type: storage.local
      config:
        rootPath: ./data/documents
    vectors:
      type: vector.supabase
      config:
        url: ${SUPABASE_URL}
        key: ${SUPABASE_KEY}
    graph:
      type: graph.sqlite
      config:
        path: ./data/knowledge.db

pipelines:
  ingestion:
    - stage: parse
      steps:
        - type: parse.auto
    - stage: chunk
      steps:
        - type: chunk.semantic
          config:
            targetSize: ${chunk_size}
    - stage: embed
      steps:
        - type: pipeline.embed
    - stage: index
      steps:
        - type: pipeline.store
          targets: [vectors, metadata]
    - stage: graph
      steps:
        - type: entity.llm
        - type: relation.llm
        - type: pipeline.store
          targets: [graph]

  retrieval:
    - stage: retrieve
      steps:
        - type: retrieval.hybrid
          config:
            vectorWeight: 0.7
            topK: 20
    - stage: rerank
      steps:
        - type: rerank.llm
          config:
            topN: 5

modules:
  - type: module.rag-query
    config:
      endpoint: /api/query
      pipeline: retrieval
  - type: module.document-ingest
    config:
      endpoint: /api/documents
  - type: module.search
    config:
      endpoint: /api/search
  - type: module.health

features:
  auth:
    type: auth.jwt
    config:
      issuer: ${JWT_ISSUER}
      secretKey: ${JWT_SECRET}
  observability:
    logging:
      level: Information
    metrics:
      enabled: true
      endpoint: /metrics
  security:
    cors:
      enabled: true
      origins: [${FRONTEND_URL}]
    rateLimit:
      enabled: true
      requests: 100
      period: 1m

output:
  type: webapi
  framework: aspnet
  project:
    name: EnterpriseRag
    sdk: net9.0
  docker:
    enabled: true
```

---

## Implementation Tasks

### Week 1: Core Models
- [ ] Create AssemblySpec and related model classes
- [ ] Implement YAML/JSON parsing
- [ ] Create VariableResolver
- [ ] Add basic unit tests

### Week 2: Validation
- [ ] Create JSON Schema for specification
- [ ] Implement SchemaValidator
- [ ] Implement ComponentReferenceValidator
- [ ] Implement DependencyValidator
- [ ] Comprehensive validation tests

### Week 3: Integration
- [ ] Integrate with ComponentRegistry
- [ ] Create specification examples
- [ ] End-to-end parsing tests
- [ ] Error message improvements

### Week 4: Documentation
- [ ] Write specification guide
- [ ] Create example library
- [ ] IDE integration (VS Code schema)
- [ ] Specification linting rules

---

## Success Criteria

- [ ] Parse all example specifications without errors
- [ ] Variable resolution handles all edge cases
- [ ] Validation catches common mistakes with clear messages
- [ ] Integration with ComponentRegistry working
- [ ] JSON Schema provides IDE support

---

**Next Document:** [03-SCAFFOLD_GENERATOR.md](./03-SCAFFOLD_GENERATOR.md)
