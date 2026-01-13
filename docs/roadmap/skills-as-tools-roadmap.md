# Skills as Tools - Simplified Implementation Roadmap

**Generated:** 2026-01-13 (Revised)
**Approach:** Skills are MCP tools, not a separate abstraction layer

---

## Core Concept

**Skills ARE tools.** They just happen to orchestrate other tools/workflows.

**No custom formats. No separate engine. Just extend `AgentTool` and use markdown docs.**

---

## 3-Week Implementation Plan

### Week 1: Foundation

#### Day 1-2: Create CompositeToolBase

**File:** `src/Core/AI/Hazina.AI.Skills/CompositeToolBase.cs`

```csharp
public abstract class CompositeToolBase : AgentTool
{
    protected readonly WorkflowEngine _workflowEngine;
    protected readonly IServiceProvider _services;

    protected CompositeToolBase(
        WorkflowEngine workflowEngine,
        IServiceProvider services)
    {
        _workflowEngine = workflowEngine;
        _services = services;
    }

    protected abstract Workflow DefineWorkflow(Dictionary<string, object> arguments);

    public override async Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateArguments(arguments, out var error))
            return new ToolResult { Success = false, Error = error };

        var workflow = DefineWorkflow(arguments);
        var result = await _workflowEngine.ExecuteWorkflowAsync(
            workflow,
            arguments,
            cancellationToken);

        return new ToolResult
        {
            Success = result.Success,
            Output = JsonSerializer.Serialize(result.FinalContext),
            Error = result.Error,
            Metadata = new Dictionary<string, object>
            {
                ["duration"] = result.Duration.TotalMilliseconds,
                ["steps"] = result.StepResults.Count
            }
        };
    }
}
```

**Testing:**
- Create simple test skill that calls 2 tools
- Verify workflow execution
- Verify error handling

#### Day 3: Create Skill Directory Structure

```
hazina/
├── skills/                           # NEW
│   ├── README.md                     # Overview of available skills
│   ├── rag/                          # Category
│   │   └── .gitkeep
│   ├── data/
│   │   └── .gitkeep
│   └── api/
│       └── .gitkeep
│
└── docs/skills/                      # NEW
    ├── README.md                     # Skill system overview
    ├── AUTHORING_GUIDE.md            # How to create skills
    └── SKILL_TEMPLATE.md             # Template for SKILL.md
```

**Documents to create:**
- `skills/README.md` - List of all available skills
- `docs/skills/README.md` - What skills are, how they work
- `docs/skills/AUTHORING_GUIDE.md` - Step-by-step guide
- `docs/skills/SKILL_TEMPLATE.md` - Copy-paste template

#### Day 4-5: Documentation

**AUTHORING_GUIDE.md Contents:**
1. What are skills?
2. When to create a skill vs. a tool
3. How to extend CompositeToolBase
4. How to define workflows
5. How to write SKILL.md
6. How to register skill
7. How to test skill
8. Examples

---

### Week 2: Example Skills (RAG)

#### Skill 1: Document Chunker (Simple Tool)

**Day 1:**

**File:** `skills/rag/document-chunker/DocumentChunkerTool.cs`

