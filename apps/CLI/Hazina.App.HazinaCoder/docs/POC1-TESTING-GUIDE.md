# POC 1 Testing Guide - Persistent Learning System

## Status: ✅ Build Successful - Ready for Testing

**Date**: 2026-01-26
**Milestone**: POC 1 implementation complete, all compilation errors resolved

---

## What Was Fixed

### Qdrant API Compatibility (v1.11.0)
- ✅ Fixed `ListCollectionsAsync` returning strings instead of objects
- ✅ Fixed `SetPayloadAsync` / `DeleteAsync` expecting numeric IDs
- ✅ Converted from UUID-based to numeric point IDs
- ✅ Store Guid in payload for proper retrieval mapping

### Null Safety
- ✅ Made `ReflectionMemory` nullable in `AgentIdentity`
- ✅ Added null check in `ReflectOnSessionAsync`
- ✅ Fixed `ReflectionLog` initialization

### Async/Await
- ✅ Made `HandleCommand` async `Task<CommandResult>`
- ✅ Updated call site to use `await`

---

## Prerequisites

### 1. Qdrant Vector Database

**Option A: Docker (Recommended)**
```bash
docker run -d -p 6333:6333 -p 6334:6334 \
  -v C:/Projects/hazina/apps/CLI/Hazina.App.HazinaCoder/data/qdrant:/qdrant/storage \
  qdrant/qdrant
```

**Option B: Manual Installation**
Download from: https://qdrant.tech/documentation/quick-start/

**Verify Running:**
```bash
curl http://localhost:6333/collections
```

### 2. OpenAI API Key

Set environment variable:
```bash
# PowerShell
$env:OPENAI_API_KEY = "sk-..."

# CMD
set OPENAI_API_KEY=sk-...

# Bash
export OPENAI_API_KEY=sk-...
```

Or load from `C:\Projects\client-manager\ClientManagerAPI\appsettings.Secrets.json`:
```json
{
  "OpenAI": {
    "ApiKey": "sk-..."
  }
}
```

---

## Testing Plan

### Phase 1: Basic Storage & Retrieval

**Test 1: Store User Preference**
```bash
cd C:/Projects/hazina/apps/CLI/Hazina.App.HazinaCoder
dotnet run

# In HazinaCoder:
> I prefer async/await over Task.Result
> /exit
```

**Expected:**
- ✅ Experience captured with type `UserPreference`
- ✅ Stored in Qdrant with embedding
- ✅ Console shows "✅ Stored experience: User prefers: async/await over Task.Result"

**Test 2: Retrieve Preference**
```bash
dotnet run

# In HazinaCoder:
> What's my preference for async programming?
```

**Expected:**
- ✅ Finds similar experience from previous session
- ✅ Responds: "💡 Based on your preferences: User prefers: async/await over Task.Result (learned X minutes ago)"
- ✅ Retrieval time < 500ms

---

### Phase 2: Multiple Preference Types

**Test 3: Different Experience Types**

Store multiple preferences:
```
> I prefer TypeScript over JavaScript for frontend
> Always use dependency injection in C# projects
> Never use magic strings - use constants
> I like clean architecture patterns
```

Query:
```
> What are my coding preferences?
```

**Expected:**
- ✅ Top 3-5 most relevant preferences retrieved
- ✅ Ranked by similarity to query
- ✅ Display includes recency ("learned X days ago")

---

### Phase 3: Cross-Session Persistence

**Test 4: Session Continuity**

1. Run HazinaCoder, state preferences, exit
2. Restart HazinaCoder
3. Query preferences without re-stating them

**Expected:**
- ✅ 95% retention across sessions (19/20 preferences recalled)
- ✅ No loss of data after restart
- ✅ Timestamps preserved

---

### Phase 4: Similarity Search Quality

**Test 5: Semantic Matching**

Store:
```
> I prefer LINQ over foreach loops
> I like immutable data structures
```

Query variations:
```
> What's my preference for C# iteration?     → Should find LINQ preference
> How do I feel about mutable objects?       → Should find immutable preference
> What looping style do I prefer?            → Should find LINQ preference
```

**Expected:**
- ✅ 80% top-5 accuracy (correct preference in top 5 results)
- ✅ Cosine similarity > 0.7 threshold
- ✅ Semantic understanding (not just keyword matching)

---

## Success Criteria

| Metric | Target | Status |
|--------|--------|--------|
| Build Status | ✅ No errors | ✅ **PASSED** |
| Preference Retention | 95% across sessions | ⏳ Pending test |
| Retrieval Latency | < 500ms | ⏳ Pending test |
| Similarity Accuracy | 80% top-5 | ⏳ Pending test |
| Cross-Session Persistence | 100% | ⏳ Pending test |

---

## Troubleshooting

### Issue: "Could not initialize Qdrant collection"
- **Cause**: Qdrant not running on localhost:6333
- **Fix**: Start Qdrant with Docker or check connection

### Issue: "Could not generate embedding"
- **Cause**: Missing or invalid OpenAI API key
- **Fix**: Set `OPENAI_API_KEY` environment variable

### Issue: "System.Net.Http.HttpRequestException"
- **Cause**: Qdrant connection refused
- **Fix**: Verify Qdrant is running: `curl http://localhost:6333/collections`

### Issue: "Argument 3: cannot convert from PointId[]"
- **Cause**: Qdrant API version mismatch
- **Fix**: Already resolved - using numeric IDs now

---

## Next Steps After Testing

Once POC 1 passes all tests:

1. **POC 2**: Automatic capture of code patterns from successful solutions
2. **POC 3**: Error resolution memory (learn from debugging sessions)
3. **POC 4**: Project context awareness (architectural decisions)
4. **POC 5**: User insight detection (communication style, expertise level)

---

## Files Modified

| File | Changes |
|------|---------|
| `ExperienceStorage.cs` | Qdrant API fixes, numeric IDs, Guid in payload |
| `ExperienceRetrieval.cs` | Retrieve Guid from payload |
| `AgentIdentity.cs` | Nullable ReflectionMemory, null checks |
| `Program.cs` | Async HandleCommand |

---

## Architecture Summary

```
User: "I prefer X"
    ↓
ExperienceCapture (regex detection)
    ↓
ExperienceStorage (generate embedding via OpenAI)
    ↓
Qdrant Vector DB (store with metadata)
    ↓
[Restart Session]
    ↓
User: "What's my preference for Y?"
    ↓
ExperienceRetrieval (generate query embedding)
    ↓
Qdrant (similarity search, cosine distance)
    ↓
MapToExperience (reconstruct from payload)
    ↓
Response: "💡 Based on your preferences: ..."
```

---

**Last Updated**: 2026-01-26 08:30
**Author**: Claude Sonnet 4.5 (HazinaCoder v2.0 Development)
**Commit**: 415bc13
