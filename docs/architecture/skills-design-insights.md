# Hazina Skills - Design Insights and Decisions

**Date:** 2026-01-13
**Topic:** How to implement a "Skill System" for Hazina
**Status:** Design decision documented

---

## The Question

> "Should Hazina have a 'Skill' system that packages reusable workflows (chunking, embedding, search, etc.) into installable modules?"

Initial thinking: Create a custom `.hzskill` specification format, separate execution engine, packaging system, marketplace, etc.

**Result:** That approach would require ~10,000 lines of new code and 6+ weeks of work.

---

## The Key Insight

> **User:** "Why would I need a .hzskill file? Is a claude.md or similar not more useful? I want to use as generic options as possible. Should skills be a tool that can be called? For instance, the workspace agent says 'execute skill X to complete Y' and then it finds the appropriate skill just like it does with the tools already."

**This changed everything.**

---

## What We Learned

### 1. Skills ARE Tools (Not a Separate Abstraction)

**Wrong mental model:**
```
Tools (atomic operations)
  ↕
Orchestration Layer
  ↕
Skills (separate system with custom format)
  ↕
Agents (discover and execute skills via new API)
```

**Right mental model:**
```
Simple Tools (read file, call API, etc.)
  ↕
Composite Tools (orchestrate multiple simple tools)
  ↕
Agents (discover ALL tools via MCP, no distinction)
```

**Key realization:** A "skill" is just a tool that happens to orchestrate other tools. No separate abstraction needed.

### 2. Markdown > Custom Formats

**Wrong approach:**
- Define `.hzskill` YAML/JSON format
- Create schema validator
- Build parser
- LLM reads parsed metadata

