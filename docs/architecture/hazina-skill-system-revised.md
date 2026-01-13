# Hazina Skill System - Revised Architecture (Skills as Tools)

**Generated:** 2026-01-13 (Revised)
**Status:** Simplified approach based on existing MCP/Tool infrastructure

---

## Core Principle

**Skills are just specialized MCP tools that can orchestrate workflows.**

No custom formats. No separate execution engine. Skills leverage existing:
- `AgentTool` base class
- `ToolRegistry` for discovery
- MCP protocol for invocation
- `WorkflowEngine` for orchestration
- Markdown for documentation

---

## What Actually Exists vs. What's Needed

### ✅ Already Exists (90% Complete)

1. **Tool Infrastructure**
   - `AgentTool.cs` - Base class for all tools ✅
   - `ToolRegistry.cs` - Central tool registration ✅
   - `McpClient.cs` / `McpServer.cs` - MCP protocol ✅
   - Parameter validation, execution model ✅

2. **Orchestration**
   - `WorkflowEngine.cs` - Sequential, parallel, conditional, loops ✅
   - `TaskOrchestrator.cs` - Dependency resolution ✅
   - Context passing between steps ✅

3. **Store Access**
   - DocumentStore, EmbeddingStore, GraphStore all accessible ✅
   - Pipeline infrastructure (chunking, embedding, retrieval) ✅

### 🟡 What's Missing (10%)

1. **Composite Tool Pattern**
   - Need a `CompositeToolBase` class that wraps WorkflowEngine
   - Makes it easy to create tools that orchestrate other tools
   - Skills extend this class

2. **Skill Documentation Pattern**
   - Standardize markdown format for skill docs
   - Similar to Claude Skills (`.claude/skills/<name>/SKILL.md`)
   - LLMs read these to understand what skills do

3. **Tool Bundling/Discovery**
   - Group related tools into "skill packs"
   - Convention: `skills/<category>/<skill-name>/`
   - Each skill has: `Tool.cs`, `SKILL.md`, optional `examples/`, `tests/`

4. **Examples**
   - 5-10 reference skills showing the pattern

---

## Implementation: Skills as Tools

### Pattern 1: Simple Skill (Single Operation)

```csharp
// skills/rag/document-chunker/DocumentChunkerTool.cs
public class DocumentChunkerTool : AgentTool
{
    private readonly ITextChunker _chunker;

    public DocumentChunkerTool(ITextChunker chunker)
    {
        _chunker = chunker;
        Name = "document-chunker";
        Description = "Chunk documents into semantic segments for RAG indexing";

        Parameters = new Dictionary<string, ToolParameter>
        {
            ["documentPath"] = new ToolParameter
            {
                Type = "string",
                Description = "Path to document",
                Required = true
            },
            ["strategy"] = new ToolParameter
            {
                Type = "string",
                Description = "Chunking strategy: fixed, hierarchical, semantic",
                DefaultValue = "semantic"
            },
            ["chunkSize"] = new ToolParameter
            {
                Type = "integer",
                Description = "Target chunk size in tokens",
                DefaultValue = 512
            }
        };
    }

    public override async Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        // Validate
        if (!ValidateArguments(arguments, out var error))
            return new ToolResult { Success = false, Error = error };

        // Execute
        var path = arguments["documentPath"].ToString();
        var strategy = arguments.GetValueOrDefault("strategy", "semantic").ToString();
        var size = Convert.ToInt32(arguments.GetValueOrDefault("chunkSize", 512));

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        var chunks = await _chunker.ChunkAsync(text, strategy, size, cancellationToken);

        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new
            {
                chunks = chunks,
                count = chunks.Count
            }),
            Metadata = new Dictionary<string, object>
            {
                ["chunkCount"] = chunks.Count,
                ["strategy"] = strategy
            }
        };
    }
}
```

### Pattern 2: Composite Skill (Orchestrates Multiple Tools)

