# Component Registry - Implementation Plan

**Parent Document:** [README.md](./README.md)
**Status:** Planning
**Created:** 2026-01-13

---

## Overview

The Component Registry is the **foundation** of the declarative assembly system. It provides a machine-readable catalog of all Hazina components that can be assembled into applications.

### Purpose

1. **Discovery** - List available components programmatically
2. **Validation** - Verify component references in assembly specs
3. **Resolution** - Map component identifiers to runtime implementations
4. **Documentation** - Generate docs from component metadata

---

## Registry Architecture

```
ComponentRegistry
    │
    ├─► ComponentCatalog
    │       │
    │       ├─► ProviderComponents (LLM, Embedding, Storage)
    │       ├─► PipelineComponents (Ingestion, Retrieval, Graph)
    │       ├─► ModuleComponents (API endpoints, features)
    │       └─► FrontendComponents (optional UI scaffolds)
    │
    ├─► SchemaValidator
    │       └─► Validates component definitions
    │
    ├─► RuntimeResolver
    │       └─► Maps components to DI registrations
    │
    └─► DocumentationGenerator
            └─► Creates docs from component metadata
```

---

## Component Categories

Based on analysis of Hazina's existing interfaces:

### 1. Provider Components

#### LLM Providers
| Component ID | Interface | Implementation | Config Class |
|-------------|-----------|----------------|--------------|
| `llm.openai` | ILLMClient | OpenAIClientWrapper | OpenAIConfig |
| `llm.anthropic` | ILLMClient | ClaudeClientWrapper | AnthropicConfig |
| `llm.gemini` | ILLMClient | GeminiClientWrapper | GeminiConfig |
| `llm.ollama` | ILLMClient | OllamaClientWrapper | OllamaConfig |
| `llm.mistral` | ILLMClient | MistralClientWrapper | MistralConfig |
| `llm.huggingface` | ILLMClient | HuggingFaceClientWrapper | HuggingFaceConfig |

#### Embedding Providers
| Component ID | Interface | Implementation | Config Class |
|-------------|-----------|----------------|--------------|
| `embedding.openai` | IEmbeddingGenerator | OpenAIEmbeddingGenerator | OpenAIConfig |
| `embedding.azureopenai` | IEmbeddingGenerator | AzureOpenAIEmbeddingGenerator | AzureOpenAIConfig |
| `embedding.local` | IEmbeddingGenerator | LocalEmbeddingGenerator | LocalEmbeddingConfig |

#### Storage Providers
| Component ID | Interface | Implementation | Config Class |
|-------------|-----------|----------------|--------------|
| `storage.local` | IDocumentStore | FileDocumentStore | FileStoreConfig |
| `storage.supabase` | IDocumentStore | SupabaseDocumentStore | SupabaseConfig |
| `storage.postgres` | IDocumentStore | PostgresDocumentStore | PostgresConfig |
| `vector.memory` | IEmbeddingStore | InMemoryVectorStore | - |
| `vector.file` | IEmbeddingStore | FileEmbeddingStore | FileStoreConfig |
| `vector.supabase` | IEmbeddingStore | SupabaseEmbeddingStore | SupabaseConfig |
| `vector.pgvector` | IEmbeddingStore | PgVectorStore | PostgresConfig |
| `graph.sqlite` | IGraphStore | SqliteGraphStore | SqliteConfig |
| `graph.neo4j` | IGraphStore | Neo4jGraphStore | Neo4jConfig |
| `graph.memory` | IGraphStore | InMemoryGraphStore | - |

### 2. Pipeline Components

#### Ingestion Pipelines
| Component ID | Interface | Description |
|-------------|-----------|-------------|
| `chunk.fixed` | ITextChunker | Fixed-size chunking |
| `chunk.sentence` | ITextChunker | Sentence-boundary chunking |
| `chunk.paragraph` | ITextChunker | Paragraph-boundary chunking |
| `chunk.semantic` | ITextChunker | Semantic coherence chunking |
| `parse.pdf` | IDocumentParser | PDF text extraction |
| `parse.docx` | IDocumentParser | Word document parsing |
| `parse.html` | IDocumentParser | HTML content extraction |
| `ingest.firecrawl` | IIngestionSource | Web crawling |
| `ingest.filesystem` | IIngestionSource | Local file system |
| `ingest.googledrive` | IIngestionSource | Google Drive integration |