```csharp
namespace Hazina.Skills.RAG;

public class DocumentChunkerTool : AgentTool
{
    private readonly ITextChunker _chunker;
    private readonly ILogger<DocumentChunkerTool> _logger;

    public DocumentChunkerTool(
        ITextChunker chunker,
        ILogger<DocumentChunkerTool> logger)
    {
        _chunker = chunker;
        _logger = logger;

        Name = "document-chunker";
        Description = "Chunk documents into semantic segments for RAG indexing";

        Parameters = new Dictionary<string, ToolParameter>
        {
            ["documentPath"] = new()
            {
                Type = "string",
                Description = "Path to document file",
                Required = true
            },
            ["strategy"] = new()
            {
                Type = "string",
                Description = "Chunking strategy: fixed, hierarchical, semantic",
                DefaultValue = "semantic"
            },
            ["chunkSize"] = new()
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
        if (!ValidateArguments(arguments, out var error))
            return new ToolResult { Success = false, Error = error };

        try
        {
            var path = arguments["documentPath"].ToString()!;
            var strategy = arguments.GetValueOrDefault("strategy", "semantic").ToString()!;
            var size = Convert.ToInt32(arguments.GetValueOrDefault("chunkSize", 512));

            _logger.LogInformation(
                "Chunking document {Path} with {Strategy} strategy",
                path, strategy);

            var text = await File.ReadAllTextAsync(path, cancellationToken);
            var chunks = await _chunker.ChunkAsync(text, strategy, size, cancellationToken);

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(new
                {
                    chunks = chunks.Select(c => new
                    {
                        text = c.Text,
                        metadata = c.Metadata
                    }),
                    count = chunks.Count
                }),
                Metadata = new Dictionary<string, object>
                {
                    ["chunkCount"] = chunks.Count,
                    ["strategy"] = strategy,
                    ["avgChunkSize"] = chunks.Average(c => c.Text.Length)
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error chunking document");
            return new ToolResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}
```

**File:** `skills/rag/document-chunker/SKILL.md` (see template)

**File:** `skills/rag/document-chunker/tests/DocumentChunkerTests.cs`

**Registration:** Add to DI container in startup

#### Skill 2: Embedding Generator (Simple Tool)

**Day 2:**

Similar pattern to document-chunker.

**Files:**
- `skills/rag/embedding-generator/EmbeddingGeneratorTool.cs`
- `skills/rag/embedding-generator/SKILL.md`
- `skills/rag/embedding-generator/tests/`

#### Skill 3: RAG Indexer (Composite Skill)

**Day 3-4:**

**File:** `skills/rag/rag-indexer/RagIndexerSkill.cs`

```csharp
namespace Hazina.Skills.RAG;

public class RagIndexerSkill : CompositeToolBase
{
    public RagIndexerSkill(
        WorkflowEngine workflowEngine,
        IServiceProvider services)
        : base(workflowEngine, services)
    {
        Name = "rag-indexer";
        Description = "Complete pipeline: chunk documents, generate embeddings, index to vector store";

        Parameters = new Dictionary<string, ToolParameter>
        {
            ["documentPath"] = new()
            {
                Type = "string",
                Description = "Path to document or directory",
                Required = true
            },
            ["collectionName"] = new()
            {
                Type = "string",
                Description = "Vector store collection name",
                Required = true
            },
            ["chunkStrategy"] = new()
            {
                Type = "string",
                Description = "Chunking strategy",
                DefaultValue = "semantic"
            },
            ["embeddingProvider"] = new()
            {
                Type = "string",
                Description = "Embedding provider: openai, google, local",
                DefaultValue = "openai"
            }
        };
    }

    protected override Workflow DefineWorkflow(Dictionary<string, object> arguments)
    {
        return new Workflow
        {
            Name = "RAG Indexing Pipeline",
            Description = "Chunk, embed, and index documents",
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Name = "chunk_documents",
                    Type = StepType.AgentTask,
                    AgentName = "document-chunker",
                    Task = BuildToolCall("document-chunker", new
                    {
                        documentPath = arguments["documentPath"],
                        strategy = arguments.GetValueOrDefault("chunkStrategy", "semantic")
                    }),
                    OutputKey = "chunks",
                    ContinueOnFailure = false
                },
                new WorkflowStep
                {
                    Name = "generate_embeddings",
                    Type = StepType.AgentTask,
                    AgentName = "embedding-generator",
                    Task = BuildToolCall("embedding-generator", new
                    {
                        chunks = "{chunks}",
                        provider = arguments.GetValueOrDefault("embeddingProvider", "openai")
                    }),
                    OutputKey = "embeddings",
                    ContinueOnFailure = false
                },
                new WorkflowStep
                {
                    Name = "index_to_store",
                    Type = StepType.AgentTask,
                    AgentName = "vector-store-indexer",
                    Task = BuildToolCall("vector-store-indexer", new
                    {
                        embeddings = "{embeddings}",
                        collectionName = arguments["collectionName"]
                    }),
                    OutputKey = "indexResult",
                    ContinueOnFailure = false
                }
            }
        };
    }

    private string BuildToolCall(string toolName, object parameters)
    {
        return JsonSerializer.Serialize(new
        {
            tool = toolName,
            parameters = parameters
        });
    }
}
```

