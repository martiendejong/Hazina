# Migration Guide: v1.x → v2.0

> This guide supersedes the earlier `docs/MIGRATION_GUIDE.md`. That file covers the same v1→v2 migration; this version is structured for the canonical `docs/migrations/` location and updated for completeness.

**Estimated time:** 20–55 minutes depending on codebase size
**Difficulty:** Medium — mostly find-and-replace with a few manual adjustments
**Risk:** Low — breaking changes are well-defined and mechanically fixable

---

## What Changed

| Area | v1.x | v2.0 | Auto-Fixable |
|------|------|------|--------------|
| Config constructors | Positional params | Object initializers | Regex / manual |
| Namespaces | `Hazina.LLMs` (monolithic) | Provider-specific | Add `using` statements |
| Method name | `GenerateTextAsync` | `GenerateAsync` | Find + replace |
| Parameter order | `prompt, model, ...` | `model, prompt, ...` | Manual review |
| CancellationToken | Not required | Required on `GenerateAsync` | Add `CancellationToken.None` |
| Base classes | None | `HazinaConfigBase`, `HazinaServiceBase`, `LLMProviderBase` | Transparent (no action needed) |

### What Did NOT Change

- Core API concepts (orchestrator, providers, RAG)
- All feature behavior
- Database schemas (no EF migrations needed)
- `appsettings.json` structure
- `QuickSetup` API
- RAG `IndexDocumentsAsync` / `QueryAsync` signatures

---

## Step 1 — Backup

```bash
git checkout -b backup/before-hazina-v2
git push origin backup/before-hazina-v2
```

---

## Step 2 — Update Package Versions

```bash
dotnet add package Hazina.AI.FluentAPI --version 2.0.0
dotnet add package Hazina.AI.RAG --version 2.0.0
dotnet add package Hazina.AI.Agents --version 2.0.0
dotnet add package Hazina.AI.Providers --version 2.0.0
```

Or update `.csproj` directly:

```xml
<PackageReference Include="Hazina.AI.FluentAPI" Version="2.0.0" />
```

Run `dotnet build` — expect compile errors. Fix them in the steps below.

---

## Step 3 — Fix Configuration Classes

### 3.1 Find Old Patterns

```bash
grep -rn "new OpenAIConfig(" .
grep -rn "new AnthropicConfig(" .
grep -rn "new OllamaConfig(" .
grep -rn "new GeminiConfig(" .
grep -rn "new MistralConfig(" .
```

### 3.2 Convert to Object Initializers

**Before:**
```csharp
var config = new OpenAIConfig(
    apiKey: "sk-...",
    model: "gpt-4o-mini",
    endpoint: "https://api.openai.com/v1",
    logPath: "logs/openai.log"
);
```

**After:**
```csharp
var config = new OpenAIConfig
{
    ApiKey = "sk-...",
    Model = "gpt-4o-mini",
    Endpoint = "https://api.openai.com/v1",
    LogPath = "logs/openai.log"
};
```

### 3.3 Shortcut — Load from appsettings.json

If your `appsettings.json` already has provider sections, use automatic binding:

```csharp
var config = OpenAIConfig.Load();                        // Reads "OpenAI" section
var config = OpenAIConfig.FromConfiguration(configuration); // Explicit IConfiguration
```

`appsettings.json` format is unchanged:
```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-4o-mini"
  }
}
```

---

## Step 4 — Fix Namespaces

### Namespace Mapping

| Class | Old Namespace | New Namespace |
|-------|---------------|---------------|
| `OpenAIConfig`, `OpenAIClientWrapper` | `Hazina.LLMs` | `Hazina.LLMs.OpenAI` |
| `AnthropicConfig`, `ClaudeClientWrapper` | `Hazina.LLMs` | `Hazina.LLMs.Anthropic` |
| `OllamaConfig`, `OllamaClientWrapper` | `Hazina.LLMs` | `Hazina.LLMs.Ollama` |
| `GeminiConfig`, `GeminiClientWrapper` | `Hazina.LLMs` | `Hazina.LLMs.Gemini` |
| `MistralConfig` | `Hazina.LLMs` | `Hazina.LLMs.Mistral` |
| `HuggingFaceConfig`, `HuggingFaceClientWrapper` | `Hazina.LLMs` | `Hazina.LLMs.HuggingFace` |

### Fix

Add the provider-specific `using` at the top of each affected file:

```csharp
using Hazina.LLMs;
using Hazina.LLMs.OpenAI;      // add
using Hazina.LLMs.Anthropic;   // add
```

---

## Step 5 — Fix Method Signatures

### 5.1 Rename GenerateTextAsync → GenerateAsync

```bash
# Find all usages
grep -rn "GenerateTextAsync" .
```

Simple rename first — then fix parameter order manually.

### 5.2 Update Parameter Order

**Before:**
```csharp
await provider.GenerateTextAsync(
    prompt: "Hello",
    model: "gpt-4o-mini",
    temperature: 0.7,
    maxTokens: 1000
);
```

**After:**
```csharp
await provider.GenerateAsync(
    model: "gpt-4o-mini",               // model is now first
    prompt: "Hello",
    temperature: 0.7,
    maxTokens: 1000,
    cancellationToken: CancellationToken.None   // new required param
);
```

---

## Step 6 — Build and Test

```bash
dotnet clean
dotnet restore
dotnet build
dotnet test
```

All errors at this point should be straightforward residual issues from the steps above.

---

## Common Errors and Fixes

### "does not contain a constructor that takes X arguments"

Config class constructor signature changed.

```csharp
// Wrong
var config = new OpenAIConfig(apiKey, model);

// Right
var config = new OpenAIConfig { ApiKey = apiKey, Model = model };
```

### "The type or namespace name 'OpenAIConfig' could not be found"

Missing provider namespace.

```csharp
using Hazina.LLMs.OpenAI;
```

### "GenerateTextAsync does not exist"

Method renamed.

```csharp
// Wrong
await provider.GenerateTextAsync(prompt, model, temp, maxTokens);

// Right
await provider.GenerateAsync(model, prompt, temp, maxTokens, CancellationToken.None);
```

### Build succeeds but runtime config errors

Use the built-in validator:

```csharp
var config = new OpenAIConfig { ApiKey = apiKey };
var errors = config.Validate();
if (errors.Any())
    throw new InvalidOperationException(string.Join(", ", errors));
```

---

## Rollback

If migration causes critical issues, revert to the backup branch:

```bash
git checkout backup/before-hazina-v2
```

Or downgrade packages:

```bash
dotnet add package Hazina.AI.FluentAPI --version 1.9.9
dotnet restore
```

---

## Migration Checklist

### Pre-Migration
- [ ] Create backup branch
- [ ] Document current Hazina package versions
- [ ] Read breaking changes in [API_CHANGELOG.md](../API_CHANGELOG.md)

### Migration
- [ ] Update all Hazina packages to 2.0.0
- [ ] Fix config class instantiations (constructor → object initializer)
- [ ] Add provider-specific `using` statements
- [ ] Rename `GenerateTextAsync` → `GenerateAsync`
- [ ] Fix parameter order (`model` first, add `CancellationToken`)

### Verification
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes
- [ ] Integration/E2E tests pass
- [ ] Production smoke test

---

## Related

- [API Changelog](../API_CHANGELOG.md)
- [Release Notes](../releases/RELEASE_NOTES.md)
- [CHANGELOG](../releases/CHANGELOG.md)
