# HazinaCoder Top 5 Improvements - Implementation Plan

**Date:** 2026-02-04
**Branch:** agent-003-massive-improvements-top5
**Agent:** agent-003
**Total Estimated Time:** 11 days

---

## Selected Improvements (Result/Effort Score 9.0+)

### 1. IMP-038: Diff Preview Mode (Score: 9.5)
**Effort:** 2 days
**Description:** Before applying changes, show unified diff with syntax highlighting. Approve/reject/modify.
**Business Value:** Prevents unwanted changes, review before execution, safer operation.

**Implementation:**
- File: `Core/Tools/DiffPreviewTool.cs`
- Generate unified diff before applying changes
- Use Spectre.Console for syntax-highlighted display
- Interactive prompt: [A]pprove, [R]eject, [M]odify, [V]iew full
- Integration with existing `apply_diff` tool

---

### 2. IMP-093: Secret Scanning (Score: 9.5)
**Effort:** 2 days
**Description:** Scan code for accidentally committed secrets. Prevent commits, alert user, suggest fixes.
**Business Value:** Prevents security breaches, compliance, best practices enforcement.

**Implementation:**
- File: `Core/Security/SecretScanner.cs`
- Pattern library for common secrets (API keys, passwords, tokens)
- Entropy-based detection for random strings
- Integration with git pre-commit hook
- Alert system with severity levels
- Suggest environment variable replacement

**Patterns to detect:**
```csharp
- AWS keys: AKIA[0-9A-Z]{16}
- GitHub tokens: ghp_[0-9a-zA-Z]{36}
- Slack tokens: xoxb-[0-9]+-[0-9]+-[0-9a-zA-Z]{24}
- Generic API keys: [Aa]pi[_-]?[Kk]ey.*['"][0-9a-zA-Z]{32,}['"]
- Password strings: [Pp]assword.*['"][^'\"]+['"]
- Connection strings with credentials
- Private keys (-----BEGIN.*PRIVATE KEY-----)
```

---

### 3. IMP-064: Package Manager Integration (Score: 9.5)
**Effort:** 2 days
**Description:** NuGet, npm, pip integration. Install packages, resolve dependencies, update versions.
**Business Value:** Dependency management, automated updates, vulnerability scanning.

**Implementation:**
- File: `Core/Tools/PackageManagerTool.cs`
- Support 3 package managers:
  - **NuGet:** dotnet add package, dotnet list package --outdated
  - **npm:** npm install, npm audit, npm outdated
  - **pip:** pip install, pip list --outdated
- Auto-detect package manager from project files
- Vulnerability scanning via audit commands
- Interactive update prompts

**Tool Methods:**
```csharp
- InstallPackage(packageManager, packageName, version)
- UpdatePackage(packageManager, packageName)
- ListOutdated(packageManager)
- AuditSecurity(packageManager)
- SearchPackage(packageManager, query)
```

---

### 4. IMP-053: Incremental Embeddings (Score: 9.5)
**Effort:** 2 days
**Description:** Only generate embeddings for changed content. Store embeddings with content hash.
**Business Value:** Reduces embedding API costs by 90%, faster learning updates.

**Implementation:**
- File: `Core/Learning/IncrementalEmbeddingCache.cs`
- Content-addressed storage using SHA256 hash
- Cache structure:
```csharp
public class EmbeddingCache
{
    public string ContentHash { get; set; }
    public float[] Embedding { get; set; }
    public DateTime Generated { get; set; }
    public int AccessCount { get; set; }
}
```
- File: `.hazina/embeddings-cache.json` (local cache)
- Integration with ExperienceStorage
- Cache invalidation on content change
- LRU eviction policy (max 10,000 entries)

**Cost Savings Example:**
- Before: 1,000 embeddings × $0.00002 = $0.02 per session
- After (90% cache hit): 100 embeddings × $0.00002 = $0.002 per session
- **Savings: 90%**

---

### 5. IMP-015: Graceful Degradation Framework (Score: 9.0)
**Effort:** 3 days
**Description:** When services unavailable (Qdrant, MCP), agent continues with reduced functionality. No hard failures.
**Business Value:** Reliability, offline operation, user not blocked by service outages.

**Implementation:**
- File: `Core/Infrastructure/GracefulDegradation.cs`
- Circuit breaker pattern for external services
- Capability detection system
- Fallback strategies:
  - Qdrant unavailable → Use in-memory experience storage
  - OpenAI API down → Use Ollama local models
  - MCP server down → Disable MCP tools, continue with core tools
- User notifications when operating in degraded mode
- Health check dashboard

**Circuit Breaker States:**
```csharp
public enum ServiceState
{
    Healthy,        // All good
    Degraded,       // Service slow/partial failure
    Failed,         // Service unavailable, using fallback
    Offline         // Offline mode, no external calls
}
```

---

## Implementation Order

**Day 1-2: Secret Scanning (Highest security impact)**
1. Create SecretScanner.cs
2. Implement pattern matching
3. Add entropy detection
4. Integrate with git workflow
5. Test with sample secrets
6. Documentation