```csharp
// src/Core/AI/Hazina.AI.Skills/CompositeToolBase.cs
public abstract class CompositeToolBase : AgentTool
{
    protected readonly WorkflowEngine _workflowEngine;
    protected readonly IToolRegistry _toolRegistry;

    protected CompositeToolBase(WorkflowEngine workflowEngine, IToolRegistry toolRegistry)
    {
        _workflowEngine = workflowEngine;
        _toolRegistry = toolRegistry;
    }

    // Define workflow to execute
    protected abstract Workflow DefineWorkflow(Dictionary<string, object> arguments);

    public override async Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        var workflow = DefineWorkflow(arguments);
        var result = await _workflowEngine.ExecuteWorkflowAsync(workflow, arguments, cancellationToken);

        return new ToolResult
        {
            Success = result.Success,
            Output = JsonSerializer.Serialize(result.FinalContext),
            Error = result.Error
        };
    }
}
```

```csharp
// skills/rag/rag-indexer/RagIndexerSkill.cs
public class RagIndexerSkill : CompositeToolBase
{
    public RagIndexerSkill(WorkflowEngine engine, IToolRegistry registry)
        : base(engine, registry)
    {
        Name = "rag-indexer";
        Description = "Complete pipeline: chunk documents, generate embeddings, index to store";

        Parameters = new Dictionary<string, ToolParameter>
        {
            ["documentPath"] = new() { Type = "string", Required = true },
            ["collectionName"] = new() { Type = "string", Required = true },
            ["chunkStrategy"] = new() { Type = "string", DefaultValue = "semantic" },
            ["embeddingProvider"] = new() { Type = "string", DefaultValue = "openai" }
        };
    }

    protected override Workflow DefineWorkflow(Dictionary<string, object> arguments)
    {
        return new Workflow
        {
            Name = "RAG Indexing Pipeline",
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Name = "Chunk Document",
                    Type = StepType.AgentTask,
                    AgentName = "document-chunker",
                    Task = $"Chunk document at {arguments["documentPath"]} using {arguments["chunkStrategy"]} strategy",
                    OutputKey = "chunks"
                },
                new WorkflowStep
                {
                    Name = "Generate Embeddings",
                    Type = StepType.AgentTask,
                    AgentName = "embedding-generator",
                    Task = $"Generate embeddings for chunks using {arguments["embeddingProvider"]}",
                    OutputKey = "embeddings"
                },
                new WorkflowStep
                {
                    Name = "Index to Store",
                    Type = StepType.AgentTask,
                    AgentName = "vector-store-indexer",
                    Task = $"Index embeddings to collection {arguments["collectionName"]}",
                    OutputKey = "indexResult"
                }
            }
        };
    }
}
```

### Pattern 3: Skill Documentation (Markdown)

```markdown
# RAG Indexer Skill

**Name:** `rag-indexer`
**Category:** RAG / Indexing
**Version:** 1.0.0

## Description

Complete document indexing pipeline for RAG. Chunks documents, generates embeddings, and indexes to vector store.

## When to Use

Use this skill when you need to:
- Index documents for semantic search
- Prepare documents for RAG question answering
- Build a searchable knowledge base

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `documentPath` | string | Yes | - | Path to document file |
| `collectionName` | string | Yes | - | Name of vector store collection |
| `chunkStrategy` | string | No | "semantic" | Chunking strategy: fixed, hierarchical, semantic |
| `embeddingProvider` | string | No | "openai" | Embedding provider: openai, google, local |

## Returns

```json
{
  "chunks": 145,
  "embeddings": 145,
  "indexedDocs": 145,
  "collectionName": "my-docs"
}
```

## Example Usage

### From Agent
```
Execute rag-indexer skill to index the file at /docs/manual.pdf into the "product-docs" collection
```

### From Code
```csharp
var skill = toolRegistry.GetTool("rag-indexer");
var result = await skill.ExecuteAsync(new Dictionary<string, object>
{
    ["documentPath"] = "/docs/manual.pdf",
    ["collectionName"] = "product-docs",
    ["chunkStrategy"] = "semantic",
    ["embeddingProvider"] = "openai"
});
```

## Dependencies

- `document-chunker` tool
- `embedding-generator` tool
- `vector-store-indexer` tool
- EmbeddingStore configured
- DocumentStore configured

## Implementation

Uses WorkflowEngine to orchestrate:
1. Document chunking (via document-chunker tool)
2. Embedding generation (via embedding-generator tool)
3. Vector store indexing (via vector-store-indexer tool)

## Testing

See `tests/RagIndexerSkillTests.cs` for examples.

## Author

Hazina Team

## License

MIT
```

