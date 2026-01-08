# Hazina Clean Code Backlog

**Total Items**: 50
**Generated**: 2026-01-08
**Goal**: Achieve "30-60 second comprehension" at every level

---

## Scoring Legend

- **Value (V)**: Impact on comprehension/quality (1-10)
- **Effort (E)**: Time/complexity to implement (1-10, lower = easier)
- **Risk (R)**: Breaking change potential (1-10, lower = safer)
- **Score**: V / (E + R) - higher is better

---

## Category A: Instant Comprehension Architecture

| ID | Item | Value | Effort | Risk | Score | Status |
|----|------|-------|--------|------|-------|--------|
| A1 | SYSTEM_SNAPSHOT.md - Single-screen system overview | 10 | 3 | 1 | **2.50** | DONE |
| A2 | Visual system maps - ASCII diagrams per domain | 9 | 4 | 1 | **1.80** | TODO |
| A3 | Unified folder structure - Domain-first organization | 8 | 7 | 6 | 0.62 | BACKLOG |
| A4 | ENTRYPOINT.md per domain - 5-10 line entry points | 9 | 2 | 1 | **3.00** | DONE |
| A5 | FLOW.md per service - Flow documentation | 8 | 3 | 1 | **2.00** | TODO |
| A6 | FAILURE_MODES.md - Traffic light failure docs | 8 | 3 | 1 | **2.00** | TODO |
| A7 | Critical path markers - README badges | 7 | 2 | 1 | **2.33** | DONE |
| A8 | Project categorization - CORE/EXTENDED/DEMO/DEPRECATED | 8 | 3 | 2 | 1.60 | BACKLOG |
| A9 | BOOTSTRAP.md - Zero-to-running guide | 9 | 2 | 1 | **3.00** | DONE |
| A10 | DEPENDENCIES.md - Dependency graphs | 7 | 4 | 1 | 1.40 | BACKLOG |

---

## Category B: Zero-Interpretation Naming

| ID | Item | Value | Effort | Risk | Score | Status |
|----|------|-------|--------|------|-------|--------|
| B11 | Rename generic classes (Manager, Engine, Handler) | 7 | 5 | 4 | 0.78 | BACKLOG |
| B12 | Unify agent naming (Hazina.Agents.* → Hazina.AI.Agents.*) | 6 | 6 | 5 | 0.55 | BACKLOG |
| B13 | Base class to interface (ChatServiceBase → IChatCapable) | 6 | 6 | 5 | 0.55 | BACKLOG |
| B14 | Explicit partial naming (ChatService.Streaming.cs) | 5 | 3 | 2 | 1.00 | BACKLOG |
| B15 | Standardize namespace depth to 3-4 levels | 4 | 5 | 4 | 0.44 | BACKLOG |
| B16 | Rename extension files (Extensions.cs → StringExtensions.cs) | 5 | 2 | 2 | 1.25 | BACKLOG |
| B17 | Consolidate config classes by domain | 7 | 5 | 4 | 0.78 | BACKLOG |
| B18 | Document ILLMClient interface thoroughly | 6 | 2 | 1 | **2.00** | TODO |
| B19 | Model/DTO/Entity suffix convention | 5 | 6 | 5 | 0.45 | BACKLOG |
| B20 | Behavior-based test names | 7 | 4 | 2 | 1.17 | BACKLOG |

---

## Category C: Architectural Purity

| ID | Item | Value | Effort | Risk | Score | Status |
|----|------|-------|--------|------|-------|--------|
| C21 | Group tool services by domain | 6 | 7 | 6 | 0.46 | BACKLOG |
| C22 | Composition over inheritance (ChatServiceBase) | 7 | 8 | 7 | 0.47 | BACKLOG |
| C23 | Enforce dependency direction (Core ← Tools ← Apps) | 8 | 5 | 4 | 0.89 | BACKLOG |
| C24 | Central IOptions pattern | 6 | 5 | 4 | 0.67 | BACKLOG |
| C25 | Unified storage provider interface | 7 | 6 | 5 | 0.64 | BACKLOG |
| C26 | Explicit prompt loading (IPromptLoader) | 6 | 4 | 3 | 0.86 | BACKLOG |
| C27 | Extract Hazina.Resilience library | 6 | 5 | 4 | 0.67 | BACKLOG |
| C28 | Extract ICostTracker interface | 5 | 4 | 3 | 0.71 | BACKLOG |
| C29 | Separate Hazina.Health assembly | 5 | 4 | 3 | 0.71 | BACKLOG |
| C30 | Unified ILogger<T> everywhere | 6 | 3 | 2 | 1.20 | BACKLOG |

---

## Category D: Testing Excellence