#### Retrieval Pipelines
| Component ID | Interface | Description |
|-------------|-----------|-------------|
| `retrieval.vector` | IRetriever | Vector similarity search |
| `retrieval.metadata` | IContextRetriever | Metadata-based filtering |
| `retrieval.semantic` | IContextRetriever | Semantic search |
| `retrieval.facts` | IContextRetriever | Fact store lookup |
| `retrieval.hybrid` | IRetriever | Combined vector + keyword |
| `rerank.none` | IReranker | Pass-through (no reranking) |
| `rerank.llm` | IReranker | LLM-based reranking |
| `rerank.crossencoder` | IReranker | Cross-encoder model |

#### Graph Pipelines
| Component ID | Interface | Description |
|-------------|-----------|-------------|
| `entity.llm` | IEntityExtractor | LLM-based entity extraction |
| `relation.llm` | IRelationshipExtractor | LLM relationship extraction |
| `normalize.embedding` | IEntityNormalizer | Embedding-based dedup |
| `normalize.exact` | IEntityNormalizer | Exact match dedup |

### 3. Module Components

#### Core Modules
| Component ID | Purpose | Generated Artifacts |
|-------------|---------|---------------------|
| `module.rag-query` | RAG question answering | QueryController, QueryService |
| `module.document-ingest` | Document ingestion | IngestController, IngestService |
| `module.search` | Semantic search | SearchController, SearchService |
| `module.chat` | Conversational interface | ChatController, ChatHub |
| `module.health` | Health check endpoint | HealthController |
| `module.metrics` | Prometheus metrics | MetricsMiddleware |

#### Auth Modules
| Component ID | Purpose | Generated Artifacts |
|-------------|---------|---------------------|
| `auth.jwt` | JWT authentication | JwtMiddleware, TokenService |
| `auth.azuread` | Azure AD integration | AzureADConfig, ClaimsTransform |
| `auth.apikey` | API key authentication | ApiKeyMiddleware |

#### Feature Modules
| Component ID | Purpose | Generated Artifacts |
|-------------|---------|---------------------|
| `feature.swagger` | OpenAPI documentation | SwaggerConfig |
| `feature.cors` | CORS configuration | CorsConfig |
| `feature.ratelimit` | Rate limiting | RateLimitMiddleware |
| `feature.caching` | Response caching | CacheService |

### 4. Frontend Components (Optional)

| Component ID | Purpose | Technology |
|-------------|---------|------------|
| `frontend.react-chat` | Chat panel | React + TypeScript |
| `frontend.react-admin` | Admin dashboard | React + TypeScript |
| `frontend.blazor-chat` | Chat panel | Blazor WASM |

---

## Component Definition Schema

### JSON Schema for Component Definitions

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Hazina Component Definition",
  "type": "object",
  "required": ["id", "type", "category", "interface"],
  "properties": {
    "id": {
      "type": "string",
      "pattern": "^[a-z]+\\.[a-z0-9-]+$",
      "description": "Unique component identifier (e.g., 'llm.openai')"
    },
    "type": {
      "type": "string",
      "description": "Full type name including namespace"
    },
    "category": {
      "type": "string",
      "enum": ["provider", "pipeline", "module", "frontend"]
    },
    "interface": {
      "type": "string",
      "description": "Primary interface this component implements"
    },
    "config": {
      "type": "object",
      "properties": {
        "type": {
          "type": "string",
          "description": "Configuration class type name"
        },
        "schema": {
          "$ref": "#/definitions/configSchema"
        }
      }
    },
    "dependencies": {
      "type": "array",
      "items": {
        "type": "string"
      },
      "description": "Other component IDs this depends on"
    },
    "nugetPackage": {
      "type": "string",
      "description": "NuGet package containing this component"
    },
    "serviceLifetime": {
      "type": "string",
      "enum": ["Singleton", "Scoped", "Transient"],
      "default": "Scoped"
    },
    "metadata": {
      "type": "object",
      "properties": {
        "displayName": { "type": "string" },
        "description": { "type": "string" },
        "documentation": { "type": "string" },
        "tags": { "type": "array", "items": { "type": "string" } },
        "version": { "type": "string" },
        "deprecated": { "type": "boolean" },
        "deprecationMessage": { "type": "string" }
      }
    }
  }
}
```

### Example Component Definition

```yaml
# components/providers/llm.openai.yaml
id: llm.openai
type: Hazina.LLMs.OpenAI.OpenAIClientWrapper
category: provider
interface: Hazina.LLMs.Client.ILLMClient

