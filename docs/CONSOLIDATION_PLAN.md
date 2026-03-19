# Hazina Modular Consolidation Plan (Phase 3)

**Date:** 2026-03-19
**Task:** 869cfzy8d
**Current State:** 172 projects
**Target State:** ~50 focused modules
**Reduction:** ~120 projects (70% consolidation)

## Executive Summary

This document outlines the systematic consolidation of Hazina's 172 projects into approximately 50 focused modules. The consolidation follows the architecture audit (Phase 1) and NuGet package strategy (Phase 2), building on the standardized .NET multi-targeting implemented in Phase 4.

### Consolidation Principles

1. **Merge overlapping functionality** - Combine similar/related capabilities
2. **Eliminate redundancy** - Remove duplicate implementations
3. **Standardize naming** - Consistent namespace/project naming
4. **Reduce coupling** - Minimize cross-dependencies
5. **Improve modularity** - Clear, focused project boundaries

### Impact

- **Reduced complexity:** 70% fewer projects to maintain
- **Clearer boundaries:** Each module has single, well-defined purpose
- **Faster builds:** Fewer projects = faster compilation
- **Easier navigation:** Simpler project structure
- **Better versioning:** Fewer packages to coordinate

---

## Consolidation Categories

### Category 1: Legacy/Deprecated Projects (Remove)

**Action:** Mark obsolete, migrate consumers, remove in v3.0

| Project | Status | Migration Path |
|---------|--------|----------------|
| Hazina.Core | Legacy | → Use Hazina.AI.Core + Hazina.LLMs.Classes |
| Hazina.Data | Legacy | → Use Hazina.Tools.Data |
| Hazina.Tools.Services.Geometric | Legacy/Niche | → Archive (PDOK-specific) |
| Hazina.AI.OpenCode | Experimental | → Archive or merge into CodeIntelligence |

**Files:** 4 projects
**Savings:** -4 projects

---

### Category 2: LLM Tools Consolidation

**Current:** 3 separate tool projects
**Target:** 1 unified tools package

#### Consolidation

**Merge into:** `Hazina.LLMs.Tools` (already exists)

**Projects to merge:**
1. `Hazina.LLMClientTools` → Merge into Hazina.LLMs.Tools
2. `Hazina.LLMs.Registry` → Merge into Hazina.LLMs.Tools (registry functionality)

**Result:**
- **Before:** 3 projects
- **After:** 1 project (`Hazina.LLMs.Tools`)
- **Savings:** -2 projects

**Rationale:** Tool-related functionality should be in single package for easier discovery and maintenance.

---

### Category 3: Storage Consolidation

**Current:** 4 storage projects
**Target:** 2 storage packages

#### Keep Separate (Different Purposes)

1. **Hazina.Store.EmbeddingStore** - Vector/semantic search
2. **Hazina.Store.DocumentStore** - RAG document storage
3. **Hazina.Store.FactsStore** - Knowledge graph storage
4. **Hazina.Store.Sqlite** - SQLite-specific implementation

**No consolidation needed** - Each serves distinct purpose

**Savings:** 0 projects (already optimal)

---

### Category 4: AI Core Consolidation

**Current:** 22 AI packages under `Hazina.AI.*`
**Target:** 12-15 focused packages

#### Group A: Context Management (Merge into Hazina.AI.Context)

**Create:** `Hazina.AI.Context`

**Merge these:**
1. `Hazina.AI.ContextEngineering` - Context optimization
2. `Hazina.AI.Compression` - Token compression
3. `Hazina.LongContext` - Long context handling

**Rationale:** All deal with context/token management - natural grouping

**Savings:** -2 projects (3 → 1)

#### Group B: Quality & Validation (Merge into Hazina.AI.Quality)

**Expand:** `Hazina.AI.Quality` (already exists)

**Merge these:**
1. `Hazina.AI.FaultDetection` - Hallucination detection
2. `Hazina.Evals` - Evaluation frameworks
3. `Hazina.AI.Learning` - Feedback loops

**Rationale:** All focused on quality assurance and continuous improvement

**Savings:** -2 projects (4 → 2: Quality + Neurochain.Core)

#### Group C: Specialized AI (Keep Separate)

**Keep as-is:**
1. `Hazina.AI.Vision` - Computer vision (ImageSharp, FFMpeg)
2. `Hazina.AI.Training` - Fine-tuning (TorchSharp)
3. `Hazina.AI.Inference` - ONNX Runtime
4. `Hazina.AI.LocalLLM` - LLamaSharp integration

