# Remove Hardcoded Paths + DI Implementation

**ClickUp Task:** 869cabf3p
**Branch:** agent-019-remove-hardcoded-paths-di
**Objective:** Inject IFileSystem/IClock and remove hardcoded paths to improve testability and portability.

## Analysis Summary

**Total Core modules affected:** 12 modules
**Total files with DateTime.Now/UtcNow:** 124 files in Core
**Total files with direct File/Directory ops:** 62 files in Core

## Existing Infrastructure

✅ **Already exists:**
- `Hazina.AgentFactory.Abstractions.IClock` - Time abstraction with `SystemClock` and `FixedClock`
- `Hazina.AgentFactory.Abstractions.IFileSystem` - File system abstraction with `PhysicalFileSystem`
- `IFileInfo` and `IDirectoryInfo` interfaces

✅ **Already using DI:**
- `HazinaStoreConfigParserV2` - Constructor injection of `IFileSystem`

## Implementation Strategy

### Phase 1: Core Storage Modules (HIGH PRIORITY)
**Impact:** Storage is foundation for everything. 62 files, 13 with file ops.

1. **DocumentStore** (`src/Core/Storage/Hazina.Store.DocumentStore/`)
   - Replace `File.*` with `IFileSystem`
   - Add constructor parameter `IFileSystem? fileSystem = null`
   - Default to `new PhysicalFileSystem()` for backward compatibility

2. **EmbeddingStore** (`src/Core/Storage/Hazina.Store.EmbeddingStore/`)
   - Replace `File.*` and `Directory.*` with `IFileSystem`
   - Update all store implementations (FileStore, JsonFileStore)
   - Add DI support to store classes

3. **AgentFactory** (`src/Core/Agents/Hazina.AgentFactory/`)
   - Already has `IFileSystem` - extend usage to all classes
   - Add `IClock` DI for time-dependent operations

### Phase 2: LLM Providers (MEDIUM PRIORITY)
**Impact:** 28 files with DateTime usage.

1. **LLM Providers** (`src/Core/LLMs.Providers/`)
   - Replace `DateTime.UtcNow` with `IClock.UtcNow`
   - Add constructor injection for all providers
   - Update wrapper classes

### Phase 3: AI Modules (LOWER PRIORITY)
**Impact:** 61 files - many are timestamp-only (logging, tracing)

1. **Defer to future PR:**
   - AI.Agents (timestamping for traces, logs)
   - AI.ContextEngineering (enrichment timestamps)
   - Most DateTime usage is for logging/telemetry

## Implementation Plan

### Step 1: DocumentStore DI (15 files estimated)
- [x] Read DocumentStore implementation
- [ ] Add IFileSystem constructor parameter
- [ ] Replace File.* calls
- [ ] Update tests with mock IFileSystem
- [ ] Verify builds

### Step 2: EmbeddingStore DI (25 files estimated)
- [ ] Update EmbeddingFileStore
- [ ] Update EmbeddingJsonFileStore
- [ ] Add IFileSystem to all store constructors
- [ ] Update factory methods

### Step 3: LLM Providers IClock (28 files estimated)
- [ ] Add IClock to OpenAIClientWrapper
- [ ] Add IClock to ClaudeClientWrapper
- [ ] Add IClock to OllamaClientWrapper
- [ ] Update all provider constructors

### Step 4: Tests & Verification
- [ ] Create test utilities (MockFileSystem, FixedClock)
- [ ] Add unit tests demonstrating DI usage
- [ ] Verify backward compatibility (default constructors work)
- [ ] Build verification

## Backward Compatibility Strategy

**CRITICAL:** All changes MUST be backward compatible.

```csharp
// BEFORE
public class DocumentStore
{
    public DocumentStore(string basePath) { ... }
}

// AFTER (backward compatible)
public class DocumentStore
{
    private readonly IFileSystem _fileSystem;

    public DocumentStore(string basePath, IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new PhysicalFileSystem();
    }
}
```

## Success Criteria

✅ **Phase 1 Complete:**
- DocumentStore uses IFileSystem (all file ops abstracted)
- EmbeddingStore uses IFileSystem (all file ops abstracted)
- AgentFactory extends IClock usage
- All existing code continues to work (default constructors)
- Tests pass
- Build succeeds

## Files Changed (Estimated)

**Phase 1:** ~40-50 files
**Lines changed:** ~200-300 lines (mostly constructor signatures + dependency injection)

## Testing Strategy

1. **Unit tests** - Use `FixedClock` for time-dependent tests
2. **Integration tests** - Use `PhysicalFileSystem` (real files)
3. **Mock tests** - Create `InMemoryFileSystem` for fast tests

## CRITICAL BLOCKER DISCOVERED

**Circular Dependency Issue:**
- AgentFactory contains IClock and IFileSystem abstractions
- AgentFactory references DocumentStore and EmbeddingStore
- If Storage packages reference AgentFactory → circular dependency

**Build Error:**
```
error MSB4006: There is a circular dependency in the target dependency graph
AgentFactory → DocumentStore/EmbeddingStore → AgentFactory
```

**Proper Solution (Recommended for Phase 2):**
1. Create new package: `Hazina.Core.Abstractions`
2. Move IClock and IFileSystem to this package
3. Both AgentFactory and Storage packages reference Core.Abstractions
4. No circular dependency

**Phase 1 Approach (Current):**
- Document the analysis and proposed architecture
- Create architectural plan for Core.Abstractions package
- Defer implementation until Core.Abstractions is created
- Focus on other TODO tasks that don't have this dependency issue

## Notes

- **NOT touching apps/** - Only Core modules in this PR
- **NOT touching AI modules yet** - Defer to Phase 3 (separate PR)
- **Focus:** Storage + LLM providers = highest ROI for testability
- **BLOCKER:** Circular dependency requires architectural refactoring first
- **RECOMMENDATION:** Create Hazina.Core.Abstractions package before implementing DI