config:
  type: Hazina.LLMs.OpenAI.OpenAIConfig
  schema:
    properties:
      apiKey:
        type: string
        required: true
        secret: true
        description: OpenAI API key
        envVar: OPENAI_API_KEY
      model:
        type: string
        required: true
        default: gpt-4o
        enum: [gpt-4o, gpt-4o-mini, gpt-4-turbo, gpt-3.5-turbo]
      endpoint:
        type: string
        required: false
        description: Custom endpoint URL (for Azure OpenAI)
      organizationId:
        type: string
        required: false
        description: OpenAI organization ID
      maxRetries:
        type: integer
        default: 3
        min: 0
        max: 10
      timeout:
        type: integer
        default: 60000
        description: Request timeout in milliseconds

dependencies: []
nugetPackage: Hazina.LLMs.OpenAI
serviceLifetime: Singleton

metadata:
  displayName: OpenAI
  description: OpenAI GPT models (GPT-4o, GPT-4, GPT-3.5)
  documentation: https://platform.openai.com/docs
  tags: [llm, openai, gpt, cloud]
  version: 2.0.0
```

---

## Implementation Classes

### File Structure

```
src/Core/AI/Hazina.AI.Assembly/
├── Registry/
│   ├── ComponentCatalog.cs
│   ├── ComponentDefinition.cs
│   ├── ComponentResolver.cs
│   ├── IComponentRegistry.cs
│   └── ComponentRegistry.cs
├── Schema/
│   ├── component-schema.json
│   ├── SchemaValidator.cs
│   └── ValidationResult.cs
├── Resolution/
│   ├── DependencyGraph.cs
│   ├── RuntimeResolver.cs
│   └── ServiceRegistration.cs
└── Documentation/
    ├── ComponentDocGenerator.cs
    └── MarkdownFormatter.cs
```

### Core Interfaces

```csharp
// IComponentRegistry.cs
namespace Hazina.AI.Assembly.Registry;

public interface IComponentRegistry
{
    /// <summary>
    /// Get all registered components
    /// </summary>
    IReadOnlyList<ComponentDefinition> GetAll();

    /// <summary>
    /// Get component by ID (e.g., "llm.openai")
    /// </summary>
    ComponentDefinition? GetById(string componentId);

    /// <summary>
    /// Get components by category
    /// </summary>
    IReadOnlyList<ComponentDefinition> GetByCategory(ComponentCategory category);

    /// <summary>
    /// Get components implementing interface
    /// </summary>
    IReadOnlyList<ComponentDefinition> GetByInterface(Type interfaceType);

    /// <summary>
    /// Search components by tags
    /// </summary>
    IReadOnlyList<ComponentDefinition> SearchByTags(params string[] tags);

    /// <summary>
    /// Validate component ID exists
    /// </summary>
    bool Exists(string componentId);

    /// <summary>
    /// Resolve dependencies for component
    /// </summary>
    DependencyGraph ResolveDependencies(string componentId);

    /// <summary>
    /// Generate DI registration for component
    /// </summary>
    ServiceRegistration GetServiceRegistration(
        string componentId,
        Dictionary<string, object> config);
}
```

```csharp
// ComponentDefinition.cs
namespace Hazina.AI.Assembly.Registry;

public record ComponentDefinition
{
    public required string Id { get; init; }
    public required Type ImplementationType { get; init; }
    public required ComponentCategory Category { get; init; }
    public required Type InterfaceType { get; init; }
    public Type? ConfigType { get; init; }
    public ConfigSchema? ConfigSchema { get; init; }
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public string? NuGetPackage { get; init; }
    public ServiceLifetime Lifetime { get; init; } = ServiceLifetime.Scoped;
    public ComponentMetadata Metadata { get; init; } = new();
}

public enum ComponentCategory
{
    Provider,
    Pipeline,
    Module,
    Frontend
}

public record ComponentMetadata
{
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Documentation { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? Version { get; init; }
    public bool Deprecated { get; init; }
    public string? DeprecationMessage { get; init; }
}

public record ConfigSchema
{
    public IReadOnlyDictionary<string, ConfigProperty> Properties { get; init; }
        = new Dictionary<string, ConfigProperty>();
}

public record ConfigProperty
{
    public required string Type { get; init; }
    public bool Required { get; init; }
    public object? Default { get; init; }
    public string? Description { get; init; }
    public bool Secret { get; init; }
    public string? EnvVar { get; init; }
    public IReadOnlyList<object>? Enum { get; init; }
    public object? Min { get; init; }
    public object? Max { get; init; }
}
```

### Component Catalog Loading

```csharp
// ComponentCatalog.cs
namespace Hazina.AI.Assembly.Registry;

public class ComponentCatalog
{
    private readonly Dictionary<string, ComponentDefinition> _components = new();
    private readonly ILogger<ComponentCatalog> _logger;