| ID | Item | Value | Effort | Risk | Score | Status |
|----|------|-------|--------|------|-------|--------|
| D31 | Behavior-focused test structure | 6 | 6 | 4 | 0.60 | BACKLOG |
| D32 | Separate unit/integration tests | 6 | 4 | 3 | 0.86 | BACKLOG |
| D33 | TestData folders | 5 | 3 | 1 | 1.25 | BACKLOG |
| D34 | Architectural tests (ArchUnit.NET) | 8 | 4 | 2 | 1.33 | BACKLOG |
| D35 | Mutation testing (Stryker.NET) | 5 | 5 | 2 | 0.71 | BACKLOG |
| D36 | Zero flaky tests policy | 7 | 6 | 3 | 0.78 | BACKLOG |
| D37 | TESTING.md strategy document | 6 | 2 | 1 | **2.00** | TODO |
| D38 | Mock builder pattern | 5 | 4 | 2 | 0.83 | BACKLOG |
| D39 | Contract tests (Pact) | 6 | 6 | 3 | 0.67 | BACKLOG |
| D40 | Performance benchmarks (BenchmarkDotNet) | 6 | 5 | 2 | 0.86 | BACKLOG |

---

## Category E: Observability & Production

| ID | Item | Value | Effort | Risk | Score | Status |
|----|------|-------|--------|------|-------|--------|
| E41 | OpenTelemetry standardization | 7 | 6 | 4 | 0.70 | BACKLOG |
| E42 | Distributed tracing with correlation IDs | 7 | 6 | 4 | 0.70 | BACKLOG |
| E43 | Structured logging with semantic fields | 6 | 4 | 3 | 0.86 | BACKLOG |
| E44 | Deep health checks (dependencies, capacity) | 6 | 4 | 3 | 0.86 | BACKLOG |
| E45 | RUNBOOK.md per service | 7 | 3 | 1 | 1.75 | BACKLOG |

---

## Category F: Developer Experience

| ID | Item | Value | Effort | Risk | Score | Status |
|----|------|-------|--------|------|-------|--------|
| F46 | WHICH_SOLUTION.md decision tree | 7 | 1 | 1 | **3.50** | DONE |
| F47 | Dev container (.devcontainer/) | 6 | 5 | 2 | 0.86 | BACKLOG |
| F48 | Build benchmarks (PERFORMANCE.md) | 5 | 3 | 1 | 1.25 | BACKLOG |
| F49 | Central examples folder | 6 | 4 | 2 | 1.00 | BACKLOG |
| F50 | hazina CLI for common tasks | 7 | 7 | 3 | 0.70 | BACKLOG |

---

## Prioritized View (By Score)

| Rank | ID | Item | Score | Status |
|------|-----|------|-------|--------|
| 1 | F46 | WHICH_SOLUTION.md | 3.50 | DONE |
| 2 | A4 | ENTRYPOINT.md x5 | 3.00 | DONE |
| 3 | A9 | BOOTSTRAP.md | 3.00 | DONE |
| 4 | A1 | SYSTEM_SNAPSHOT.md | 2.50 | DONE |
| 5 | A7 | Critical path markers | 2.33 | DONE |
| 6 | A5 | FLOW.md per service | 2.00 | TODO |
| 7 | A6 | FAILURE_MODES.md | 2.00 | TODO |
| 8 | D37 | TESTING.md | 2.00 | TODO |
| 9 | B18 | ILLMClient docs | 2.00 | TODO |
| 10 | A2 | Visual system maps | 1.80 | TODO |
| 11 | E45 | RUNBOOK.md per service | 1.75 | BACKLOG |
| 12 | A8 | Project categorization | 1.60 | BACKLOG |
| 13 | A10 | DEPENDENCIES.md | 1.40 | BACKLOG |
| 14 | D34 | Architectural tests | 1.33 | BACKLOG |
| 15 | B16 | Rename extension files | 1.25 | BACKLOG |
| 16 | D33 | TestData folders | 1.25 | BACKLOG |
| 17 | F48 | Build benchmarks | 1.25 | BACKLOG |
| 18 | C30 | Unified ILogger<T> | 1.20 | BACKLOG |
| 19 | B20 | Behavior-based test names | 1.17 | BACKLOG |
| 20 | B14 | Explicit partial naming | 1.00 | BACKLOG |
| 21 | F49 | Central examples folder | 1.00 | BACKLOG |
| 22 | C23 | Enforce dependency direction | 0.89 | BACKLOG |
| 23 | C26 | Explicit prompt loading | 0.86 | BACKLOG |
| 24 | D32 | Separate unit/integration | 0.86 | BACKLOG |
| 25 | D40 | Performance benchmarks | 0.86 | BACKLOG |
| 26 | E43 | Structured logging | 0.86 | BACKLOG |
| 27 | E44 | Deep health checks | 0.86 | BACKLOG |
| 28 | F47 | Dev container | 0.86 | BACKLOG |
| 29 | D38 | Mock builder pattern | 0.83 | BACKLOG |
| 30 | B11 | Rename generic classes | 0.78 | BACKLOG |
| 31 | B17 | Consolidate config | 0.78 | BACKLOG |
| 32 | D36 | Zero flaky tests | 0.78 | BACKLOG |
| 33 | C28 | Extract ICostTracker | 0.71 | BACKLOG |
| 34 | C29 | Separate Hazina.Health | 0.71 | BACKLOG |
| 35 | D35 | Mutation testing | 0.71 | BACKLOG |
| 36 | E41 | OpenTelemetry | 0.70 | BACKLOG |
| 37 | E42 | Distributed tracing | 0.70 | BACKLOG |
| 38 | F50 | hazina CLI | 0.70 | BACKLOG |
| 39 | C24 | Central IOptions | 0.67 | BACKLOG |
| 40 | C27 | Extract Hazina.Resilience | 0.67 | BACKLOG |
| 41 | D39 | Contract tests | 0.67 | BACKLOG |
| 42 | C25 | Unified storage provider | 0.64 | BACKLOG |
| 43 | A3 | Unified folder structure | 0.62 | BACKLOG |
| 44 | D31 | Behavior-focused tests | 0.60 | BACKLOG |
| 45 | B12 | Unify agent naming | 0.55 | BACKLOG |
| 46 | B13 | Base class to interface | 0.55 | BACKLOG |
| 47 | C22 | Composition over inheritance | 0.47 | BACKLOG |
| 48 | C21 | Group tool services | 0.46 | BACKLOG |
| 49 | B19 | Model/DTO/Entity suffix | 0.45 | BACKLOG |
| 50 | B15 | Standardize namespace depth | 0.44 | BACKLOG |

