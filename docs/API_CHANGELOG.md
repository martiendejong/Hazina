# Hazina API Changelog

Complete record of API changes, breaking changes, and new features across Hazina versions.

---

## v2.0 (Current - develop branch)

**Release Date:** TBD
**Status:** In Development

### 🚨 Breaking Changes

#### 1. Configuration Classes - HazinaConfigBase Refactor (PR #6)

**What Changed:**
All provider configuration classes now inherit from `HazinaConfigBase` abstract base class. Constructor patterns changed to object initializers.

**OLD (v1.x):**
```csharp
// ❌ No longer supported
var config = new OpenAIConfig(
    apiKey: "sk-...",
    model: "gpt-4o-mini",
    endpoint: "https://api.openai.com/v1",
    logPath: "logs/openai.log"
);
```

**NEW (v2.0):**
```csharp
// ✅ Object initializer (recommended)
var config = new OpenAIConfig
{
    ApiKey = "sk-...",
    Model = "gpt-4o-mini",
    Endpoint = "https://api.openai.com/v1",
    LogPath = "logs/openai.log"
};

// ✅ Or simple constructor
var config = new OpenAIConfig("sk-...");
config.Model = "gpt-4o-mini";

// ✅ Or load from appsettings.json
var config = OpenAIConfig.Load();
```

**Benefits:**
- ~400 LOC reduction through shared base class
- Consistent configuration loading across all providers
- Built-in validation via `Validate()` method
- Configuration binding from appsettings.json

**Affected Classes:**
- `OpenAIConfig`
- `AnthropicConfig`
- `OllamaConfig`
- `GeminiConfig`
- `MistralConfig`
- `HuggingFaceConfig`
- All other provider configs

**Migration Path:**
Replace constructor calls with object initializers or use `Config.Load()` for appsettings.json binding.

---

#### 2. Namespace Reorganization (PR #6)

**What Changed:**
Provider-specific classes moved from general `Hazina.LLMs` namespace to provider-specific namespaces.

**Examples:**

| Old Namespace | New Namespace | Classes |
|---------------|---------------|---------|
| `Hazina.LLMs` | `Hazina.LLMs.OpenAI` | `OpenAIConfig`, `OpenAIClientWrapper` |
| `Hazina.LLMs` | `Hazina.LLMs.Anthropic` | `AnthropicConfig`, `ClaudeClientWrapper` |
| `Hazina.LLMs` | `Hazina.LLMs.Ollama` | `OllamaConfig`, `OllamaClientWrapper` |

**Migration Path:**
Add provider-specific using statements:

```csharp
// OLD
using Hazina.LLMs;

// NEW
using Hazina.LLMs;
using Hazina.LLMs.OpenAI;      // For OpenAI classes
using Hazina.LLMs.Anthropic;   // For Anthropic classes
using Hazina.LLMs.Ollama;      // For Ollama classes
```

---

#### 3. Method Signature Changes (Multiple PRs)

**GenerateTextAsync → GenerateAsync**

**OLD:**
```csharp
await provider.GenerateTextAsync(
    prompt: "Hello",
    model: "gpt-4o-mini",
    temperature: 0.7,
    maxTokens: 1000
);
```

**NEW:**
```csharp
await provider.GenerateAsync(
    model: "gpt-4o-mini",        // Parameter order changed
    prompt: "Hello",
    temperature: 0.7,
    maxTokens: 1000,
    cancellationToken: CancellationToken.None  // New required parameter
);
```

**Changes:**
- Method renamed
- Parameter order changed (model first)
- Added `CancellationToken` parameter

---

### ✨ New Features

#### 1. Context Compression Module (PR #8)

Reduces LLM request token counts by up to 87% while preserving important context.

```csharp
using Hazina.AI.ContextCompression;

var compressor = new ContextCompressionModule();
var compressed = await compressor.CompressAsync(largeContext, options: new CompressionOptions
{
    TargetReduction = 0.7,  // 70% reduction
    PreserveKeywords = true
});
```

**See:** [CONTEXT_COMPRESSION.md](CONTEXT_COMPRESSION.md)

---

#### 2. Google Drive Integration (PR #7)

Store and retrieve documents directly from Google Drive.

```csharp
using Hazina.Storage.GoogleDrive;

var credentials = GoogleDriveCredentials.FromJson("credentials.json");
var store = new GoogleDriveDocumentStore(credentials);

await store.SaveAsync(document, folderId: "your-folder-id");
var doc = await store.LoadAsync(documentId);
```