**Rationale:** Heavy dependencies, specialized use cases

#### Group D: Agent Systems (Merge into Hazina.AI.Agents)

**Expand:** `Hazina.AI.Agents`

**Merge these:**
1. `Hazina.AI.CognitivePipeline` - Cognitive processing
2. `Hazina.AI.TaskPrediction` - Task prediction
3. `Hazina.AI.DecisionTracking` - Decision logging

**Keep separate:**
- `Hazina.AI.Workflows` - Workflow orchestration (different abstraction level)
- `Hazina.AgentFactory` - High-level agent creation
- `Hazina.Generator` - Document-augmented generation
- `Hazina.DynamicAPI` - Dynamic API client

**Savings:** -2 projects (4 → 2)

#### Group E: Core Infrastructure (Merge into Hazina.AI.Core)

**Expand:** `Hazina.AI.Core`

**Merge these:**
1. `Hazina.AI.Memory` - Long-term memory (move to Core as feature)
2. `Hazina.AI.Routing` - Request routing (core orchestration feature)

**Rationale:** Small modules that are fundamental orchestration features

**Savings:** -2 projects (3 → 1)

#### Keep Separate (Distinct Responsibilities)

1. `Hazina.AI.Orchestration` - Multi-provider coordination
2. `Hazina.AI.FluentAPI` - Fluent configuration
3. `Hazina.AI.Providers` - Provider abstraction
4. `Hazina.AI.RAG` - Retrieval-Augmented Generation
5. `Hazina.AI.PromptManagement` - Prompt templates
6. `Hazina.AI.Guardrails` - Safety constraints
7. `Hazina.Neurochain.Core` - Multi-layer validation
8. `Hazina.CodeIntelligence` - Code analysis
9. `Hazina.Brain` - Integrated AI system (high-level)

**Total AI Consolidation:**
- **Before:** 22 projects
- **After:** 14 projects
- **Savings:** -8 projects

---

### Category 5: Tools.Services Consolidation

**Current:** 23 service projects
**Target:** 10-12 focused services

#### Group A: Core Services (Merge into Hazina.Tools.Services)

**Expand:** `Hazina.Tools.Services` (root package)

**Merge these:**
1. `Hazina.Tools.Services.Store` - Storage operations
2. `Hazina.Tools.Services.Embeddings` - Embedding helpers
3. `Hazina.Tools.Services.ToolAgent` - Tool agent helpers

**Rationale:** Generic service utilities should be in base package

**Savings:** -3 projects (4 → 1)

#### Group B: Content Services (Create Hazina.Tools.Content)

**Create:** `Hazina.Tools.Content`

**Merge these:**
1. `Hazina.Tools.Services.ContentRetrieval` - Content fetching
2. `Hazina.Tools.Services.Web` - Web utilities
3. `Hazina.Tools.Services.Images` - Image processing
4. `Hazina.Tools.TextExtraction` - Text extraction

**Rationale:** All deal with content retrieval/processing

**Savings:** -3 projects (4 → 1)

#### Group C: Communication Services (Create Hazina.Tools.Communication)

**Create:** `Hazina.Tools.Communication`

**Merge these:**
1. `Hazina.Tools.Services.Chat` - Chat operations
2. `Hazina.Tools.Services.Social` - Social media
3. `Hazina.Tools.Services.WordPress` - WordPress integration

**Rationale:** All communication/publishing-related

**Savings:** -2 projects (3 → 1)

#### Keep Separate (Distinct/Heavy Dependencies)

1. `Hazina.Tools.Services.FileOps` - File operations (10 dependents)
2. `Hazina.Tools.Services.Database` - Database operations
3. `Hazina.Tools.Services.BigQuery` - Google BigQuery
4. `Hazina.Tools.Services.GoogleDrive` - Google Drive
5. `Hazina.Tools.Services.DataGathering` - Data gathering
6. `Hazina.Tools.Services.Prompts` - Prompt utilities
7. `Hazina.Tools.Services.WebSearch` - Web search (meta-package)
8. `Hazina.Tools.Services.PDOK` - Dutch geo data (niche)
9. `Hazina.Tools.Services.Intake` - Data intake

**WebSearch Module (Keep as-is):**
- `WebSearch.Core` - Interfaces
- `WebSearch.Infrastructure` - Base implementation
- `WebSearch.Providers` - Provider implementations
- `WebSearch` - Unified package