**Files:**
- `skills/rag/rag-indexer/RagIndexerSkill.cs`
- `skills/rag/rag-indexer/SKILL.md`
- `skills/rag/rag-indexer/tests/`
- `skills/rag/rag-indexer/examples/` (sample data)

#### Skill 4: RAG Q&A (Composite Skill)

**Day 4-5:**

**Workflow:**
1. Retrieve relevant chunks (vector search)
2. Rerank chunks (if configured)
3. Generate answer with LLM
4. Return answer + sources

**Files:**
- `skills/rag/rag-qa/RagQaSkill.cs`
- `skills/rag/rag-qa/SKILL.md`
- `skills/rag/rag-qa/tests/`

#### Skill 5: Knowledge Graph Builder (Composite Skill)

**Day 5:**

**Workflow:**
1. Chunk documents
2. Extract entities (LLM)
3. Extract relationships (LLM)
4. Build graph (GraphConstructionPipeline)

**Files:**
- `skills/rag/knowledge-graph-builder/KnowledgeGraphBuilderSkill.cs`
- `skills/rag/knowledge-graph-builder/SKILL.md`
- `skills/rag/knowledge-graph-builder/tests/`

---

### Week 3: Integration & Polish

#### Day 1-2: Tool Registration

**Create:** `src/Core/AI/Hazina.AI.Skills/SkillRegistry.cs`

```csharp
public class SkillRegistry
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IServiceProvider _services;
    private readonly ILogger<SkillRegistry> _logger;

    public SkillRegistry(
        IToolRegistry toolRegistry,
        IServiceProvider services,
        ILogger<SkillRegistry> logger)
    {
        _toolRegistry = toolRegistry;
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Auto-discover and register all skills from assemblies
    /// </summary>
    public void RegisterAllSkills()
    {
        var skillTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(AgentTool)))
            .Where(t => t.Namespace?.StartsWith("Hazina.Skills") ?? false);

        foreach (var skillType in skillTypes)
        {
            try
            {
                var skill = (AgentTool)ActivatorUtilities.CreateInstance(_services, skillType);
                _toolRegistry.RegisterTool(skill);
                _logger.LogInformation("Registered skill: {SkillName}", skill.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register skill: {SkillType}", skillType.Name);
            }
        }
    }

    /// <summary>
    /// Register specific skill
    /// </summary>
    public void RegisterSkill<T>() where T : AgentTool
    {
        var skill = ActivatorUtilities.CreateInstance<T>(_services);
        _toolRegistry.RegisterTool(skill);
        _logger.LogInformation("Registered skill: {SkillName}", skill.Name);
    }
}
```

**Update Startup:**

```csharp
// In ConfigureServices
services.AddSingleton<SkillRegistry>();

// In Configure or Startup
var skillRegistry = app.Services.GetRequiredService<SkillRegistry>();
skillRegistry.RegisterAllSkills();
```

#### Day 3: MCP Integration Testing

**Test agent discovery:**

```bash
# Start MCP server
dotnet run --project Hazina.App.McpServer

# Test tool listing
curl http://localhost:5000/mcp/tools/list

# Expected output includes:
# - document-chunker
# - embedding-generator
# - rag-indexer
# - rag-qa
# - knowledge-graph-builder
```

**Test agent execution:**

```
User: "Index the documents in /docs/manual/ for RAG search"

Agent should:
1. Discover "rag-indexer" tool
2. Call it with appropriate parameters
3. Return success message
```

#### Day 4: Documentation

**Create tutorial:** `docs/skills/tutorials/creating-your-first-skill.md`

**Contents:**
1. Introduction to skills
2. When to create a skill
3. Step-by-step: Building a simple skill
4. Step-by-step: Building a composite skill
5. Testing your skill
6. Registering your skill
7. Documenting your skill (SKILL.md)
8. Publishing your skill (NuGet)