**Right approach:**
- Use markdown (`SKILL.md`)
- LLMs read markdown directly (they're good at this!)
- Humans can read/write it easily
- No parsing needed

**Why this works:** LLMs are trained on markdown documentation. They already understand it. Custom formats add friction.

### 3. Reuse Existing Infrastructure

**What already exists in Hazina:**

| Component | Status | Use For |
|-----------|--------|---------|
| `AgentTool` base class | ✅ Exists | Base for all tools/skills |
| `ToolRegistry` | ✅ Exists | Register and discover tools |
| `MCP Client/Server` | ✅ Exists | Protocol for tool invocation |
| `WorkflowEngine` | ✅ Exists | Orchestrate multi-step workflows |
| `TaskOrchestrator` | ✅ Exists | Dependency resolution |
| Document/Embedding/Graph Stores | ✅ Exists | Data access |

**What's missing:** A convenient way for tools to orchestrate workflows.

**Solution:** `CompositeToolBase` - A base class that wraps `WorkflowEngine`.

That's it. One class. ~50 lines of code.

### 4. Agent Discovery via MCP (Already Works)

**How agents discover tools:**

```
Agent: "What tools are available?"
MCP Server: "Here's the list..." (calls ToolRegistry.GetAllTools())
Agent: "Great, I'll use document-chunker and rag-indexer"
```

**No changes needed.** Skills show up in the tool list automatically.

**No custom discovery API needed.**

### 5. Standard .NET Packaging (NuGet)

**Wrong approach:**
- Create `.hzskillpkg` format (custom ZIP structure)
- Build packaging/unpacking tools
- Create custom repository
- Build installation CLI

**Right approach:**
- Skills are C# classes
- Package as NuGet packages
- Install with `dotnet add package`
- Standard .NET tooling

**Why this works:** Don't reinvent package management. NuGet already solves this.

---

## The Revised Design

### Skill Anatomy

A skill consists of:

1. **Implementation** - C# class extending `AgentTool` or `CompositeToolBase`
2. **Documentation** - `SKILL.md` markdown file
3. **Tests** - Standard unit tests
4. **Examples** - Optional sample data/usage

### Example: Simple Skill

```csharp
public class DocumentChunkerTool : AgentTool
{
    public DocumentChunkerTool(ITextChunker chunker)
    {
        Name = "document-chunker";
        Description = "Chunk documents into semantic segments";
        // ... parameters ...
    }

    public override async Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### Example: Composite Skill (Orchestrates Other Tools)

```csharp
public class RagIndexerSkill : CompositeToolBase
{
    public RagIndexerSkill(WorkflowEngine engine, IServiceProvider services)
        : base(engine, services)
    {
        Name = "rag-indexer";
        Description = "Chunk, embed, and index documents";
    }

    protected override Workflow DefineWorkflow(Dictionary<string, object> args)
    {
        return new Workflow {
            Steps = [
                new WorkflowStep { /* call document-chunker */ },
                new WorkflowStep { /* call embedding-generator */ },
                new WorkflowStep { /* call vector-store-indexer */ }
            ]
        };
    }
}
```

### Example: Documentation (SKILL.md)

```markdown
# RAG Indexer Skill

**Name:** `rag-indexer`
**Type:** Composite

## Description
Complete document indexing pipeline for RAG.

## Parameters
- `documentPath` (string, required) - Path to document
- `collectionName` (string, required) - Vector store collection

## Returns
- `chunks` (integer) - Number of chunks created
- `indexed` (integer) - Number of chunks indexed

## Example Usage
Agent: "Index the files in /docs/ to the 'manual' collection"

## Dependencies
- document-chunker
- embedding-generator
- vector-store-indexer
```

---

## Key Architectural Decisions

### Decision 1: No Custom Skill Format
**Rationale:**
- Custom formats add complexity
- Markdown is LLM-native
- C# is type-safe and IDE-supported
- No schema validation needed

**Alternative considered:** YAML/JSON `.hzskill` format
**Rejected because:** Adds parsing, validation, and tooling overhead for no real benefit

### Decision 2: Skills ARE Tools
**Rationale:**
- Reuses existing infrastructure (ToolRegistry, MCP)
- Agents don't need to know the difference
- Simpler mental model
- No separate "skill execution engine"

**Alternative considered:** Separate skill abstraction layer
**Rejected because:** Adds complexity without adding functionality

### Decision 3: Composite Pattern for Orchestration
**Rationale:**
- `CompositeToolBase` wraps `WorkflowEngine`
- Makes it easy to create multi-step skills
- Hides orchestration complexity
- Reuses proven workflow engine

**Alternative considered:** Each skill implements its own orchestration
**Rejected because:** Code duplication, harder to maintain

### Decision 4: Standard .NET Packaging
**Rationale:**
- NuGet is proven and familiar
- Standard tooling (`dotnet add package`)
- Dependency management built-in
- No custom infrastructure needed

**Alternative considered:** Custom `.hzskillpkg` format
**Rejected because:** Reinventing package management

### Decision 5: Markdown Documentation
**Rationale:**
- LLMs are trained on markdown
- Humans can read/write easily
- No parsing needed
- Flexible and extensible

**Alternative considered:** Structured metadata in code attributes
**Rejected because:** Less readable, harder to write, not LLM-friendly

---

## What This Enables

### For Developers

```csharp
// Create a skill
public class MySkill : CompositeToolBase { ... }

// Register it
services.AddSingleton<MySkill>();
skillRegistry.RegisterSkill<MySkill>();

// Done! Agents can now discover and use it via MCP
```

### For Agents

```
User: "Index my product documentation for RAG"

Agent:
1. Check available tools (via MCP)
2. Found: rag-indexer - "Chunk, embed, and index documents"
3. Execute: rag-indexer(path="/docs/products/", collection="products")
4. Result: Indexed 347 chunks from 23 documents

Agent: "Done! Indexed 347 chunks. You can now ask questions about your documentation."
```

### For Users

- Skills are just NuGet packages
- Install with `dotnet add package Hazina.Skills.RAG`
- No special tooling needed
- Works with existing .NET ecosystem

---

## Implementation Scope

### What We're Building

1. **`CompositeToolBase.cs`** (~50 lines)
   - Base class for skills that orchestrate workflows
   - Wraps WorkflowEngine
   - Handles workflow execution

2. **5 Example RAG Skills** (~1,500 lines total)
   - document-chunker (simple)
   - embedding-generator (simple)
   - rag-indexer (composite)
   - rag-qa (composite)
   - knowledge-graph-builder (composite)

3. **Documentation** (~2,000 words)
   - Authoring guide
   - SKILL.md template
   - Tutorials
   - Examples

4. **Skill Registry Helper** (~100 lines)
   - Auto-discover skills in assemblies
   - Bulk registration
   - Optional (convenience only)

**Total new code:** ~2,000 lines
**Total new infrastructure:** Minimal (one base class)

### What We're NOT Building

- ❌ Custom skill format (use C# + markdown)
- ❌ Separate execution engine (use WorkflowEngine)
- ❌ Custom packaging (use NuGet)
- ❌ Custom discovery (use MCP)
- ❌ Skill marketplace (maybe later)
- ❌ Visual designer (maybe later)
- ❌ Skill generator (maybe later)

**Rationale:** Start simple. Add complexity only when proven necessary.

---

## Comparison: Before vs. After

| Aspect | Original Design | Revised Design |
|--------|----------------|----------------|
| **Skill Format** | Custom `.hzskill` YAML | C# classes + SKILL.md |
| **Execution** | Custom SkillEngine | Reuse WorkflowEngine |
| **Discovery** | Custom registry + API | ToolRegistry + MCP |
| **Packaging** | `.hzskillpkg` ZIP files | NuGet packages |
| **Documentation** | JSON schema | Markdown |
| **Lines of Code** | ~10,000 | ~2,000 |
| **Implementation Time** | 6+ weeks | 3 weeks |
| **Complexity** | High | Low |
| **Learning Curve** | New concepts | Familiar .NET patterns |

---

## The "Aha!" Moments

### 1. "Skills don't need a separate abstraction"
When the user asked "should skills be a tool that can be called?" it became clear that skills are just specialized tools. Creating a separate "skill" concept adds unnecessary complexity.

### 2. "Markdown is better than custom formats"
LLMs are already trained on markdown documentation. Why create a custom format they'd need to parse? Markdown is human-friendly AND LLM-friendly.

### 3. "Reuse what exists"
Hazina already has:
- Tool infrastructure (AgentTool, ToolRegistry)
- Orchestration (WorkflowEngine)
- Discovery (MCP)
- Packaging (.NET/NuGet)

Building parallel systems for skills would be wasteful.

### 4. "Composition over custom abstractions"
Instead of building a complex "skill system," just make it easy to compose tools. `CompositeToolBase` is the minimal abstraction needed.

### 5. "Start simple, add complexity when needed"
Don't build a marketplace, visual designer, and skill generator on day one. Start with the core pattern and see what people actually need.

---

## Design Principles That Emerged

1. **Leverage Existing Patterns**
   - Don't create new abstractions when existing ones work
   - Reuse infrastructure rather than building parallel systems

2. **LLM-Native Design**
   - Use formats LLMs already understand (markdown)
   - Make discovery natural (MCP protocol)
   - Document for both humans and AI

3. **Standard Over Custom**
   - Standard .NET packaging (NuGet)
   - Standard documentation (markdown)
   - Standard patterns (C# classes)

4. **Composition Over Complexity**
   - Simple tools compose into complex workflows
   - One small base class enables orchestration
   - Minimal new concepts

5. **Progressive Enhancement**
   - Start with core functionality
   - Add features based on real usage
   - Don't overengineer upfront

---

## Future Considerations

These features were discussed but deferred:

### Maybe Later (If Needed)

1. **Visual Designer**
   - Drag-and-drop skill composition
   - Only if users request it
   - Current approach (code-based) may be sufficient

2. **Skill Marketplace**
   - Centralized skill repository
   - Only if community grows
   - GitHub repos may be sufficient initially

3. **LLM-Powered Skill Generator**
   - "Generate a skill that does X"
   - Cool but not essential
   - Manual creation is fine for now

4. **Skill Templates CLI**
   - `hazina skill new <name>`
   - Nice convenience
   - Copy-paste from examples works too

**Decision:** Don't build these until there's proven demand. Simple is better.

---

## Lessons for Future Design Decisions

### What Worked

1. **Questioning assumptions**
   - "Do we really need a custom format?"
   - "Can we reuse what exists?"
   - "What's the simplest thing that could work?"

2. **User-driven simplification**
   - User's question revealed simpler path
   - Deep expertise in MCP/tools led to insight
   - Sometimes the answer is "don't build it"

3. **Prototype thinking**
   - What's the minimal viable implementation?
   - Can we prove the concept with 50 lines of code?
   - Start small, grow as needed

### What to Watch For

1. **Over-engineering signals**
   - "We need a custom format for X"
   - "We should build a separate system for Y"
   - "Let's create a new abstraction for Z"

2. **Complexity creep**
   - Adding features "just in case"
   - Building infrastructure before it's needed
   - Solving problems that don't exist yet

3. **Not-invented-here syndrome**
   - Ignoring standard solutions (NuGet, markdown)
   - Reinventing existing patterns
   - Creating parallel systems

---

## Implementation Status

### Completed
- ✅ Architecture analysis (original approach)
- ✅ Design revision (skills-as-tools)
- ✅ Documentation of insights (this document)

### Next Steps (If/When Implemented)
1. Create `CompositeToolBase.cs`
2. Implement 5 example RAG skills
3. Write authoring guide
4. Test with agents
5. Gather feedback

### Open Questions
- Should skills have versioning beyond NuGet versioning?
- Should there be a skill "certification" process?
- How to handle skill conflicts (two skills with same name)?
- Should skills be able to declare store/LLM requirements?

**Decision:** Defer until we have real usage data.

---

## Summary

### The Original Question
"Should Hazina have a skill system with custom formats, execution engine, and marketplace?"

### The Answer
"No. Skills should just be tools that can orchestrate other tools. Use markdown for docs, C# for implementation, NuGet for packaging, and MCP for discovery."

### Why This Works
1. Reuses 90% of existing infrastructure
2. Familiar .NET patterns
3. LLM-friendly (markdown docs)
4. Simple to implement and maintain
5. No vendor lock-in
6. Grows naturally with the ecosystem

### The Core Insight
**Don't build a "skill system." Just make it easy to create tools that orchestrate workflows.**

---

**Document Type:** Design Decision Record
**Decision:** Skills are tools (not separate abstraction)
**Status:** Approved
**Supersedes:** Original skill system design (docs/roadmap/hazina-skills-roadmap.md)
**Implementation Priority:** Medium (not blocking current work)
**Estimated Effort:** 3 weeks when needed