    public ComponentCatalog(ILogger<ComponentCatalog> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load components from embedded YAML files
    /// </summary>
    public void LoadFromEmbeddedResources()
    {
        var assembly = typeof(ComponentCatalog).Assembly;
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".component.yaml") || n.EndsWith(".component.json"));

        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            var definition = ParseComponentDefinition(content, resourceName);
            if (definition != null)
            {
                Register(definition);
            }
        }

        _logger.LogInformation("Loaded {Count} components from embedded resources",
            _components.Count);
    }

    /// <summary>
    /// Load components from directory
    /// </summary>
    public void LoadFromDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogWarning("Component directory not found: {Path}", path);
            return;
        }

        var files = Directory.GetFiles(path, "*.component.yaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(path, "*.component.json", SearchOption.AllDirectories));

        foreach (var file in files)
        {
            try
            {
                var content = File.ReadAllText(file);
                var definition = ParseComponentDefinition(content, file);
                if (definition != null)
                {
                    Register(definition);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load component from {File}", file);
            }
        }
    }

    public void Register(ComponentDefinition definition)
    {
        if (_components.ContainsKey(definition.Id))
        {
            _logger.LogWarning("Overwriting component: {Id}", definition.Id);
        }
        _components[definition.Id] = definition;
    }

    public ComponentDefinition? Get(string id) =>
        _components.TryGetValue(id, out var def) ? def : null;

    public IReadOnlyList<ComponentDefinition> GetAll() =>
        _components.Values.ToList();

    public IReadOnlyList<ComponentDefinition> GetByCategory(ComponentCategory category) =>
        _components.Values.Where(c => c.Category == category).ToList();

    private ComponentDefinition? ParseComponentDefinition(string content, string source)
    {
        // Implementation: Parse YAML/JSON to ComponentDefinition
        // Use YamlDotNet or System.Text.Json
        throw new NotImplementedException();
    }
}
```

### Runtime Resolution

```csharp
// RuntimeResolver.cs
namespace Hazina.AI.Assembly.Resolution;

public class RuntimeResolver
{
    private readonly IComponentRegistry _registry;
    private readonly ILogger<RuntimeResolver> _logger;

    /// <summary>
    /// Generate IServiceCollection registrations for a component
    /// </summary>
    public ServiceRegistration Resolve(
        string componentId,
        Dictionary<string, object> configuration)
    {
        var component = _registry.GetById(componentId)
            ?? throw new ComponentNotFoundException(componentId);

        // Validate configuration against schema
        ValidateConfiguration(component, configuration);

        // Resolve dependencies
        var dependencies = _registry.ResolveDependencies(componentId);

        // Create registration
        return new ServiceRegistration
        {
            ServiceType = component.InterfaceType,
            ImplementationType = component.ImplementationType,
            Lifetime = component.Lifetime,
            ConfigurationFactory = (sp) => CreateConfiguration(component, configuration, sp),
            Dependencies = dependencies.GetOrdered()
                .Select(d => Resolve(d.Id, new Dictionary<string, object>()))
                .ToList()
        };
    }

    /// <summary>
    /// Apply registrations to IServiceCollection
    /// </summary>
    public void ApplyToServices(
        IServiceCollection services,
        ServiceRegistration registration)
    {
        // Register dependencies first
        foreach (var dep in registration.Dependencies)
        {
            ApplyToServices(services, dep);
        }

        // Register main service
        var descriptor = new ServiceDescriptor(
            registration.ServiceType,
            sp => {
                var config = registration.ConfigurationFactory(sp);
                return ActivatorUtilities.CreateInstance(
                    sp,
                    registration.ImplementationType,
                    config);
            },
            registration.Lifetime);

        services.Add(descriptor);
    }
}