---

## Status Summary

| Status | Count | Percentage |
|--------|-------|------------|
| DONE | 5 | 10% |
| TODO | 5 | 10% |
| BACKLOG | 40 | 80% |

---

## Implementation Phases

### Phase 0: Foundation (DONE)
- [x] F46: WHICH_SOLUTION.md
- [x] A4: ENTRYPOINT.md x5
- [x] A9: BOOTSTRAP.md
- [x] A1: SYSTEM_SNAPSHOT.md
- [x] A7: Critical path markers

### Phase 1: Documentation Excellence (TODO)
- [ ] A2: Visual system maps
- [ ] A5: FLOW.md per service
- [ ] A6: FAILURE_MODES.md
- [ ] D37: TESTING.md
- [ ] B18: ILLMClient docs

### Phase 2: Quick Code Wins
- [ ] C30: Unified ILogger<T>
- [ ] B14: Explicit partial naming
- [ ] B16: Rename extension files
- [ ] D33: TestData folders
- [ ] D34: Architectural tests

### Phase 3: Testing Infrastructure
- [ ] D32: Separate unit/integration
- [ ] D40: Performance benchmarks
- [ ] D38: Mock builder pattern
- [ ] B20: Behavior-based test names
- [ ] D36: Zero flaky tests

### Phase 4: Observability
- [ ] E45: RUNBOOK.md per service
- [ ] E43: Structured logging
- [ ] E44: Deep health checks
- [ ] E41: OpenTelemetry
- [ ] E42: Distributed tracing

### Phase 5: Architectural Refinement
- [ ] C23: Enforce dependency direction
- [ ] C26: Explicit prompt loading
- [ ] C24: Central IOptions
- [ ] C28: Extract ICostTracker
- [ ] C29: Separate Hazina.Health

### Phase 6: Major Refactoring (High Risk)
- [ ] B11: Rename generic classes
- [ ] B12: Unify agent naming
- [ ] B13: Base class to interface
- [ ] C22: Composition over inheritance
- [ ] A3: Unified folder structure

---

## How to Use This Backlog

1. **Pick items by score**: Higher score = better value/effort/risk ratio
2. **Work in phases**: Complete each phase before moving to next
3. **Documentation first**: Phases 0-1 are zero risk, do them first
4. **Mark status**: Update this file as items are completed
5. **Review quarterly**: Re-score items as codebase evolves

---

## Quick Filters

### Zero Risk Items (Risk = 1)
- F46, A4, A9, A1, A7, A5, A6, D37, B18, A2, E45, A10, D33, F48

### High Value Items (Value >= 8)
- A1, A2, A3, A4, A5, A6, A8, A9, C23, D34

### Quick Wins (Effort <= 3)
- F46, A4, A9, A7, D37, B18, E45, D33, F48, B14, B16

---

*Last Updated: 2026-01-08*
*Maintainer: Clean Code Initiative*
