# Migration Guide: v1.x to v2.0

Complete step-by-step guide for migrating from Hazina v1.x to v2.0.

---

## Table of Contents

1. [Overview](#overview)
2. [Breaking Changes Summary](#breaking-changes-summary)
3. [Before You Start](#before-you-start)
4. [Step-by-Step Migration](#step-by-step-migration)
5. [Common Migration Scenarios](#common-migration-scenarios)
6. [Troubleshooting](#troubleshooting)
7. [Rollback Plan](#rollback-plan)

---

## Overview

**Estimated Migration Time:** 30 minutes to 2 hours (depending on codebase size)

**Difficulty:** Medium (mostly find-and-replace with some manual adjustments)

**Risk Level:** Low (breaking changes are well-defined and mechanically fixable)

### What Changed in v2.0

- ✅ **Configuration classes:** Constructor parameters → Object initializers
- ✅ **Namespace reorganization:** Provider classes moved to dedicated namespaces
- ✅ **Method signatures:** Updated method names and parameter orders
- ✅ **New base classes:** HazinaConfigBase, HazinaServiceBase, LLMProviderBase
- ✅ **Code deduplication:** ~750 LOC removed through inheritance

### What Stayed the Same

- ✅ Core API concepts (orchestrator, providers, RAG)
- ✅ Feature functionality (no behavior changes)
- ✅ Database schemas (no migrations needed)
- ✅ Configuration file formats (appsettings.json structure)

---

## Breaking Changes Summary

| Change | Impact | Auto-Fix? | Effort |
|--------|--------|-----------|--------|
| Config constructors | High | ✅ Yes (regex) | 10-30 min |
| Namespaces | Medium | ✅ Yes (using statements) | 5-15 min |
| Method signatures | Medium | ✅ Yes (find-replace) | 5-10 min |
| Base classes | Low | ❌ No (transparent) | 0 min |

**Total Estimated Effort:** 20-55 minutes for typical project

---

## Before You Start

### 1. Check Your Current Version

```bash
dotnet list package | grep Hazina
```

If you see versions < 2.0, you need this migration.

### 2. Backup Your Code

```bash
git checkout -b backup-before-hazina-v2
git push origin backup-before-hazina-v2
```

### 3. Update Dependencies

```bash
# Update all Hazina packages to v2.0
dotnet add package Hazina.AI.FluentAPI --version 2.0.0
dotnet add package Hazina.AI.RAG --version 2.0.0
dotnet add package Hazina.AI.Agents --version 2.0.0
dotnet add package Hazina.AI.Providers --version 2.0.0
# ... repeat for all Hazina packages you use
```

Or update in .csproj:

```xml
<PackageReference Include="Hazina.AI.FluentAPI" Version="2.0.0" />
<PackageReference Include="Hazina.AI.RAG" Version="2.0.0" />
```

### 4. Run Initial Build (Expected to Fail)

```bash
dotnet build
```

Note the compilation errors - we'll fix them step by step.

---

## Step-by-Step Migration

### Step 1: Fix Configuration Classes (15-30 minutes)

#### 1.1 Find All Config Instantiations

```bash
# Search for old constructor patterns
grep -rn "new OpenAIConfig(" .
grep -rn "new AnthropicConfig(" .
grep -rn "new OllamaConfig(" .
grep -rn "new GeminiConfig(" .
```

#### 1.2 Convert to Object Initializers

**Pattern to find:**
```csharp
new OpenAIConfig(apiKey: "sk-...", model: "gpt-4o-mini")
new OpenAIConfig(apiKey, model)
new OpenAIConfig("sk-...", "gpt-4o-mini", endpoint, logPath)
```

**Replace with:**
```csharp
new OpenAIConfig { ApiKey = "sk-...", Model = "gpt-4o-mini" }
new OpenAIConfig { ApiKey = apiKey, Model = model }
new OpenAIConfig { ApiKey = "sk-...", Model = "gpt-4o-mini", Endpoint = endpoint, LogPath = logPath }
```

#### 1.3 Automated Fix with Regex (VS Code / Rider)

**Find (regex):**
```regex
new OpenAIConfig\(([^)]+)\)
```

**Manual Replace (case-by-case):**
- Check each match
- Convert parameters to properties
- Ensure property names are PascalCase (ApiKey, Model, etc.)

#### 1.4 Or Use Simple Constructor (Backwards Compatible)

If you only need ApiKey:

```csharp
// OLD
var config = new OpenAIConfig(apiKey: "sk-...");

// NEW (still works!)
var config = new OpenAIConfig("sk-...");
```

#### 1.5 Verify Build

```bash
dotnet build
```

Fix any remaining config-related errors.

---

### Step 2: Fix Namespaces (5-15 minutes)

#### 2.1 Find Missing Namespaces

Look for errors like:
```
error CS0246: The type or namespace name 'OpenAIConfig' could not be found
```

#### 2.2 Add Provider-Specific Using Statements

**At the top of files using OpenAI:**
```csharp
using Hazina.LLMs;
using Hazina.LLMs.OpenAI;  // ADD THIS
```

**At the top of files using Anthropic:**
```csharp
using Hazina.LLMs;
using Hazina.LLMs.Anthropic;  // ADD THIS
```

**At the top of files using Ollama:**
```csharp
using Hazina.LLMs;
using Hazina.LLMs.Ollama;  // ADD THIS
```

#### 2.3 Complete List of Namespace Changes

| Class | Old Namespace | New Namespace |
|-------|---------------|---------------|
| `OpenAIConfig`, `OpenAIClientWrapper` | `Hazina.LLMs` | `Hazina.LLMs.OpenAI` |
| `AnthropicConfig`, `ClaudeClientWrapper` | `Hazina.LLMs` | `Hazina.LLMs.Anthropic` |
| `OllamaConfig`, `OllamaClientWrapper` | `Hazina.LLMs` | `Hazina.LLMs.Ollama` |
| `GeminiConfig`, `GeminiClientWrapper` | `Hazina.LLMs` | `Hazina.LLMs.Gemini` |
| `MistralConfig` | `Hazina.LLMs` | `Hazina.LLMs.Mistral` |

#### 2.4 Verify Build

```bash
dotnet build
```

All namespace errors should be resolved.

---

### Step 3: Fix Method Signatures (5-10 minutes)

#### 3.1 Update GenerateTextAsync → GenerateAsync

**OLD (v1.x):**
```csharp
await provider.GenerateTextAsync(
    prompt: "Hello",
    model: "gpt-4o-mini",
    temperature: 0.7,
    maxTokens: 1000
);
```

**NEW (v2.0):**
```csharp
await provider.GenerateAsync(
    model: "gpt-4o-mini",        // Model first now
    prompt: "Hello",
    temperature: 0.7,
    maxTokens: 1000,
    cancellationToken: CancellationToken.None  // New parameter
);
```

#### 3.2 Find All Instances

```bash
grep -rn "GenerateTextAsync" .
```

#### 3.3 Automated Replace (Simple Cases)

**Find:**
```
GenerateTextAsync
```

**Replace:**
```
GenerateAsync
```

**Then manually fix parameter order** (model first, add CancellationToken).

#### 3.4 Other Method Changes

Check [API_CHANGELOG.md](API_CHANGELOG.md) for complete list of method signature changes.

---

### Step 4: Final Build & Test (5 minutes)

```bash
# Clean build
dotnet clean
dotnet restore
dotnet build

# Run tests
dotnet test

# If using integration tests with LLMs, verify they still work
```

---

## Common Migration Scenarios

### Scenario 1: Simple OpenAI Setup

**Before (v1.x):**
```csharp
using Hazina.LLMs;
using Hazina.AI.Providers.Core;

var config = new OpenAIConfig(
    apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
    model: "gpt-4o-mini"
);

var client = new OpenAIClientWrapper(config);
var orchestrator = new ProviderOrchestrator();
orchestrator.RegisterProvider("openai", client, priority: 1);
```

**After (v2.0):**
```csharp
using Hazina.LLMs;
using Hazina.LLMs.OpenAI;  // NEW
using Hazina.AI.Providers.Core;

var config = new OpenAIConfig  // Object initializer
{
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
    Model = "gpt-4o-mini"
};

var client = new OpenAIClientWrapper(config);
var orchestrator = new ProviderOrchestrator();
orchestrator.RegisterProvider("openai", client, priority: 1);
```

---

### Scenario 2: QuickSetup (No Changes!)

**Before (v1.x) and After (v2.0) - IDENTICAL:**
```csharp
using Hazina.AI.FluentAPI.Configuration;

var ai = QuickSetup.SetupWithFailover(
    openAIKey: "sk-...",
    anthropicKey: "sk-ant-..."
);

var response = await ai.GetResponse(messages);
```

✅ **No migration needed** - QuickSetup API unchanged!

---

### Scenario 3: Multi-Provider with Configs

**Before (v1.x):**
```csharp
var openaiConfig = new OpenAIConfig("sk-...", "gpt-4o-mini");
var anthropicConfig = new AnthropicConfig("sk-ant-...", "claude-3-5-sonnet");
var ollamaConfig = new OllamaConfig("http://localhost:11434", "llama3:8b");

var openaiClient = new OpenAIClientWrapper(openaiConfig);
var anthropicClient = new ClaudeClientWrapper(anthropicConfig);
var ollamaClient = new OllamaClientWrapper(ollamaConfig);
```

**After (v2.0):**
```csharp
using Hazina.LLMs.OpenAI;      // NEW
using Hazina.LLMs.Anthropic;   // NEW
using Hazina.LLMs.Ollama;      // NEW

var openaiConfig = new OpenAIConfig { ApiKey = "sk-...", Model = "gpt-4o-mini" };
var anthropicConfig = new AnthropicConfig { ApiKey = "sk-ant-...", Model = "claude-3-5-sonnet" };
var ollamaConfig = new OllamaConfig { Endpoint = "http://localhost:11434", Model = "llama3:8b" };

var openaiClient = new OpenAIClientWrapper(openaiConfig);
var anthropicClient = new ClaudeClientWrapper(anthropicConfig);
var ollamaClient = new OllamaClientWrapper(ollamaConfig);
```

---

### Scenario 4: Loading from appsettings.json

**appsettings.json (unchanged):**
```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-4o-mini",
    "LogPath": "logs/openai.log"
  }
}
```

**Before (v1.x) - Manual binding:**
```csharp
var openaiSection = configuration.GetSection("OpenAI");
var config = new OpenAIConfig(
    apiKey: openaiSection["ApiKey"]!,
    model: openaiSection["Model"]!
);
```

**After (v2.0) - Automatic binding:**
```csharp
// Much simpler!
var config = OpenAIConfig.Load();  // Automatic from appsettings.json

// Or from IConfiguration instance
var config = OpenAIConfig.FromConfiguration(configuration);
```

---

### Scenario 5: RAG Applications

**Before (v1.x):**
```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.RAG.Core;

var ai = QuickSetup.SetupOpenAI("sk-...");
var vectorStore = new InMemoryVectorStore();
var rag = new RAGEngine(ai, vectorStore);

await rag.IndexDocumentsAsync(documents);
var response = await rag.QueryAsync("What is Hazina?");
```

**After (v2.0) - IDENTICAL:**
```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.RAG.Core;

var ai = QuickSetup.SetupOpenAI("sk-...");
var vectorStore = new InMemoryVectorStore();
var rag = new RAGEngine(ai, vectorStore);

await rag.IndexDocumentsAsync(documents);
var response = await rag.QueryAsync("What is Hazina?");
```

✅ **No changes needed** - RAG API unchanged!

---

## Troubleshooting

### Issue: "OpenAIConfig does not contain a constructor that takes X arguments"

**Cause:** Using old constructor pattern

**Fix:** Convert to object initializer:
```csharp
// WRONG
var config = new OpenAIConfig(apiKey, model);

// RIGHT
var config = new OpenAIConfig { ApiKey = apiKey, Model = model };
```

---

### Issue: "The type or namespace name 'OpenAIConfig' could not be found"

**Cause:** Missing provider-specific namespace

**Fix:** Add using statement:
```csharp
using Hazina.LLMs.OpenAI;
```

---

### Issue: "GenerateTextAsync does not exist"

**Cause:** Method renamed to GenerateAsync

**Fix:**
```csharp
// OLD
await provider.GenerateTextAsync(prompt, model, temp, maxTokens);

// NEW
await provider.GenerateAsync(model, prompt, temp, maxTokens, CancellationToken.None);
```

---

### Issue: "CS1503: Argument 1: cannot convert from 'string' to 'IConfiguration'"

**Cause:** OpenAIConfig.Load() signature changed

**Fix:**
```csharp
// OLD (v1.x)
var config = new OpenAIConfig(apiKey);

// NEW (v2.0) - Simple constructor still works
var config = new OpenAIConfig(apiKey);

// OR use object initializer
var config = new OpenAIConfig { ApiKey = apiKey };

// OR load from appsettings.json
var config = OpenAIConfig.Load();
```

---

### Issue: Build succeeds but runtime errors

**Cause:** Configuration validation failures

**Fix:** Validate configs:
```csharp
var config = new OpenAIConfig { ApiKey = apiKey };

var errors = config.Validate();
if (errors.Any())
{
    foreach (var error in errors)
        Console.WriteLine($"Config error: {error}");

    throw new InvalidOperationException("Invalid configuration");
}
```

---

## Rollback Plan

If migration causes critical issues:

### Option 1: Revert to Backup Branch

```bash
git checkout backup-before-hazina-v2
git branch -D main
git checkout -b main
```

### Option 2: Downgrade Packages

```bash
# Downgrade to v1.x
dotnet add package Hazina.AI.FluentAPI --version 1.9.9
dotnet add package Hazina.AI.RAG --version 1.9.9
# ... repeat for all packages

dotnet restore
dotnet build
```

### Option 3: Selective Migration

Migrate one module at a time:
1. Start with non-critical services
2. Test thoroughly
3. Proceed to critical services
4. Keep v1.x and v2.0 side-by-side temporarily

---

## Migration Checklist

Use this checklist to track your migration progress:

### Pre-Migration
- [ ] Back up code (git branch)
- [ ] Document current Hazina version
- [ ] Review breaking changes in API_CHANGELOG.md
- [ ] Estimate migration time based on codebase size

### Migration Steps
- [ ] Update all Hazina package versions to 2.0.0
- [ ] Fix configuration class instantiations (constructor → object initializer)
- [ ] Add provider-specific using statements
- [ ] Update method signatures (GenerateTextAsync → GenerateAsync)
- [ ] Update parameter orders where changed
- [ ] Add CancellationToken parameters where required

### Testing
- [ ] Clean build succeeds (`dotnet build`)
- [ ] All unit tests pass (`dotnet test`)
- [ ] Integration tests pass (if applicable)
- [ ] Manual testing of critical features
- [ ] Performance benchmarks (if applicable)

### Post-Migration
- [ ] Update internal documentation
- [ ] Notify team of migration completion
- [ ] Monitor for runtime issues
- [ ] Delete backup branch after stability confirmed

---

## Getting Help

If you encounter issues not covered in this guide:

1. **Check API Changelog:** [API_CHANGELOG.md](API_CHANGELOG.md)
2. **Search Issues:** https://github.com/martiendejong/Hazina/issues
3. **Ask in Discussions:** https://github.com/martiendejong/Hazina/discussions
4. **Create Issue:** Provide code example and error message

---

## Success Stories

Share your migration experience!

**Template:**
```markdown
**Codebase Size:** X files, Y LOC
**Migration Time:** Z minutes
**Issues Encountered:** None / [describe]
**Tips for Others:** [your advice]
```

Post in: https://github.com/martiendejong/Hazina/discussions

---

**Last Updated:** 2026-01-08
**Hazina Version:** 2.0.0
**Estimated Success Rate:** 95%+ (with this guide)
