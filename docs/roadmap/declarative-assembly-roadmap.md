# Hazina Declarative Assembly System - Implementation Roadmap

**Status:** Approved for Implementation
**Version:** 1.0
**Created:** 2026-01-13
**Timeline:** 16 weeks (4 months)

---

## Executive Summary

This roadmap outlines the implementation plan for the **Declarative RAG App Building System** - a transformative capability that allows developers to create production-ready Hazina applications from YAML specifications or natural language descriptions.

### Strategic Value

| Metric | Before | After |
|--------|--------|-------|
| Time to first RAG app | 2-4 hours | 15 minutes |
| Boilerplate code | ~500 lines | 0 lines |
| Configuration errors | Common | Validated |
| Provider switching | Manual refactor | Config change |
| AI-assisted building | Not possible | Native |

### Key Deliverables

1. **Component Registry** - Machine-readable catalog of all Hazina components
2. **Assembly Specification** - YAML format for declarative app definitions
3. **Scaffold Generator** - Code generation from specifications
4. **Visual Studio Template** - One-click project creation
5. **AI Build Orchestrator** - Claude Code integration for autonomous building

---

## Implementation Phases

### Overview Timeline

```
         Week 1-4           Week 5-8          Week 9-12         Week 13-16
        ┌──────────┐       ┌──────────┐       ┌──────────┐       ┌──────────┐
        │ PHASE 1  │       │ PHASE 2  │       │ PHASE 3  │       │ PHASE 4  │
        │Foundation│──────►│Completion│──────►│   DX     │──────►│   AI     │
        │          │       │          │       │          │       │          │
        │Registry  │       │ Full     │       │Templates │       │ Claude   │
        │Schema    │       │ Coverage │       │ CLI      │       │ Code     │
        │Generator │       │ Pipelines│       │ Docs     │       │ Building │
        └──────────┘       └──────────┘       └──────────┘       └──────────┘
```

---

## Phase 1: Foundation (Weeks 1-4)

**Goal:** Establish core infrastructure for declarative assembly

### Week 1-2: Component Registry

**Deliverables:**
- [ ] Component definition JSON schema
- [ ] ComponentDefinition, ConfigSchema models
- [ ] YAML/JSON parsing with validation
- [ ] ComponentCatalog with embedded resources
- [ ] Initial 20 component definitions (core providers)

**Files Created:**
```
src/Core/AI/Hazina.AI.Assembly/
├── Hazina.AI.Assembly.csproj
├── Registry/
│   ├── IComponentRegistry.cs
│   ├── ComponentRegistry.cs
│   ├── ComponentCatalog.cs
│   └── ComponentDefinition.cs
├── Schema/
│   └── component-schema.json
└── Components/
    └── providers/
        ├── llm.openai.component.yaml
        ├── llm.anthropic.component.yaml
        └── ... (20 components)
```

**Success Criteria:**
- [ ] Load components from embedded resources
- [ ] Query by ID, category, interface
- [ ] Validate component configs
- [ ] 100% test coverage for registry

### Week 3-4: Assembly Specification & Basic Generator

**Deliverables:**
- [ ] AssemblySpec model classes
- [ ] YAML parser with variable resolution
- [ ] JSON Schema for IDE support
- [ ] SchemaValidator, ComponentReferenceValidator
- [ ] Basic ScaffoldGenerator (project structure only)

**Files Created:**
```
src/Core/AI/Hazina.AI.Assembly/
├── Specification/
│   ├── AssemblySpec.cs
│   ├── MetadataSpec.cs
│   ├── ProvidersSpec.cs
│   └── VariableResolver.cs
├── Parsing/
│   └── SpecificationParser.cs
├── Validation/
│   ├── SpecificationValidator.cs
│   └── ValidationResult.cs
├── Generator/
│   └── ScaffoldGenerator.cs
└── Schema/
    └── assembly-1.0.json
```

**Success Criteria:**
- [ ] Parse example specifications
- [ ] Variable resolution works (${VAR})
- [ ] Generate basic project structure
- [ ] Validation catches common errors

### Phase 1 Milestone

```bash
# By end of Week 4, this should work:
hazina assemble minimal-rag.assembly.yaml --output ./MyApp
cd MyApp
dotnet build  # Should compile
```

---

## Phase 2: Completeness (Weeks 5-8)

**Goal:** Full coverage of Hazina features, complete code generation

### Week 5-6: Full Provider Coverage

**Deliverables:**
- [ ] All LLM provider components (OpenAI, Anthropic, Gemini, Ollama, etc.)
- [ ] All embedding provider components
- [ ] All storage components (document, vector, graph)
- [ ] OrchestrationSpec for failover configuration
- [ ] DI code generation for all providers

**Component Coverage:**
| Category | Components | Status |
|----------|------------|--------|
| LLM Providers | 7 | Phase 2 |
| Embedding Providers | 4 | Phase 2 |
| Document Storage | 4 | Phase 2 |
| Vector Storage | 5 | Phase 2 |
| Graph Storage | 3 | Phase 2 |
| **Total** | **23** | |