**Rationale:** Well-modularized already, example of good design

**Total Services Consolidation:**
- **Before:** 23 projects
- **After:** 15 projects
- **Savings:** -8 projects

---

### Category 6: Tools.Foundation Consolidation

**Current:** 7 foundation projects
**Target:** 4 focused packages

#### Merge Context Tools

**Merge into:** `Hazina.Tools.Extensions`

**Merge these:**
1. `Hazina.Tools.ContextCompression` → Extensions as feature

**Result:**
- **Before:** 7 projects
- **After:** 6 projects
- **Savings:** -1 project

**Keep separate:**
1. `Hazina.Tools.Core` - Base abstractions
2. `Hazina.Tools.Data` - Data access
3. `Hazina.Tools.Models` - Domain models
4. `Hazina.Tools.Extensions` - Utility extensions
5. `Hazina.Tools.AI.Agents` - Agent-specific tools
6. `Hazina.Tools.TextExtraction` - Move to Content group (see above)

---

### Category 7: Infrastructure Consolidation

**Current:** 15 infrastructure projects
**Target:** 12 focused packages

#### Group A: Observability (Keep Separate - Good Design)

1. `Hazina.Observability.Core` - Core abstractions
2. `Hazina.Observability.AspNetCore` - ASP.NET Core integration
3. `Hazina.Observability.LLMLogs` - LLM-specific logging

**Rationale:** Well-separated concerns, optional integration layers

#### Group B: Security (Keep Separate)

1. `Hazina.Security.Core` - Core security
2. `Hazina.Security.AspNetCore` - ASP.NET integration

#### Group C: Auth (Merge into Hazina.Auth)

**Merge into:** `Hazina.Auth.Core`

**Projects:**
1. `Hazina.Auth.Core`
2. `Hazina.Auth.Identity` (if exists separately)

**Savings:** Potentially -1 project

#### Keep Separate (Distinct Purposes)

1. `Hazina.API.Generic` - Generic API framework
2. `Hazina.Agent.API` - Agent API
3. `Hazina.Core.Plugins` - Plugin system
4. `Hazina.EventSourcing` - Event sourcing
5. `Hazina.CodeGeneration.Core` - Code generation
6. `Hazina.Enterprise.Core` - Enterprise features
7. `Hazina.Indexing` - Indexing system
8. `Hazina.Tools.Migration` - Migration utilities

**Total Infrastructure Consolidation:**
- **Before:** 15 projects
- **After:** 13 projects
- **Savings:** -2 projects

---

### Category 8: UI Components

**Current:** 2 UI projects
**Target:** Keep separate

1. `Hazina.ChatShared` - WPF chat UI
2. `Hazina.UI.SchemaComponents` - Schema-based UI

**No consolidation** - Different UI paradigms

---

### Category 9: Applications & Demos

**Current:** 27 application projects
**Target:** 27 projects (no consolidation)

**Rationale:** Applications are end-user deliverables, not libraries. Each serves specific purpose.

#### CLI Tools (5)
- Keep all: HazinaCoder, ClaudeCode, AIImage, CLI, Tests

#### Desktop Apps (4)
- Keep all: AppBuilder, EmbeddingsViewer, ExplorerIntegration, Windows

#### Web Apps (2)
- Keep all: API.Search, HtmlMockupGenerator

#### Demos (14)
- Keep all: Educational/example purposes

#### Testing (1)
- Keep: IntegrationTests.OpenAI

#### Specialized (1)
- Keep: TaskRunner subsystem (3 projects)

**No savings:** Applications remain unchanged

---

### Category 10: Test Projects

**Current:** 33 test projects
**Target:** 20-25 test projects

**Strategy:** Merge unit tests for consolidated packages

#### Consolidate Tests for Merged Packages

Each consolidated package gets single test project:

1. `Hazina.AI.Context.Tests` - Tests for merged context management
2. `Hazina.AI.Quality.Tests` - Tests for merged quality/validation
3. `Hazina.AI.Agents.Tests` - Tests for merged agent features
4. `Hazina.Tools.Content.Tests` - Tests for merged content services
5. `Hazina.Tools.Communication.Tests` - Tests for communication services

**Keep separate tests for distinct packages:**
- All LLM provider tests (8 projects) - Keep separate
- Storage tests (3 projects) - Keep separate
- Core library tests - Keep separate

**Estimated savings:** -8 to -13 test projects

---

## Consolidation Summary