public record ServiceRegistration
{
    public required Type ServiceType { get; init; }
    public required Type ImplementationType { get; init; }
    public ServiceLifetime Lifetime { get; init; }
    public required Func<IServiceProvider, object?> ConfigurationFactory { get; init; }
    public IReadOnlyList<ServiceRegistration> Dependencies { get; init; } = [];
}
```

---

## Catalog File Organization

```
src/Core/AI/Hazina.AI.Assembly/
└── Components/
    ├── providers/
    │   ├── llm/
    │   │   ├── llm.openai.component.yaml
    │   │   ├── llm.anthropic.component.yaml
    │   │   ├── llm.gemini.component.yaml
    │   │   ├── llm.ollama.component.yaml
    │   │   └── llm.mistral.component.yaml
    │   ├── embedding/
    │   │   ├── embedding.openai.component.yaml
    │   │   ├── embedding.azureopenai.component.yaml
    │   │   └── embedding.local.component.yaml
    │   └── storage/
    │       ├── storage.local.component.yaml
    │       ├── storage.supabase.component.yaml
    │       ├── vector.memory.component.yaml
    │       ├── vector.pgvector.component.yaml
    │       └── graph.sqlite.component.yaml
    ├── pipelines/
    │   ├── chunking/
    │   │   ├── chunk.fixed.component.yaml
    │   │   ├── chunk.semantic.component.yaml
    │   │   └── chunk.paragraph.component.yaml
    │   ├── retrieval/
    │   │   ├── retrieval.vector.component.yaml
    │   │   ├── retrieval.hybrid.component.yaml
    │   │   └── rerank.llm.component.yaml
    │   └── graph/
    │       ├── entity.llm.component.yaml
    │       └── relation.llm.component.yaml
    └── modules/
        ├── core/
        │   ├── module.rag-query.component.yaml
        │   ├── module.document-ingest.component.yaml
        │   └── module.health.component.yaml
        └── auth/
            ├── auth.jwt.component.yaml
            └── auth.apikey.component.yaml
```

---

## DI Extension Method Generation

The registry should be able to generate extension methods for easy service registration:

```csharp
// Generated: HazinaAssemblyExtensions.cs
public static class HazinaAssemblyExtensions
{
    public static IServiceCollection AddHazinaFromSpec(
        this IServiceCollection services,
        string specPath)
    {
        var resolver = new AssemblyResolver();
        var spec = resolver.LoadSpecification(specPath);

        foreach (var component in spec.Components)
        {
            var registration = resolver.Resolve(component.Id, component.Config);
            registration.ApplyTo(services);
        }

        return services;
    }

    public static IServiceCollection AddHazinaComponent(
        this IServiceCollection services,
        string componentId,
        Action<ComponentConfigurationBuilder>? configure = null)
    {
        var builder = new ComponentConfigurationBuilder();
        configure?.Invoke(builder);

        var registry = ComponentRegistry.Default;
        var resolver = new RuntimeResolver(registry);
        var registration = resolver.Resolve(componentId, builder.Build());

        resolver.ApplyToServices(services, registration);

        return services;
    }
}

// Usage:
services.AddHazinaComponent("llm.openai", c => c
    .Set("apiKey", config["OpenAI:ApiKey"])
    .Set("model", "gpt-4o"));
```

---

## Implementation Tasks

### Week 1: Schema & Models
- [ ] Create component definition JSON schema
- [ ] Implement ComponentDefinition model class
- [ ] Implement ConfigSchema model
- [ ] Add YAML/JSON parsing support
- [ ] Write unit tests for parsing

### Week 2: Catalog & Registry
- [ ] Implement ComponentCatalog class
- [ ] Implement ComponentRegistry interface
- [ ] Add embedded resource loading
- [ ] Add directory-based loading
- [ ] Create initial component definitions (10 core components)

### Week 3: Resolution & DI
- [ ] Implement DependencyGraph
- [ ] Implement RuntimeResolver
- [ ] Create ServiceRegistration model
- [ ] Generate IServiceCollection extensions
- [ ] Integration tests with actual Hazina types

### Week 4: Documentation & Polish
- [ ] Implement ComponentDocGenerator
- [ ] Generate markdown documentation
- [ ] Add validation error messages
- [ ] Complete component coverage (all providers)
- [ ] API documentation

---

## Success Criteria

- [ ] 80% of existing Hazina interfaces have component definitions
- [ ] Component loading works from both embedded resources and filesystem
- [ ] Runtime resolver correctly creates DI registrations
- [ ] Configuration validation catches invalid specs
- [ ] Documentation generation produces useful output
- [ ] Unit test coverage > 80%

---

## Dependencies

### NuGet Packages Required
- `YamlDotNet` - YAML parsing
- `NJsonSchema` - JSON Schema validation
- `Microsoft.Extensions.DependencyInjection.Abstractions`

### Hazina Packages Referenced
All packages that contain components to be registered.

---

**Next Document:** [02-ASSEMBLY_SPECIFICATION.md](./02-ASSEMBLY_SPECIFICATION.md)