**Create:** `docs/skills/tutorials/skill-composition.md`

How to compose complex workflows from simple skills.

#### Day 5: Examples & Demo

**Create demo app:** `apps/Demos/Hazina.Demo.Skills/`

```csharp
// Program.cs
var services = new ServiceCollection();

// Add Hazina services
services.AddHazina();
services.AddWorkflowEngine();
services.AddToolRegistry();
services.AddSkillRegistry();

// Register skills
var provider = services.BuildServiceProvider();
var skillRegistry = provider.GetRequiredService<SkillRegistry>();
skillRegistry.RegisterAllSkills();

// List available skills
var toolRegistry = provider.GetRequiredService<IToolRegistry>();
var skills = toolRegistry.GetAllTools()
    .Where(t => t.GetType().Namespace?.StartsWith("Hazina.Skills") ?? false);

Console.WriteLine("Available Skills:");
foreach (var skill in skills)
{
    Console.WriteLine($"- {skill.Name}: {skill.Description}");
}

// Execute a skill
var ragIndexer = toolRegistry.GetTool("rag-indexer");
var result = await ragIndexer.ExecuteAsync(new Dictionary<string, object>
{
    ["documentPath"] = "/docs/sample/",
    ["collectionName"] = "demo-docs",
    ["chunkStrategy"] = "semantic",
    ["embeddingProvider"] = "openai"
});

Console.WriteLine($"Success: {result.Success}");
Console.WriteLine($"Output: {result.Output}");
```

**Update main README.md with skills section:**

```markdown
## Skills

Hazina includes a growing library of reusable skills for common AI tasks:

### RAG Skills
- **document-chunker** - Chunk documents into semantic segments
- **embedding-generator** - Generate embeddings for text
- **rag-indexer** - Complete indexing pipeline (chunk + embed + index)
- **rag-qa** - Question answering with retrieval
- **knowledge-graph-builder** - Extract entities and build knowledge graph

### Creating Custom Skills

See [Authoring Guide](docs/skills/AUTHORING_GUIDE.md) for how to create your own skills.

Skills are just MCP tools that can orchestrate workflows. They integrate seamlessly
with agents, can be called from code, and are discoverable via the MCP protocol.
```

---

## Deliverables Checklist

### Week 1
- [ ] `CompositeToolBase.cs` created and tested
- [ ] Skill directory structure created
- [ ] README.md in skills/
- [ ] AUTHORING_GUIDE.md complete
- [ ] SKILL_TEMPLATE.md created

### Week 2
- [ ] 5 RAG skills implemented:
  - [ ] document-chunker (simple)
  - [ ] embedding-generator (simple)
  - [ ] rag-indexer (composite)
  - [ ] rag-qa (composite)
  - [ ] knowledge-graph-builder (composite)
- [ ] Each skill has SKILL.md
- [ ] Each skill has tests
- [ ] All skills pass tests

### Week 3
- [ ] SkillRegistry.cs created
- [ ] All skills auto-register on startup
- [ ] MCP integration tested
- [ ] Agent can discover and use skills
- [ ] Tutorial: Creating Your First Skill
- [ ] Tutorial: Skill Composition
- [ ] Demo app created
- [ ] Main README updated

---

## File Manifest

### New Files Created

