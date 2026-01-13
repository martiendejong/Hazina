# AI Build Orchestrator - Implementation Plan

**Parent Document:** [README.md](./README.md)
**Status:** Planning
**Created:** 2026-01-13

---

## Overview

The AI Build Orchestrator enables **Claude Code** (or similar AI agents) to autonomously build Hazina applications from natural language descriptions or assembly specifications. This is the **unique differentiator** that no other framework offers.

### Key Insight

You already have a fully functional AI build agent (Claude Code). The declarative system simply provides:

1. **Representation Model** - Specs and component catalog
2. **Template System** - Code patterns to apply
3. **Validation Layer** - Verify generated code works

The AI handles the intelligence; we provide the structured knowledge.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                    User Natural Language Request                     │
│               "Build a RAG app for my company docs"                  │
└─────────────────────────────────────────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      Claude Code (AI Agent)                          │
│                                                                       │
│  ┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐        │
│  │ Understand │──►│ Plan     │──►│ Generate │──►│ Validate  │        │
│  │ Request   │  │ Approach  │  │ Code     │  │ & Fix    │        │
│  └───────────┘  └───────────┘  └───────────┘  └───────────┘        │
│        │              │              │              │               │
│        ▼              ▼              ▼              ▼               │
└────────┼──────────────┼──────────────┼──────────────┼───────────────┘
         │              │              │              │
         ▼              ▼              ▼              ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│  Component   │ │  Assembly    │ │   Code       │ │   Build      │
│  Registry    │ │  Spec Format │ │   Templates  │ │   System     │
│              │ │              │ │              │ │              │
│  (knowledge) │ │  (structure) │ │  (patterns)  │ │  (verify)    │
└──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘
```

---

## Claude Code Integration Points

### 1. Control Plane Instructions

The AI Orchestrator is primarily implemented as **instructions** in the Claude Code control plane.

**Location:** `C:\scripts\agents\hazina-builder.agent.md`

```markdown
# Hazina Builder Agent

## Purpose
Build Hazina RAG applications from natural language descriptions or assembly specifications.

## Available Resources
- Component Registry: `C:\Projects\hazina\src\Core\AI\Hazina.AI.Assembly\Components\`
- Assembly Schema: `C:\Projects\hazina\docs\declarative-assembly\02-ASSEMBLY_SPECIFICATION.md`
- Code Templates: `C:\Projects\hazina\templates\`
- Scaffold CLI: `hazina assemble <spec>`

## Workflow

### Step 1: Requirement Analysis
- Parse user request
- Identify required components (LLM, embedding, storage, modules)
- Determine if simple (use template) or complex (custom spec)

### Step 2: Spec Generation
IF user provides natural language:
  - Generate assembly specification YAML
  - Validate against schema
  - Present to user for review

IF user provides spec file:
  - Validate specification
  - Resolve variables
  - Check component availability

### Step 3: Project Generation
OPTION A: Use scaffold CLI
  - Run: `hazina assemble <spec> --output <path>`
  - Verify output

OPTION B: Manual generation
  - Read templates from templates/
  - Apply substitutions from spec
  - Write files to output directory

### Step 4: Validation
- Run: `dotnet build`
- If errors: analyze and fix
- Run: `dotnet test` (if tests exist)
- Verify endpoints work

### Step 5: Documentation
- Generate README.md
- Create usage examples
- Document environment variables

## Decision Rules

### When to use templates vs custom:
- User asks for "simple RAG" → Use hazina-rag-minimal template
- User asks for "production" → Use hazina-rag template
- User specifies multiple providers → Generate custom spec
- User needs specific modules → Generate custom spec

### Provider selection:
- Default LLM: OpenAI GPT-4o
- Default embedding: OpenAI text-embedding-3-small
- Default storage: In-memory (dev), Supabase (prod)

### Error handling:
- Build fails → Read error, identify fix, apply, rebuild
- Missing package → Add to .csproj, restore
- Config error → Fix appsettings.json
- Max retries: 3 per error type

## Component Quick Reference

### LLM Providers
| ID | Use Case |
|----|----------|
| llm.openai | Best quality, cloud |
| llm.anthropic | Claude models, cloud |
| llm.ollama | Local, privacy-sensitive |

### Embedding Providers
| ID | Dimensions | Use Case |
|----|------------|----------|
| embedding.openai | 1536/3072 | Cloud, high quality |
| embedding.local | varies | Privacy, no network |

### Vector Stores
| ID | Use Case |
|----|----------|
| vector.memory | Development, testing |
| vector.supabase | Cloud production |
| vector.pgvector | Self-hosted production |

### Modules
| ID | Endpoints |
|----|-----------|
| module.rag-query | POST /api/query |
| module.document-ingest | POST /api/ingest |
| module.search | GET/POST /api/search |
| module.health | GET /health |

## Examples

### Example 1: Simple RAG
User: "Create a simple RAG app for testing"
Action: Use hazina-rag-minimal template with memory store

### Example 2: Production RAG
User: "Build a production RAG for my company with OpenAI and Supabase"
Action: Generate spec with openai + supabase + full modules

### Example 3: Multi-provider
User: "RAG with OpenAI primary, Claude fallback, pgvector storage"
Action: Generate custom spec with failover chain
```