### Week 7-8: Pipeline & Module Coverage

**Deliverables:**
- [ ] Pipeline specification (ingestion, retrieval, graph)
- [ ] Pipeline step definitions
- [ ] Module components (rag-query, ingest, search, chat, health)
- [ ] Controller and service generation
- [ ] Feature components (auth, observability, security)

**Module Coverage:**
| Module | Endpoints | Status |
|--------|-----------|--------|
| module.rag-query | POST /api/query | Phase 2 |
| module.document-ingest | POST /api/ingest | Phase 2 |
| module.search | GET,POST /api/search | Phase 2 |
| module.chat | WS /api/chat | Phase 2 |
| module.health | GET /health | Phase 2 |

### Phase 2 Milestone

```bash
# By end of Week 8, this should work:
hazina assemble production-rag.assembly.yaml --output ./ProductionApp
cd ProductionApp
dotnet build   # ✓ Compiles
dotnet run     # ✓ Runs
curl /health   # ✓ Returns healthy
curl /swagger  # ✓ Shows API docs
```

---

## Phase 3: Developer Experience (Weeks 9-12)

**Goal:** Polish, templates, CLI, comprehensive documentation

### Week 9-10: dotnet new Templates

**Deliverables:**
- [ ] hazina-rag template (full featured)
- [ ] hazina-rag-minimal template (quick start)
- [ ] hazina-worker template (background processing)
- [ ] hazina-console template (CLI app)
- [ ] Template.json with all parameters
- [ ] NuGet package: Hazina.Templates

**Template Features:**
| Parameter | Options | Description |
|-----------|---------|-------------|
| --LlmProvider | openai, anthropic, ollama | Primary LLM |
| --VectorStore | memory, supabase, pgvector | Vector storage |
| --IncludeDocker | true/false | Docker files |
| --IncludeTests | true/false | Test project |
| --IncludeSwagger | true/false | API docs |

### Week 11-12: CLI & Documentation

**Deliverables:**
- [ ] `hazina assemble` CLI command
- [ ] `hazina validate` CLI command
- [ ] `hazina components list` CLI command
- [ ] Assembly specification guide
- [ ] Component reference documentation
- [ ] Tutorial: "Build RAG in 15 minutes"
- [ ] Example specifications library

**Documentation Structure:**
```
docs/
├── declarative-assembly/
│   ├── README.md (index)
│   ├── 01-COMPONENT_REGISTRY.md
│   ├── 02-ASSEMBLY_SPECIFICATION.md
│   ├── 03-SCAFFOLD_GENERATOR.md
│   ├── 04-VS_TEMPLATE.md
│   └── 05-AI_BUILD_ORCHESTRATOR.md
├── tutorials/
│   ├── 15-minute-rag.md
│   └── declarative-to-production.md
└── examples/
    ├── minimal-rag.assembly.yaml
    ├── production-rag.assembly.yaml
    └── multi-provider-rag.assembly.yaml
```

### Phase 3 Milestone

```bash
# By end of Week 12, these should work:

# Create from template
dotnet new hazina-rag -n MyApp --LlmProvider openai --VectorStore supabase
cd MyApp && dotnet run  # ✓ Works

# Create from spec
hazina assemble my-spec.yaml --output ./CustomApp
cd CustomApp && dotnet run  # ✓ Works

# List available components
hazina components list --category provider  # ✓ Shows all providers
```

---

## Phase 4: AI Integration (Weeks 13-16)

**Goal:** Claude Code integration for autonomous app building

### Week 13-14: Agent Instructions

**Deliverables:**
- [ ] hazina-builder.agent.md (control plane)
- [ ] hazina-build skill (auto-discovered)
- [ ] Component quick reference for AI
- [ ] Decision rules and patterns
- [ ] Error handling patterns

**Files Created:**
```
C:\scripts\
├── agents/
│   └── hazina-builder.agent.md
└── .claude/skills/
    └── hazina-build/
        └── SKILL.md
```

### Week 15-16: Natural Language Building

**Deliverables:**
- [ ] NL-to-spec conversion prompts
- [ ] Interactive build mode
- [ ] Auto-fix patterns (build errors)
- [ ] Worktree integration for builds
- [ ] Demo scenarios and testing

**AI Capabilities:**
| Input | Output |
|-------|--------|
| "Build a simple RAG" | Working project |
| "RAG with OpenAI and Supabase" | Custom spec + project |
| "Make this production-ready" | Docker + monitoring + auth |
| "Fix this build error" | Applied fix + rebuild |

### Phase 4 Milestone

```
# By end of Week 16, this should work:

User: "Build a RAG app for my company docs using Azure OpenAI and Supabase"

Claude:
1. Generates assembly specification
2. Shows spec for review
3. Creates project
4. Builds and validates
5. Presents working application
```