```
src/Core/AI/Hazina.AI.Skills/
├── CompositeToolBase.cs                       # Base class for composite skills
└── SkillRegistry.cs                           # Auto-discovery and registration

skills/
├── README.md                                  # Overview of available skills
├── rag/
│   ├── document-chunker/
│   │   ├── SKILL.md
│   │   ├── DocumentChunkerTool.cs
│   │   ├── examples/sample.txt
│   │   └── tests/DocumentChunkerTests.cs
│   ├── embedding-generator/
│   │   ├── SKILL.md
│   │   ├── EmbeddingGeneratorTool.cs
│   │   └── tests/EmbeddingGeneratorTests.cs
│   ├── rag-indexer/
│   │   ├── SKILL.md
│   │   ├── RagIndexerSkill.cs
│   │   ├── examples/
│   │   └── tests/RagIndexerTests.cs
│   ├── rag-qa/
│   │   ├── SKILL.md
│   │   ├── RagQaSkill.cs
│   │   └── tests/RagQaTests.cs
│   └── knowledge-graph-builder/
│       ├── SKILL.md
│       ├── KnowledgeGraphBuilderSkill.cs
│       └── tests/KnowledgeGraphBuilderTests.cs

docs/skills/
├── README.md                                  # Skill system overview
├── AUTHORING_GUIDE.md                         # How to create skills
├── SKILL_TEMPLATE.md                          # Template for SKILL.md
└── tutorials/
    ├── creating-your-first-skill.md           # Step-by-step tutorial
    └── skill-composition.md                   # Composing complex workflows

apps/Demos/Hazina.Demo.Skills/
├── Program.cs                                 # Demo application
└── README.md                                  # How to run demo
```

**Total New Files:** ~25
**Total New Code:** ~2,000 lines (vs. 10,000 in original plan)

---

## Success Criteria

### Functional Requirements
- [ ] Agent can discover skills via MCP protocol
- [ ] Agent can execute simple skills (document-chunker)
- [ ] Agent can execute composite skills (rag-indexer)
- [ ] Skills can call other skills
- [ ] Skills report progress and errors clearly
- [ ] Skills integrate with existing stores (Document, Embedding, Graph)

### Quality Requirements
- [ ] All skills have unit tests
- [ ] Test coverage > 80%
- [ ] All skills have SKILL.md documentation
- [ ] Documentation is clear and has examples
- [ ] Code follows Hazina conventions

### Performance Requirements
- [ ] Skill discovery < 100ms
- [ ] Skill execution overhead < 5% vs. direct WorkflowEngine
- [ ] Large workflows (10+ steps) complete successfully

---

## What We're NOT Building

This approach explicitly avoids:

- ❌ Custom .hzskill format
- ❌ Separate skill execution engine
- ❌ Custom packaging format (.hzskillpkg)
- ❌ Skill marketplace infrastructure
- ❌ Visual workflow designer (future consideration)
- ❌ Skill versioning beyond NuGet versioning
- ❌ Skill dependency resolution beyond .NET dependencies

**Why?** All of these add complexity without adding value. Skills are tools. Tools are packages. Packages are NuGet. It's that simple.

---

## Future Considerations (Post Week 3)

### Optional Enhancements (if needed)

1. **Visual Designer Integration**
   - Add skills to Hazina.App.Windows as workflow nodes
   - Drag-and-drop skill composition
   - Visual debugging

2. **Skill Templates** (CLI)
   - `hazina skill new <skill-name>` command
   - Generate boilerplate code
   - Interactive prompts for configuration

3. **Skill Discovery UI**
   - Web interface to browse available skills
   - Search and filter
   - View documentation

4. **Community Skills**
   - Create GitHub repo for community skills
   - Pull request process for adding skills
   - Quality guidelines

5. **Skill Analytics**
   - Track skill usage
   - Performance metrics
   - Error rates

**Decision Point:** Implement these only if there's demonstrated demand.

---

## Getting Started (Right Now)

### Today

1. Create branch: `feature/skills-as-tools`
2. Create `src/Core/AI/Hazina.AI.Skills/` project
3. Implement `CompositeToolBase.cs`
4. Write tests for CompositeToolBase
5. Create `skills/` directory structure

### This Week

1. Implement first simple skill: `document-chunker`
2. Test with agent
3. Verify MCP discovery works
4. Write AUTHORING_GUIDE.md draft

### Next Week

1. Implement remaining 4 RAG skills
2. Test all skills
3. Create demo app
4. Finalize documentation

---

## Questions?

See `docs/skills/AUTHORING_GUIDE.md` for detailed implementation guidance.

For architectural questions, see `docs/architecture/hazina-skill-system-revised.md`.

---

**Document Status:** Complete
**Estimated Effort:** 3 weeks (1 developer)
**Complexity:** Low (reuses 90% of existing infrastructure)
**Risk:** Low (no breaking changes to existing code)