### 2. Skill Definition

**Location:** `C:\scripts\.claude\skills\hazina-build\SKILL.md`

```markdown
# Hazina Build Skill

**Activation:** When user asks to build, create, or scaffold a Hazina/RAG application

## Steps

1. **Analyze request**
   - Extract: LLM provider, storage choice, required modules
   - Identify: production vs development, features needed

2. **Generate or validate spec**
   ```bash
   # If generating from natural language
   # Create YAML at: ./hazina-app.assembly.yaml
   ```

3. **Run scaffold**
   ```bash
   hazina assemble hazina-app.assembly.yaml --output ./MyRagApp
   ```

4. **Verify build**
   ```bash
   cd MyRagApp
   dotnet restore
   dotnet build
   ```

5. **Fix errors** (if any)
   - Read build output
   - Identify issue
   - Apply fix
   - Rebuild

6. **Report success**
   - Show generated structure
   - List environment variables needed
   - Provide next steps

## Auto-Fix Patterns

### Missing NuGet package
```
Error: "The type or namespace 'Hazina' could not be found"
Fix: Add missing package reference to .csproj
```

### Invalid configuration
```
Error: "Configuration section 'Hazina:OpenAI' not found"
Fix: Add section to appsettings.json
```

### Null reference in config
```
Error: "ApiKey cannot be null"
Fix: Ensure environment variable is set or add to config
```
```

### 3. Natural Language to Spec Conversion

The AI can generate assembly specifications from natural language descriptions.

**Prompt Template:**

```markdown
## Spec Generation Prompt

You are generating a Hazina assembly specification from user requirements.

### User Request:
{user_request}

### Available Components:
{component_catalog}

### Output Format:
Generate a valid YAML assembly specification following this schema:
{assembly_schema}

### Guidelines:
1. Choose appropriate providers based on user's stated needs
2. Default to simpler options unless user specifies otherwise
3. Include health check module by default
4. Add Docker if user mentions "production" or "deploy"
5. Use environment variable references for secrets
6. Add comments explaining non-obvious choices

### Generate the specification:
```

**Example Conversion:**

User Input:
> "I need a RAG application for internal company documents. It should use Azure OpenAI since we have enterprise agreement, store vectors in our existing Supabase, and support both querying and document upload. Make it production-ready."