---

## Integration Points

### Integration with Existing Hazina Features

| Feature | Integration |
|---------|-------------|
| Hazina.AI.FluentAPI | Generated code uses FluentAPI |
| Hazina.AI.Providers | ProviderOrchestrator configured from spec |
| Hazina.AI.RAG | RAGEngine wired from pipeline spec |
| Hazina.Store.* | Storage configured from providers section |
| Hazina.Production.Monitoring | Observability features enabled via spec |

### Integration with Skills System

The Declarative Assembly system complements the Skills System (see [hazina-skills-roadmap.md](./hazina-skills-roadmap.md)):

| Skills System | Declarative Assembly |
|--------------|---------------------|
| Runtime workflows | Project generation |
| .hzskill files | .assembly.yaml files |
| SkillEngine execution | Scaffold generation |
| Composable skills | Composable components |

**Synergy:** Skills can be included in assembly specifications as custom modules.

---

## Resource Requirements

### Development Resources

| Phase | Effort | Skills |
|-------|--------|--------|
| Phase 1 | 2 developers, 4 weeks | C#, YAML, schema design |
| Phase 2 | 2 developers, 4 weeks | DI, code generation |
| Phase 3 | 1 developer + 1 tech writer, 4 weeks | Templates, docs |
| Phase 4 | 1 developer, 4 weeks | AI prompts, integration |

### Dependencies

| Dependency | Version | Purpose |
|------------|---------|---------|
| YamlDotNet | 15.0+ | YAML parsing |
| NJsonSchema | 11.0+ | JSON Schema validation |
| Scriban | 5.0+ | Template engine |
| Microsoft.Extensions.DI | 8.0+ | DI abstractions |

---

## Risk Assessment

### Technical Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Schema complexity | Medium | Medium | Start simple, iterate |
| Code generation bugs | Medium | High | Extensive testing |
| Template maintenance | Low | Medium | Centralized templates |
| AI unpredictability | Medium | Low | Validation layers |

### Mitigation Strategies

1. **Incremental validation** - Validate at each generation step
2. **Fallback options** - Manual creation always available
3. **Comprehensive testing** - Generated code tested in CI
4. **User feedback loop** - Early access program

---

## Success Metrics

### Quantitative

| Metric | Target | Measurement |
|--------|--------|-------------|
| Time to first build | < 5 min | User testing |
| Build success rate | > 95% | CI metrics |
| Spec validation errors | < 5% | User reports |
| Documentation coverage | 100% | Audit |

### Qualitative

- Developers prefer spec-based creation over manual
- AI builds meet user expectations
- Documentation is self-sufficient
- Error messages are actionable

---

## Rollout Plan

### Alpha (Week 8)
- Internal testing
- Limited component coverage
- Basic generation

### Beta (Week 12)
- External preview program
- Full component coverage
- Template packages

### GA (Week 16)
- Public release
- Full AI integration
- Marketplace presence

---

## Appendix: Example Specifications

### Minimal RAG (5 lines)
```yaml
version: "1.0"
metadata: { name: minimal-rag, version: 1.0.0 }
providers:
  llm: { primary: { type: llm.openai } }
  embedding: { type: embedding.openai }
  storage: { vectors: { type: vector.memory } }
```

### Production RAG (Full)
See: [02-ASSEMBLY_SPECIFICATION.md](../declarative-assembly/02-ASSEMBLY_SPECIFICATION.md#example-specifications)

---

## Appendix: CLI Commands

```bash
# Scaffold from spec
hazina assemble <spec-file> [--output <path>] [--dry-run]

# Validate specification
hazina validate <spec-file> [--strict]

# List available components
hazina components list [--category <cat>] [--format json|table]

# Show component details
hazina components show <component-id>

# Generate spec from template
hazina spec new <template> [--output <file>]

# Interactive spec builder
hazina spec wizard
```

---

## Document References

| Document | Purpose |
|----------|---------|
| [README.md](../declarative-assembly/README.md) | Overview |
| [01-COMPONENT_REGISTRY.md](../declarative-assembly/01-COMPONENT_REGISTRY.md) | Registry design |
| [02-ASSEMBLY_SPECIFICATION.md](../declarative-assembly/02-ASSEMBLY_SPECIFICATION.md) | Spec format |
| [03-SCAFFOLD_GENERATOR.md](../declarative-assembly/03-SCAFFOLD_GENERATOR.md) | Code generation |
| [04-VS_TEMPLATE.md](../declarative-assembly/04-VS_TEMPLATE.md) | Templates |
| [05-AI_BUILD_ORCHESTRATOR.md](../declarative-assembly/05-AI_BUILD_ORCHESTRATOR.md) | AI integration |

---

**Last Updated:** 2026-01-13
**Status:** Ready for Review and Approval
**Next Step:** Begin Phase 1 implementation