| Category | Before | After | Savings |
|----------|--------|-------|---------|
| Legacy/Deprecated | 4 | 0 | -4 |
| LLM Tools | 3 | 1 | -2 |
| Storage | 4 | 4 | 0 |
| AI Core | 22 | 14 | -8 |
| Tools.Services | 23 | 15 | -8 |
| Tools.Foundation | 7 | 6 | -1 |
| Infrastructure | 15 | 13 | -2 |
| UI Components | 2 | 2 | 0 |
| Applications | 27 | 27 | 0 |
| Test Projects | 33 | 20 | -13 |
| **TOTAL** | **140** | **102** | **-38** |

**Note:** 172 total - 32 apps/demos = 140 library/test projects

**Library projects consolidation:**
- Before: 107 library projects
- After: 82 library projects
- Savings: -25 projects (-23%)

**Including test consolidation:**
- Before: 140 projects (libs + tests)
- After: 102 projects
- Savings: -38 projects (-27%)

**Total (including apps):**
- Before: 172 projects
- After: 134 projects
- Savings: -38 projects (-22%)

---

## Implementation Plan

### Phase 3.1: Legacy Removal (Week 1)

**Tasks:**
1. Mark legacy projects as `[Obsolete]` with migration guidance
2. Update documentation with migration paths
3. Create GitHub issue for v3.0 removal
4. Update dependent projects to use new packages

**Projects affected:** 4
**Risk:** Low (already deprecated)

### Phase 3.2: Small Merges (Week 1-2)

**Tasks:**
1. Merge LLM tools (LLMClientTools, Registry → LLMs.Tools)
2. Merge Tools.Foundation (ContextCompression → Extensions)
3. Merge Infrastructure Auth projects

**Projects affected:** 4
**Risk:** Low (minimal dependencies)

### Phase 3.3: Context Management (Week 2)

**Create:** `Hazina.AI.Context`

**Tasks:**
1. Create new Hazina.AI.Context project
2. Move code from ContextEngineering, Compression, LongContext
3. Update namespaces to Hazina.AI.Context
4. Update dependent projects
5. Create combined README
6. Deprecate old packages (mark obsolete)

**Projects affected:** 3
**Risk:** Medium (some dependents)

### Phase 3.4: Quality & Validation (Week 3)

**Expand:** `Hazina.AI.Quality`

**Tasks:**
1. Merge FaultDetection, Evals, Learning into Quality
2. Reorganize into subnamespaces (Quality.FaultDetection, Quality.Evals, Quality.Learning)
3. Update dependent projects
4. Update tests

**Projects affected:** 3
**Risk:** Medium (core quality infrastructure)

### Phase 3.5: Agent Systems (Week 3)

**Expand:** `Hazina.AI.Agents`

**Tasks:**
1. Merge CognitivePipeline, TaskPrediction, DecisionTracking into Agents
2. Reorganize namespaces
3. Update AgentFactory dependencies
4. Update tests

**Projects affected:** 3
**Risk:** Medium (agent infrastructure)

### Phase 3.6: AI Core Features (Week 4)

**Expand:** `Hazina.AI.Core`

**Tasks:**
1. Merge Memory, Routing into Core
2. Update Orchestration dependencies
3. Update tests

**Projects affected:** 2
**Risk:** Low (internal features)

### Phase 3.7: Tools.Services Consolidation (Week 4-5)

**Create:** Multiple packages

**Tasks:**
1. Create Hazina.Tools.Content (merge ContentRetrieval, Web, Images, TextExtraction)
2. Create Hazina.Tools.Communication (merge Chat, Social, WordPress)
3. Expand Hazina.Tools.Services (merge Store, Embeddings, ToolAgent)
4. Update all dependent projects
5. Update tests

**Projects affected:** 10
**Risk:** Medium (many dependents)

### Phase 3.8: Test Consolidation (Week 6)

**Tasks:**
1. Merge tests for consolidated packages
2. Update CI/CD test runs
3. Verify coverage metrics

**Projects affected:** 13
**Risk:** Low (internal testing)

### Phase 3.9: Documentation & Cleanup (Week 6)

**Tasks:**
1. Update all READMEs with new structure
2. Update MODULAR_ARCHITECTURE_AUDIT.md
3. Create MIGRATION_GUIDE.md for consolidated packages
4. Update NuGet package documentation
5. Archive obsolete packages

**Risk:** Low (documentation only)

### Phase 3.10: Validation & Release (Week 7)