**Day 3-4: Package Manager Integration (High utility)**
1. Create PackageManagerTool.cs
2. Implement NuGet support
3. Implement npm support
4. Implement pip support
5. Auto-detection logic
6. Test with real projects

**Day 5-6: Diff Preview Mode (Better UX)**
1. Create DiffPreviewTool.cs
2. Implement diff generation
3. Syntax highlighting
4. Interactive approval UI
5. Integration with apply_diff
6. Test with code changes

**Day 7-8: Incremental Embeddings (Cost optimization)**
1. Create IncrementalEmbeddingCache.cs
2. Content hashing
3. Cache storage
4. Integration with ExperienceStorage
5. Performance benchmarks
6. Cost analysis

**Day 9-11: Graceful Degradation (Reliability)**
1. Create GracefulDegradation.cs
2. Circuit breaker implementation
3. Capability detection
4. Fallback strategies
5. Health monitoring
6. Integration testing

---

## Testing Strategy

**Unit Tests:**
- SecretScanner: Pattern matching accuracy
- PackageManagerTool: Command generation
- DiffPreviewTool: Diff parsing
- EmbeddingCache: Hash collision, eviction
- Circuit breaker: State transitions

**Integration Tests:**
- Real package manager operations
- Qdrant failover
- OpenAI → Ollama fallback
- End-to-end workflow with degraded services

**Performance Tests:**
- Embedding cache hit rate
- Circuit breaker overhead
- Diff generation speed

---

## Success Criteria

✅ **Secret Scanning:**
- Detects 95%+ of common secret patterns
- <100ms scan time for typical file
- Zero false negatives for test cases
- Clear actionable warnings

✅ **Package Manager Integration:**
- Supports NuGet, npm, pip
- Auto-detects correct manager
- Handles offline scenarios
- Shows vulnerability counts

✅ **Diff Preview Mode:**
- Generates accurate unified diffs
- Syntax highlighting for C#, TypeScript, Python
- Interactive approval works
- Integrates with existing tools

✅ **Incremental Embeddings:**
- 90%+ cache hit rate after warmup
- Cost reduced by 85%+
- No accuracy degradation
- <10ms cache lookup

✅ **Graceful Degradation:**
- Works offline (no external services)
- <5s failover to fallback
- Clear status indicators
- No crashes from service failures

---

## Dependencies

**NuGet Packages:**
```xml
<!-- Already installed -->
<PackageReference Include="Spectre.Console" Version="0.49.1" />
<PackageReference Include="Qdrant.Client" Version="1.11.0" />
<PackageReference Include="Azure.AI.OpenAI" Version="1.0.0-beta.17" />

<!-- To add -->
<PackageReference Include="DiffPlex" Version="1.7.2" /> <!-- Diff generation -->
<PackageReference Include="Polly" Version="8.2.0" /> <!-- Circuit breaker -->
```

---

## File Structure

```
apps/CLI/Hazina.App.HazinaCoder/
├── Core/
│   ├── Security/
│   │   ├── SecretScanner.cs (NEW)
│   │   └── SecretPatterns.cs (NEW)
│   ├── Tools/
│   │   ├── DiffPreviewTool.cs (NEW)
│   │   └── PackageManagerTool.cs (NEW)
│   ├── Learning/
│   │   └── IncrementalEmbeddingCache.cs (NEW)
│   └── Infrastructure/
│       ├── GracefulDegradation.cs (NEW)
│       ├── CircuitBreaker.cs (NEW)
│       └── ServiceHealthMonitor.cs (NEW)
├── Tests/
│   ├── SecretScannerTests.cs (NEW)
│   ├── PackageManagerTests.cs (NEW)
│   ├── DiffPreviewTests.cs (NEW)
│   ├── EmbeddingCacheTests.cs (NEW)
│   └── CircuitBreakerTests.cs (NEW)
└── docs/
    ├── SECRET_SCANNING_GUIDE.md (NEW)
    ├── PACKAGE_MANAGEMENT_GUIDE.md (NEW)
    └── GRACEFUL_DEGRADATION.md (NEW)
```

---

## Risk Mitigation

**Risk:** Breaking existing functionality
**Mitigation:** Comprehensive unit tests, feature flags for new features

**Risk:** Performance degradation
**Mitigation:** Benchmark before/after, async operations

**Risk:** Incomplete offline support
**Mitigation:** Test without network, fallback to local models

**Risk:** Secret scanner false positives
**Mitigation:** Configurable sensitivity, whitelist mechanism

---

## Post-Implementation

**Documentation:**
- Update README.md with new capabilities
- CLI help text for new tools
- Architecture diagrams

**Metrics:**
- Track secret detection rate
- Monitor cache hit rates
- Measure cost savings
- Service degradation frequency

**Future Enhancements:**
- Custom secret patterns via config
- More package managers (cargo, gradle)
- Visual diff viewer in web UI
- Distributed embedding cache

---

**Status:** Ready for implementation
**Estimated Completion:** 2026-02-15 (11 days)
**Expected Impact:** Transform HazinaCoder from prototype → production-ready