**See:** [GOOGLE_DRIVE_INTEGRATION.md](GOOGLE_DRIVE_INTEGRATION.md)

---

#### 3. 3-Layer Tool Agent Architecture (PR #1)

Token-optimized architecture for tool orchestration.

```csharp
using Hazina.Tools.Services.ToolAgent;

// Layer 1: Chat agent (minimal context)
// Layer 2: Tool agent (orchestration - FREE with Ollama)
// Layer 3: Generation services (full context)

var toolAgent = new ToolAgentService(clientFactory, logger);
var result = await toolAgent.ExecuteAsync(new ToolAgentRequest
{
    Action = "generate_brand_profile",
    ContextHint = "user-123",
    Wait = false  // Async execution
});
```

**Benefits:**
- 87% token cost reduction
- Free orchestration with Ollama
- Minimal context in chat layer

**See:** [TOOL_AGENT_ARCHITECTURE.md](TOOL_AGENT_ARCHITECTURE.md)

---

#### 4. API Compatibility Properties (PR #10)

Added properties to support cross-repo integration (Hazina ↔ client-manager).

**BrandDocumentFragment:**
```csharp
public class FragmentMetadata
{
    // New properties
    public bool NeedsRegeneration { get; set; }
    public string RegenerationReason { get; set; }
}
```

---

#### 5. Clean Code Architecture (PR #5)

"30-Second Comprehension Architecture" - Phase 2 complete.

**Features:**
- Architectural tests (D34)
- ILogger standardization (C30)
- TestData patterns (D33)
- Visual architecture maps

**See:**
- `docs/CLEAN_CODE_*.md` files
- `docs/ARCHITECTURE.md`

---

#### 6. Code Deduplication (PR #6)

**HazinaServiceBase:**
Base class for all service implementations (~200 LOC reduction).

**LLMProviderBase:**
Base class for all LLM provider wrappers (~150 LOC reduction).

**HazinaConfigBase:**
Base class for all configuration classes (~400 LOC reduction).

**Total:** ~750 LOC eliminated through inheritance.

---

### 🐛 Bug Fixes

#### PR #4 - Test Results Publishing
Fixed test results publishing for fork PRs (GitHub Actions permissions).

#### PR #3 - Solution Structure
Added missing projects to solution files and fixed PromptManagement build errors.

#### PR #2 - BrandDocument Regeneration
Added regeneration metadata fields to track document update requirements.

---

### 📝 Documentation Changes

- Added comprehensive guides for new features
- Updated configuration examples with v2.0 patterns
- Added migration guides for breaking changes
- Improved API reference documentation

---

## v1.x (Legacy)

**Status:** Legacy - No longer maintained
**Upgrade Path:** See migration guides above

### Key Differences from v2.0

| Aspect | v1.x | v2.0 |
|--------|------|------|
| Config Pattern | Constructor params | Object initializers |
| Config Base | No base class | HazinaConfigBase |
| Namespaces | Monolithic | Provider-specific |
| Method Names | GenerateTextAsync | GenerateAsync |
| Code Deduplication | None | ~750 LOC reduced |

---

## Migration Checklists

### From v1.x to v2.0

#### Step 1: Update Dependencies
```bash
dotnet add package Hazina.AI.FluentAPI --version 2.0.0
dotnet add package Hazina.AI.RAG --version 2.0.0
# Update all Hazina packages to 2.0.0
```

#### Step 2: Fix Configuration Classes
Search for config constructor calls:
```bash
# Find all instances
grep -r "new OpenAIConfig(" .
grep -r "new AnthropicConfig(" .
```

Replace with object initializers (see examples above).

#### Step 3: Fix Namespaces
Add provider-specific using statements:
```csharp
using Hazina.LLMs.OpenAI;
using Hazina.LLMs.Anthropic;
```

#### Step 4: Update Method Calls
Search for old method names:
```bash
grep -r "GenerateTextAsync" .
```

Update to `GenerateAsync` with new signature.

#### Step 5: Test
```bash
dotnet test
```

Fix any remaining compilation errors.

---

## Support

- **Issues:** https://github.com/martiendejong/Hazina/issues
- **Discussions:** https://github.com/martiendejong/Hazina/discussions
- **Documentation:** https://github.com/martiendejong/Hazina/tree/main/docs

---

**Last Updated:** 2026-01-08
**Maintained By:** Hazina Core Team