---

## Directory Structure for Skills

```
hazina/
├── src/Core/AI/Hazina.AI.Skills/
│   ├── CompositeToolBase.cs          # Base class for skills that orchestrate workflows
│   └── SkillRegistry.cs              # Helper for bulk skill registration
│
├── skills/                            # All skills live here
│   ├── rag/                          # Category: RAG
│   │   ├── document-chunker/
│   │   │   ├── SKILL.md              # Documentation (LLM reads this)
│   │   │   ├── DocumentChunkerTool.cs
│   │   │   ├── examples/
│   │   │   │   └── example.txt
│   │   │   └── tests/
│   │   │       └── DocumentChunkerTests.cs
│   │   │
│   │   ├── rag-indexer/
│   │   │   ├── SKILL.md
│   │   │   ├── RagIndexerSkill.cs
│   │   │   └── tests/
│   │   │
│   │   └── rag-qa/
│   │       ├── SKILL.md
│   │       ├── RagQaSkill.cs
│   │       └── tests/
│   │
│   ├── data/                         # Category: Data Processing
│   │   ├── csv-parser/
│   │   ├── json-transformer/
│   │   └── data-validator/
│   │
│   └── api/                          # Category: API Integration
│       ├── rest-caller/
│       └── webhook-receiver/
│
└── docs/skills/
    ├── README.md                      # Overview of skill system
    ├── AUTHORING_GUIDE.md             # How to create skills
    └── examples/                      # Example skill implementations
```

---

## How Agents Discover and Use Skills

### Agent Perspective

```
User: "Index the documents in /docs/research/ for RAG search"

Agent thinks:
1. I need to index documents for RAG
2. Let me check available tools... (reads from ToolRegistry via MCP)
3. Found: "rag-indexer" tool - "Complete pipeline: chunk, embed, index"
4. Perfect! Let me call it:

Agent: Execute rag-indexer tool with:
  - documentPath: /docs/research/
  - collectionName: research-docs
  - chunkStrategy: semantic

Result: {chunks: 523, indexed: 523, collection: "research-docs"}

Agent: "Successfully indexed 523 document chunks into the 'research-docs' collection"
```

### MCP Tool Discovery

The agent discovers skills the same way it discovers any tool:

```
MCP Client: tools/list

MCP Server Response:
{
  "tools": [
    {
      "name": "document-chunker",
      "description": "Chunk documents into semantic segments for RAG indexing",
      "inputSchema": {
        "type": "object",
        "properties": {
          "documentPath": {"type": "string"},
          "strategy": {"type": "string"},
          "chunkSize": {"type": "integer"}
        },
        "required": ["documentPath"]
      }
    },
    {
      "name": "rag-indexer",
      "description": "Complete pipeline: chunk documents, generate embeddings, index to store",
      "inputSchema": { ... }
    },
    ...
  ]
}
```

---

## Minimal Implementation Plan

### Week 1: Foundation
- [ ] Create `CompositeToolBase.cs`
- [ ] Create skill directory structure (`skills/`)
- [ ] Document skill authoring pattern (`docs/skills/AUTHORING_GUIDE.md`)

### Week 2-3: Example Skills
Create 5 reference skills:
- [ ] `document-chunker` (simple tool)
- [ ] `embedding-generator` (simple tool)
- [ ] `rag-indexer` (composite skill)
- [ ] `rag-qa` (composite skill)
- [ ] `knowledge-graph-builder` (composite skill)