Generated Spec:
```yaml
version: "1.0"

metadata:
  name: company-docs-rag
  version: 1.0.0
  description: Internal document RAG for company knowledge base

variables:
  # Azure OpenAI configuration
  azure_endpoint: ${AZURE_OPENAI_ENDPOINT}
  azure_key: ${AZURE_OPENAI_KEY}
  azure_deployment: ${AZURE_OPENAI_DEPLOYMENT:-gpt-4o}

  # Supabase configuration
  supabase_url: ${SUPABASE_URL}
  supabase_key: ${SUPABASE_SERVICE_KEY}

providers:
  llm:
    primary:
      type: llm.azureopenai  # Using Azure OpenAI per enterprise agreement
      config:
        endpoint: ${azure_endpoint}
        apiKey: ${azure_key}
        deploymentName: ${azure_deployment}

  embedding:
    type: embedding.azureopenai
    config:
      endpoint: ${azure_endpoint}
      apiKey: ${azure_key}
      deploymentName: text-embedding-ada-002  # Common enterprise deployment

  storage:
    vectors:
      type: vector.supabase  # Existing Supabase instance
      config:
        url: ${supabase_url}
        key: ${supabase_key}
        table: document_embeddings

    documents:
      type: storage.local
      config:
        rootPath: ./data/documents

pipelines:
  ingestion:
    - stage: parse
      steps:
        - type: parse.auto
          config:
            supportedTypes: [pdf, docx, txt, md]
    - stage: chunk
      steps:
        - type: chunk.semantic
          config:
            targetSize: 512
    - stage: embed
      steps:
        - type: pipeline.embed
    - stage: index
      steps:
        - type: pipeline.store
          targets: [vectors]

  retrieval:
    - stage: retrieve
      steps:
        - type: retrieval.vector
          config:
            topK: 10
            minSimilarity: 0.7
    - stage: rerank
      steps:
        - type: rerank.llm
          config:
            topN: 5

modules:
  - type: module.rag-query
    config:
      endpoint: /api/query
  - type: module.document-ingest  # Document upload support
    config:
      endpoint: /api/documents
      maxFileSizeMB: 50
  - type: module.health

features:
  auth:
    type: auth.azuread  # Integrate with company Azure AD
    config:
      tenantId: ${AZURE_AD_TENANT_ID}
      clientId: ${AZURE_AD_CLIENT_ID}

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
    name: CompanyDocsRag
    sdk: net9.0
  docker:
    enabled: true  # Production-ready as requested
```

---

## Auto-Fix Capabilities

### Build Error Resolution

```csharp
// Conceptual: What the AI should do
public class BuildErrorHandler
{
    public async Task<FixResult> AnalyzeAndFix(string buildOutput)
    {
        // Pattern: Missing using statement
        if (buildOutput.Contains("CS0246") && buildOutput.Contains("could not be found"))
        {
            var missingType = ExtractTypeName(buildOutput);
            var @namespace = LookupNamespace(missingType);
            return new FixResult
            {
                Action = FixAction.AddUsing,
                File = ExtractFileName(buildOutput),
                Value = @namespace
            };
        }

        // Pattern: Missing NuGet package
        if (buildOutput.Contains("NU1101"))
        {
            var package = ExtractPackageName(buildOutput);
            return new FixResult
            {
                Action = FixAction.AddPackage,
                Package = package
            };
        }

        // Pattern: Null reference in config
        if (buildOutput.Contains("cannot be null") || buildOutput.Contains("ArgumentNullException"))
        {
            return new FixResult
            {
                Action = FixAction.AddConfiguration,
                Suggestion = "Check environment variables or appsettings.json"
            };
        }

        // Unknown error
        return new FixResult
        {
            Action = FixAction.AskUser,
            Message = buildOutput
        };
    }
}
```

### Common Fix Patterns

| Error Pattern | Fix Action |
|--------------|------------|
| `CS0246: Type not found` | Add using statement or NuGet package |
| `NU1101: Package not found` | Install NuGet package |
| `ArgumentNullException: ApiKey` | Set environment variable |
| `JsonException: Invalid JSON` | Fix appsettings.json syntax |
| `Cannot connect to database` | Check connection string |
| `Port already in use` | Change port in launchSettings.json |

---

## Spec Generation from Exploration

The AI can generate specs by analyzing existing applications.

### Reverse Engineering Workflow

