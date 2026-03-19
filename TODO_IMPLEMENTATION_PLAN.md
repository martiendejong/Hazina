# Hazina TODO Implementation Plan

**Created:** 2026-03-19
**Total Tasks:** 28 remaining from original 45
**Estimated Timeline:** 4 weeks
**Approach:** Batched implementation by domain

---

## Implementation Batches

### Batch 1: Quick Wins (Week 1) - 3 tasks, ~8 hours

**Priority:** HIGH - User-facing documentation and critical bug fix

1. **869cabf30**: Fix: await embeddings init
   - **Issue:** DocumentStore may use uninitialized embedding stores
   - **Fix:** Add lazy initialization pattern to EmbeddingStore
   - **Files:**
     - `src/Core/Storage/Hazina.Store.DocumentStore/Core/DocumentStore.cs`
     - `src/Core/Storage/Hazina.Store.EmbeddingStore/Core/AbstractTextEmbeddingStore.cs`
   - **Implementation:** Add `private bool _initialized` flag, `EnsureInitializedAsync()` method
   - **Testing:** Unit tests for initialization race conditions
   - **Estimate:** 2 hours

2. **869cfzy8g**: Phase 5: Documentation & Examples
   - **Create:** `MODULE_GUIDE.md` with architectural overview
   - **Examples:** 5+ working examples in `docs/examples/`
     - Basic RAG pipeline
     - LLM provider switching
     - Custom tool creation
     - Embeddings storage
     - Agent orchestration
   - **Migration:** Add migration guide from pre-modular architecture
   - **Estimate:** 4 hours

3. **869cabf5d**: Add XML docs and samples
   - **Scope:** All public APIs need XML documentation
   - **Focus:** Core assemblies first (LLMs, Storage, Agents)
   - **Samples:** Inline code examples in XML docs
   - **Tool:** Use AI to generate initial XML docs, human review
   - **Estimate:** 2 hours

### Batch 2: Testing Infrastructure (Week 1-2) - 3 tasks, ~6 hours

**Priority:** MEDIUM - Foundation for quality

4. **869cabf59**: PartialJsonParser test coverage
   - **Current:** Minimal tests
   - **Target:** 90%+ code coverage
   - **Tests:** Edge cases (nested objects, arrays, malformed JSON)
   - **Files:** `tests/Hazina.LLMs.Tests/PartialJsonParserTests.cs`
   - **Estimate:** 2 hours

5. **869cabf56**: Benchmark split/token counter
   - **Create:** BenchmarkDotNet project
   - **Metrics:** Performance baselines for:
     - Document splitting (DocumentSplitter)
     - Token counting (TokenCounter)
     - Embedding generation
   - **Baseline:** Document current performance for regression detection
   - **Estimate:** 2 hours

6. **869cabf3h**: Unit/integration tests
   - **Scope:** Increase test coverage across framework
   - **Priority Areas:**
     - AgentFactory configuration parsing
     - LLM provider abstractions
     - Storage implementations
   - **Target:** 70%+ overall coverage
   - **Estimate:** 2 hours

### Batch 3: Code Quality (Week 2) - 2 tasks, ~4 hours

**Priority:** HIGH - Developer experience and safety

7. **869cabf5e**: Stabilize public API surfaces
   - **Document:** API stability guidelines
   - **Policy:** Semantic versioning commitment
   - **Identify:** Public APIs that need stabilization
   - **Mark:** Experimental APIs with `[Experimental]` attribute
   - **Create:** `docs/API_STABILITY.md`
   - **Estimate:** 2 hours

8. **869cabf5b**: Introduce analyzers and nullable refs
   - **Enable:** Nullable reference types across all projects
   - **Add:** Roslyn analyzers for:
     - Async best practices
     - Disposal patterns
     - Null safety
   - **Fix:** Existing nullable warnings
   - **Estimate:** 2 hours

### Batch 4: Embedding & Storage (Week 2-3) - 4 tasks, ~12 hours

**Priority:** MEDIUM - Performance and reliability

9. **869cabf54**: Embedding format + atomic writes
   - **Format:** Define standard embedding file format (JSON/binary)
   - **Atomic:** Implement write-temp-rename pattern
   - **Corruption:** Add checksum validation
   - **Files:**
     - `src/Core/Storage/Hazina.Store.EmbeddingStore/Stores/File/EmbeddingFileStore.cs`
   - **Estimate:** 3 hours

10. **869cabf53**: Embedding compaction + integrity
    - **Compaction:** Remove orphaned embeddings
    - **Integrity:** Validate embedding-document links
    - **Tool:** CLI command for maintenance
    - **Estimate:** 3 hours

11. **869cabf51**: Batch indexing APIs
    - **API:** Bulk document indexing endpoint
    - **Performance:** Parallel embedding generation
    - **Progress:** Callback for progress tracking
    - **Estimate:** 3 hours

12. **869cabf4v**: Alternative store adapters
    - **Providers:** Additional storage backends
      - Redis adapter
      - Azure Cosmos DB adapter
    - **Interface:** `IEmbeddingStore` standardization
    - **Estimate:** 3 hours

### Batch 5: Tool System (Week 3) - 6 tasks, ~15 hours

**Priority:** MEDIUM - Framework extensibility

13. **869cabf4g**: Structured logging + correlation
    - **Library:** Serilog enrichers
    - **Correlation:** Request ID tracking across tools
    - **Tracing:** Add distributed tracing headers
    - **Estimate:** 2 hours