Each with:
- [ ] Tool implementation
- [ ] SKILL.md documentation
- [ ] Unit tests
- [ ] Example usage

### Week 4: Registration & Discovery
- [ ] Create `SkillRegistry.cs` helper
- [ ] Auto-discover skills from `skills/` directory
- [ ] Register all skills in ToolRegistry
- [ ] Expose via MCP server
- [ ] Test agent can discover and call skills

### Week 5: Documentation
- [ ] Complete authoring guide
- [ ] Create video tutorial
- [ ] Document best practices
- [ ] Create skill template project

---

## What This Approach Gives You

### ✅ Advantages

1. **Zero New Formats**
   - Skills are just C# classes extending AgentTool
   - Documentation is markdown
   - No YAML/JSON schemas to learn

2. **LLM-Native Discovery**
   - Agents discover skills via MCP (existing protocol)
   - Read markdown docs to understand usage
   - No custom parsing logic needed

3. **Reuses Existing Infrastructure**
   - WorkflowEngine for orchestration ✅
   - ToolRegistry for discovery ✅
   - MCP for invocation ✅
   - AgentTool base class ✅

4. **Simple Mental Model**
   - "Skills are just tools that can call other tools"
   - Easy to explain, easy to implement

5. **Composability**
   - Skills can call other skills
   - Build complex workflows from simple tools
   - Natural dependency graph

6. **Standard .NET Development**
   - Skills are NuGet packages
   - Standard C# code
   - Standard testing frameworks
   - IDE support out of the box

### 🎯 What's Different from Current State

**Current:** Tools are individual, atomic operations
**With Skills:** Some tools orchestrate workflows of other tools

That's it. That's the whole change.

---

## Comparison to Original Plan

| Aspect | Original Plan | Revised Plan |
|--------|--------------|--------------|
| **Format** | Custom .hzskill YAML | C# classes + markdown |
| **Execution** | Separate SkillEngine | Reuse WorkflowEngine |
| **Discovery** | Custom registry | ToolRegistry + MCP |
| **Documentation** | JSON schema | Markdown files |
| **Packaging** | .hzskillpkg zip files | NuGet packages |
| **Invocation** | New API | Existing AgentTool API |
| **Lines of Code** | ~10,000 | ~500 |

---

## Example: Agent Using a Skill

```
User: "I need to set up RAG for my product documentation in /docs/"

Agent:
1. Checking available tools...
2. Found "rag-indexer" skill - perfect for this task
3. Executing: rag-indexer
   - documentPath: /docs/
   - collectionName: product-docs
   - chunkStrategy: semantic
   - embeddingProvider: openai

[rag-indexer executing workflow:]
  ✓ Chunking documents (document-chunker)
  ✓ Generating embeddings (embedding-generator)
  ✓ Indexing to store (vector-store-indexer)

Result: Indexed 347 chunks from 12 documents

Agent: "Successfully set up RAG for your product documentation.
       Indexed 347 chunks from 12 documents into the 'product-docs' collection.
       You can now ask questions about your documentation."
```

---

## Next Steps

1. **Create `CompositeToolBase.cs`** - Foundation class
2. **Create first skill** - `document-chunker` as example
3. **Test with agent** - Verify agent can discover and use it
4. **Create 4 more skills** - RAG workflow examples
5. **Document pattern** - Authoring guide for developers

**Estimated Time:** 2-3 weeks vs. 4-6 weeks for original plan

**Complexity:** ~500 LOC vs. ~10,000 LOC for original plan

---

## Conclusion

**Skills don't need a separate abstraction layer.**

They're just tools that happen to orchestrate other tools. By treating them as first-class MCP tools and using markdown for documentation, we get:

- Simpler implementation
- Better LLM integration
- Standard .NET patterns
- Reuse of existing infrastructure
- Faster time to market

The only new concept is `CompositeToolBase` - everything else already exists.

---

**Document Status:** Complete (Revised)
**Last Updated:** 2026-01-13
**Author:** Claude Code Analysis Agent