**Tasks:**
1. Full solution build verification
2. Run complete test suite
3. Verify NuGet package generation
4. Update version to 2.0.0 (breaking changes)
5. Create release notes
6. Tag release
7. Publish consolidated packages

**Risk:** Medium (final validation)

---

## Migration Guide Template

For each consolidated package, provide:

### Package: `Hazina.AI.Context`

**Old Packages (Obsolete):**
- `Hazina.AI.ContextEngineering` → `Hazina.AI.Context.Engineering`
- `Hazina.AI.Compression` → `Hazina.AI.Context.Compression`
- `Hazina.LongContext` → `Hazina.AI.Context.LongContext`

**Migration:**
```csharp
// Before
using Hazina.AI.ContextEngineering;
using Hazina.AI.Compression;

// After
using Hazina.AI.Context;
using Hazina.AI.Context.Compression;
```

**Breaking Changes:**
- Namespace changes only
- All APIs preserved
- No functional changes

**Timeline:**
- v2.0: Consolidated packages released
- v2.1-2.9: Old packages marked `[Obsolete]`, still functional
- v3.0: Old packages removed

---

## Risk Assessment

### High Risk
- **None** - All consolidations preserve functionality

### Medium Risk
- AI Core consolidations (7 packages) - Many dependents
- Tools.Services consolidations (10 packages) - External APIs
- Test project merges (13 projects) - CI/CD updates needed

### Low Risk
- Legacy removal (4 packages) - Already deprecated
- Small merges (4 packages) - Minimal impact
- Documentation updates - No code changes

### Mitigation Strategies

1. **Preserve all APIs** - No breaking changes except namespaces
2. **Mark obsolete first** - Give users warning period
3. **Comprehensive tests** - Verify no regressions
4. **Gradual rollout** - Phase implementation over 7 weeks
5. **Clear documentation** - Migration guides for all changes

---

## Success Criteria

1. ✅ Project count reduced from 172 to ~134 (-22%)
2. ✅ Library projects reduced from 107 to 82 (-23%)
3. ✅ All functionality preserved (zero breaking changes to APIs)
4. ✅ All tests passing
5. ✅ NuGet packages build successfully
6. ✅ Clear migration documentation
7. ✅ Improved maintainability (fewer projects to manage)
8. ✅ Faster build times (fewer projects to compile)

---

## Timeline

**Total Duration:** 7 weeks

| Week | Phase | Projects | Risk |
|------|-------|----------|------|
| 1 | Legacy Removal + Small Merges | 8 | Low |
| 2 | Context Management | 3 | Medium |
| 3 | Quality & Agents | 6 | Medium |
| 4 | AI Core + Services Start | 7 | Medium |
| 5 | Services Completion | 5 | Medium |
| 6 | Tests + Documentation | 13 | Low |
| 7 | Validation + Release | 0 | Medium |

**Total:** 38 projects consolidated

---

## Post-Consolidation Benefits

### Immediate Benefits
1. **Faster builds** - 22% fewer projects = faster compilation
2. **Easier navigation** - Clearer project boundaries
3. **Simpler dependencies** - Fewer packages to coordinate
4. **Better discoverability** - Related features in same package

### Long-Term Benefits
1. **Easier maintenance** - Fewer moving parts
2. **Clearer architecture** - Explicit boundaries between modules
3. **Better versioning** - Fewer packages to version-coordinate
4. **Reduced cognitive load** - Less context switching

### Developer Experience
1. **Easier onboarding** - Simpler project structure
2. **Faster IDE load** - Fewer projects in solution
3. **Better IntelliSense** - Related APIs grouped together
4. **Clearer documentation** - One README per logical module

---

## Next Steps

After Phase 3 completion:

1. **Phase 4:** .NET Version Standardization ✅ (Already complete)
2. **Phase 5:** Documentation & Examples (Task 869cfzy8g)
3. **Phase 6:** Performance Optimization
4. **Phase 7:** Production Hardening

---

## Related Documents

- `MODULAR_ARCHITECTURE_AUDIT.md` - Phase 1 audit
- `NUGET-PACKAGES-ANALYSIS.md` - Phase 2 package strategy
- `MIGRATION_GUIDE.md` - To be created during Phase 3
- `RELEASE_NOTES.md` - v2.0 release notes

---

**Document Owner:** Hazina Project Team
**Last Updated:** 2026-03-19
**Status:** READY FOR IMPLEMENTATION