```markdown
## When user says: "Make my existing app work with Hazina"

1. **Scan project structure**
   - Find .csproj files
   - Identify existing dependencies
   - Locate configuration files

2. **Analyze code patterns**
   - Find existing LLM usage
   - Identify storage patterns
   - Locate API endpoints

3. **Generate migration spec**
   - Map existing code to Hazina components
   - Create assembly specification
   - Generate migration plan

4. **Present options**
   - Show generated spec
   - Highlight incompatibilities
   - Suggest migration steps
```

---

## Interactive Build Mode

For complex applications, the AI can work interactively:

```
User: Build a RAG app

AI: I'll help you build a RAG application. Let me ask a few questions:

1. What LLM provider would you like to use?
   - OpenAI (recommended for quality)
   - Anthropic Claude
   - Ollama (local/private)

User: OpenAI

AI: 2. Where will you store document embeddings?
   - Supabase (recommended, cloud-hosted)
   - PostgreSQL with pgvector (self-hosted)
   - In-memory (development only)

User: Supabase

AI: 3. What modules do you need?
   - [x] RAG Query (answer questions from documents)
   - [x] Document Ingestion (upload new documents)
   - [ ] Search (standalone semantic search)
   - [ ] Chat (conversational interface)

User: Query and Ingestion

AI: I've generated the following specification:

[shows YAML]

Shall I create the project? I'll need these environment variables:
- OPENAI_API_KEY
- SUPABASE_URL
- SUPABASE_KEY

User: Yes, create it

AI: Creating project...
✓ Generated project structure
✓ Created 12 files
✓ Build successful

Your RAG application is ready at ./MyRagApp

Next steps:
1. Set environment variables
2. Run: dotnet run
3. Access API at: http://localhost:5000/swagger
```

---

## Integration with Worktree System

When the AI builds Hazina projects, it follows the worktree protocol:

```markdown
## Hazina Build in Worktree

1. **Allocate worktree**
   - Check worktrees.pool.md for FREE seat
   - Mark seat BUSY
   - Create worktree: `git worktree add ... -b agent-XXX-hazina-build`

2. **Generate project**
   - Run scaffold in worktree
   - Add generated files to git
   - Commit: "feat: Generate Hazina RAG application from spec"

3. **Validate**
   - Build the project
   - Run tests if present
   - Fix any issues

4. **Create PR**
   - Push branch
   - Create PR with description
   - Link to assembly spec

5. **Release worktree**
   - Clean up
   - Mark seat FREE
   - Prune worktree
```

---

## Implementation Tasks

### Week 1: Agent Instructions
- [ ] Create hazina-builder.agent.md
- [ ] Define component quick reference
- [ ] Document decision rules
- [ ] Add error handling patterns

### Week 2: Skill Definition
- [ ] Create hazina-build skill
- [ ] Implement NL-to-spec conversion
- [ ] Add auto-fix patterns
- [ ] Test with various inputs

### Week 3: Interactive Mode
- [ ] Design question flow
- [ ] Implement guided wizard
- [ ] Add validation checkpoints
- [ ] Create user feedback loop

### Week 4: Integration
- [ ] Connect with worktree system
- [ ] Add PR creation workflow
- [ ] Document edge cases
- [ ] Create demo scenarios

---

## Success Criteria

- [ ] AI generates valid spec from natural language
- [ ] Generated projects compile first try 90%+
- [ ] Auto-fix resolves common errors
- [ ] Full workflow completes in < 5 minutes
- [ ] Documentation covers all patterns

---

## Future Enhancements

### Model-Specific Optimization
- Use Haiku for spec validation (fast, cheap)
- Use Sonnet for code generation (balanced)
- Use Opus for complex debugging (thorough)

### Learning from Failures
- Log failed builds with causes
- Update patterns based on failures
- Improve prompts from experience

### Community Templates
- Share successful specs
- Rate and review templates
- Fork and customize

---

**This document completes the implementation plan series.**
**Return to:** [README.md](./README.md)