14. **869cabf4d**: Tool Provider pattern
    - **Pattern:** Plugin architecture for tool discovery
    - **Registry:** Central tool registration
    - **Lifecycle:** Tool initialization/disposal
    - **Estimate:** 3 hours

15. **869cabf4a**: Tool validations/guardrails
    - **Validation:** Input parameter validation
    - **Guardrails:** Safety checks before execution
    - **Policies:** Configurable tool execution policies
    - **Estimate:** 3 hours

16. **869cabf45**: Mocks/fakes for tools
    - **Testing:** Mock implementations for all tools
    - **Fakes:** In-memory tool implementations
    - **Examples:** Test examples using mocks
    - **Estimate:** 2 hours

17. **869cabf42**: Message enricher pipeline
    - **Pipeline:** Middleware for message transformation
    - **Enrichers:**
      - Metadata injection
      - Context augmentation
      - Safety filters
    - **Estimate:** 3 hours

18. **869cabf3k**: Opt-in tool sets + schema
    - **Tool Sets:** Predefined tool combinations
    - **Schema:** JSON schema for tool configuration
    - **Examples:**
      - "code-assistant" tool set
      - "data-analyst" tool set
    - **Estimate:** 2 hours

### Batch 6: CI/CD (Week 3-4) - 2 tasks, ~6 hours

**Priority:** HIGH - Deployment automation

19. **869cabf3g**: CI + NuGet publish
    - **GitHub Actions:** Automated build/test/publish
    - **NuGet:** Publish packages to nuget.org
    - **Versioning:** Git tag-based versioning
    - **Matrix:** Multi-targeting (net8.0, net9.0, net10.0 when ready)
    - **Files:** `.github/workflows/ci.yml`
    - **Estimate:** 4 hours

20. **869cabf3a**: Additional providers
    - **LLM Providers:** Add support for:
      - Google Gemini (already exists, verify)
      - Anthropic Claude (already exists, verify)
      - Mistral
      - Local models (Ollama)
    - **Storage:** Verify pgvector, Faiss
    - **Estimate:** 2 hours

### Batch 7: MediaLibrary Extraction (Week 4) - 3 tasks, ~8 hours

**Priority:** LOW - Code organization

21. **869cabf2m**: Extract MediaLibrary to Hazina.UI
22. **869cabf2k**: (related extraction task)
23. **869cabf2h**: (related extraction task)
    - **Scope:** Move MediaLibrary component to new assembly
    - **New Assembly:** `Hazina.UI` or `Hazina.Tools.UI`
    - **Dependencies:** Clean up cross-assembly references
    - **Breaking:** May be breaking change, document migration
    - **Combined Estimate:** 8 hours

### Batch 8: Task Manager System (Week 4) - 5 tasks, ~20 hours

**Priority:** LOW - New feature area

24. **869caatrf**: Bundle Multiple Tray Apps
25. **869caatre**: Task Manager Window
26. **869caatrd**: System Tray Application
27. **869caatmc**: Migrate Scheduled Tasks + MSI
28. **869caatm9**: Cron-Style Task Scheduler

**Scope:** Complete task scheduling and management system
- **Architecture:** Quartz.NET integration
- **UI:** WPF/Avalonia tray application
- **Persistence:** Task configuration storage
- **MSI:** Installer for system service
- **Migration:** Migrate existing Windows Task Scheduler tasks

**Decision:** This is a substantial feature set that may warrant its own planning session. Recommend deferring to Week 5-6 or separate project phase.

**Combined Estimate:** 20+ hours

---

## Implementation Order Rationale

1. **Week 1: Quick Wins** - Immediate value, user-facing improvements
2. **Week 1-2: Testing** - Foundation for quality assurance
3. **Week 2: Quality** - Developer experience and API safety
4. **Week 2-3: Storage** - Core infrastructure reliability
5. **Week 3: Tools** - Framework extensibility
6. **Week 3-4: CI/CD** - Deployment automation
7. **Week 4: MediaLibrary** - Code organization (low priority)
8. **Week 4+: Task Manager** - Major feature (potentially separate phase)

---

## Risk Assessment

### High Risk
- **Task Manager System (Batch 8):** Large scope, unclear requirements, may need user clarification
- **EnableWindowsTargeting:** CI/CD batch must handle net9.0-windows projects correctly

### Medium Risk
- **Breaking Changes:** API stabilization and MediaLibrary extraction may break consumers
- **Test Coverage:** May uncover bugs requiring additional fixes

### Low Risk
- **Documentation:** Low technical risk, high time variability
- **Bug Fixes:** Well-scoped, clear requirements

---

## Success Metrics

- **Completion Rate:** Target 25/28 tasks (89%) in 4 weeks
- **Test Coverage:** Achieve 70%+ overall coverage
- **Documentation:** 100% public APIs have XML docs
- **CI/CD:** Automated NuGet publishing operational
- **Bug Fixes:** Zero P0/P1 bugs introduced by changes

---

## Next Steps

1. **User Approval:** Review this plan with user
2. **Batch 1 Start:** Begin with quick wins (bug fix + docs)
3. **Daily Commits:** Commit progress daily, create PRs per batch
4. **Weekly Review:** Check progress against timeline each Friday
5. **Defer If Needed:** Task Manager System (Batch 8) may shift to separate phase

---

**Updated:** 2026-03-19
**Status:** READY FOR APPROVAL
