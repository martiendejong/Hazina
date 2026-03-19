# Hazina Version Upgrade Paths

Complete guide for upgrading between any Hazina versions.

---

## Table of Contents

1. [Overview](#overview)
2. [Version Compatibility Matrix](#version-compatibility-matrix)
3. [Upgrade Paths](#upgrade-paths)
4. [Common Upgrade Scenarios](#common-upgrade-scenarios)
5. [Troubleshooting](#troubleshooting)
6. [Best Practices](#best-practices)

---

## Overview

This document provides upgrade paths for all Hazina versions. Use this guide to:

- **Determine upgrade requirements** for your current version
- **Plan multi-version upgrades** (e.g., v0.9 → v2.0)
- **Understand breaking changes** across versions
- **Minimize downtime** during upgrades

---

## Version Compatibility Matrix

### Framework Compatibility

| Hazina Version | .NET 8.0 | .NET 9.0 | .NET 10.0 | Windows | Linux | macOS |
|----------------|----------|----------|-----------|---------|-------|-------|
| v1.0.1 (current) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| v1.0.0 | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| v0.9.x | ✅ | ❌ | ❌ | ✅ | ✅ | ⚠️ Limited |
| v0.8.x | ✅ | ❌ | ❌ | ✅ | ⚠️ Limited | ❌ |

### Provider Compatibility

| Hazina Version | OpenAI | Anthropic | Ollama | Gemini | Azure OpenAI |
|----------------|--------|-----------|--------|--------|--------------|
| v1.0.1 | ✅ GPT-4o | ✅ Claude 3.5 | ✅ Latest | ✅ 1.5 | ⚠️ Preview |
| v1.0.0 | ✅ GPT-4 | ✅ Claude 3 | ✅ 0.1+ | ❌ | ❌ |
| v0.9.x | ✅ GPT-4 | ✅ Claude 2.1 | ⚠️ Experimental | ❌ | ❌ |
| v0.8.x | ✅ GPT-3.5 | ❌ | ❌ | ❌ | ❌ |

**Legend:**
- ✅ Fully Supported
- ⚠️ Preview/Limited Support
- ❌ Not Supported

---

## Upgrade Paths

### Direct Upgrades (No Intermediate Steps)

These version jumps can be done directly:

```
v1.0.0 → v1.0.1  ✅ No breaking changes
v0.9.x → v1.0.x  ✅ Minor breaking changes (30 min migration)
```

### Multi-Step Upgrades (Intermediate Versions Required)

These version jumps require intermediate steps:

```
v0.8.x → v1.0.x
  Step 1: v0.8.x → v0.9.5
  Step 2: v0.9.5 → v1.0.x

v0.7.x → v1.0.x
  Step 1: v0.7.x → v0.8.5
  Step 2: v0.8.5 → v0.9.5
  Step 3: v0.9.5 → v1.0.x
```

**Why Multi-Step?**
- Cumulative breaking changes too complex for single migration
- API changes build on each other
- Database schema migrations require intermediate versions

---

## Common Upgrade Scenarios

### Scenario 1: v1.0.0 → v1.0.1

**Breaking Changes:** None
**Migration Time:** 5 minutes
**Risk Level:** Very Low

**Steps:**
```bash
# 1. Update packages
dotnet add package Hazina.AI.FluentAPI --version 1.0.1
dotnet add package Hazina.AI.RAG --version 1.0.1
# ... repeat for all packages

# 2. Restore and build
dotnet restore
dotnet build

# 3. Test
dotnet test
```

**Changes:**
- ✅ Bug fixes only
- ✅ Performance improvements
- ✅ Documentation updates
- ❌ No API changes
- ❌ No behavior changes

---

### Scenario 2: v0.9.x → v1.0.x

**Breaking Changes:** Yes (moderate)
**Migration Time:** 30-60 minutes
**Risk Level:** Medium

**Major Changes:**
1. **Package Reorganization**
   - `Hazina.Core` split into `Hazina.AI.FluentAPI` + `Hazina.AI.RAG`
   - New namespace structure

2. **Configuration Changes**
   - Constructor → Object initializer pattern
   - Provider-specific namespaces

3. **Method Renames**
   - `GetTextAsync` → `GetResponse`
   - `StreamTextAsync` → `StreamResponse`

**Migration Steps:**

#### Step 1: Update Package References

```xml
<!-- BEFORE (v0.9.x) -->
<PackageReference Include="Hazina.Core" Version="0.9.5" />
<PackageReference Include="Hazina.Providers" Version="0.9.5" />

<!-- AFTER (v1.0.x) -->
<PackageReference Include="Hazina.AI.FluentAPI" Version="1.0.1" />
<PackageReference Include="Hazina.AI.RAG" Version="1.0.1" />
<PackageReference Include="Hazina.LLMs.OpenAI" Version="1.0.1" />
```

#### Step 2: Update Namespaces

```csharp
// BEFORE (v0.9.x)
using Hazina.Core;
using Hazina.Providers;

// AFTER (v1.0.x)
using Hazina.AI.FluentAPI.Configuration;
using Hazina.LLMs.OpenAI;
```

#### Step 3: Update Configuration

```csharp
// BEFORE (v0.9.x)
var config = new OpenAIConfig("sk-...", "gpt-4");

// AFTER (v1.0.x)
var config = new OpenAIConfig { ApiKey = "sk-...", Model = "gpt-4o" };
```

#### Step 4: Update Method Calls

```csharp
// BEFORE (v0.9.x)
var response = await client.GetTextAsync(prompt);

// AFTER (v1.0.x)
var messages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.User, prompt)
};
var response = await client.GetResponse(messages);
```

**Verification:**
```bash
dotnet build
dotnet test
```

---

### Scenario 3: v0.8.x → v1.0.x (Multi-Step)

**Breaking Changes:** Yes (major)
**Migration Time:** 2-4 hours
**Risk Level:** High

**Recommended Path:**
```
v0.8.x → v0.9.5 → v1.0.1
```

**Why Not Direct?**
- Database schema changed in v0.9.0
- API surface completely redesigned in v0.9.0
- Provider system rewritten in v0.9.0

#### Step 1: v0.8.x → v0.9.5

**Time:** 1-2 hours

1. **Update packages:**
   ```bash
   dotnet add package Hazina.Core --version 0.9.5
   ```

2. **Migrate database:**
   ```bash
   dotnet ef migrations add UpgradeToV09
   dotnet ef database update
   ```

3. **Update API calls:**
   ```csharp
   // BEFORE (v0.8.x)
   var result = await ai.Generate(text);

   // AFTER (v0.9.x)
   var result = await ai.GetTextAsync(text);
   ```

4. **Test thoroughly:**
   ```bash
   dotnet build
   dotnet test
   # Manual testing of critical features
   ```

#### Step 2: v0.9.5 → v1.0.1

Follow [Scenario 2](#scenario-2-v09x--v10x) above.

**Total Time:** 2-4 hours (including testing)

---

### Scenario 4: Upgrading with Custom Providers

**If you built custom LLM providers:**

#### v0.9.x → v1.0.x

**Required Changes:**

1. **Implement New Interface:**
   ```csharp
   // BEFORE (v0.9.x)
   public class CustomProvider : ILLMProvider
   {
       public async Task<string> GenerateAsync(string prompt)
       {
           // Implementation
       }
   }

   // AFTER (v1.0.x)
   public class CustomProvider : LLMProviderBase, ILLMClient
   {
       public async Task<LLMResponse<string>> GetResponse(
           List<HazinaChatMessage> messages,
           HazinaChatResponseFormat format,
           IToolsContext? toolsContext,
           List<ImageData>? images,
           CancellationToken cancel)
       {
           // New implementation
       }
   }
   ```

2. **Add Token Tracking:**
   ```csharp
   // AFTER (v1.0.x) - Required
   return new LLMResponse<string>(
       result: generatedText,
       tokenUsage: new TokenUsage
       {
           PromptTokens = promptTokens,
           CompletionTokens = completionTokens,
           TotalTokens = totalTokens,
           TotalCost = CalculateCost(totalTokens)
       }
   );
   ```

3. **Update Registration:**
   ```csharp
   // BEFORE (v0.9.x)
   orchestrator.RegisterProvider("custom", customProvider);

   // AFTER (v1.0.x)
   orchestrator.RegisterProvider("custom", customProvider, priority: 1);
   ```

---

## Troubleshooting

### Issue 1: Build Errors After Upgrade

**Symptom:**
```
error CS0246: The type or namespace name 'Hazina' could not be found
```

**Causes:**
1. Packages not updated
2. Old packages still referenced
3. NuGet cache issues

**Fix:**
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Remove old packages
dotnet remove package Hazina.Core
dotnet remove package Hazina.Providers

# Add new packages
dotnet add package Hazina.AI.FluentAPI --version 1.0.1
dotnet add package Hazina.AI.RAG --version 1.0.1

# Restore
dotnet restore
dotnet build
```

---

### Issue 2: Runtime Errors After Successful Build

**Symptom:**
```
System.MissingMethodException: Method not found: 'GetTextAsync'
```

**Causes:**
- Mixed versions (some packages updated, others not)
- Stale dependencies in bin/obj

**Fix:**
```bash
# Clean build artifacts
dotnet clean
rm -rf bin obj

# Ensure all Hazina packages are same version
dotnet list package | grep Hazina

# Update any mismatched versions
dotnet add package [MismatchedPackage] --version 1.0.1

# Rebuild
dotnet restore
dotnet build
```

---

### Issue 3: Database Migration Failures

**Symptom:**
```
Unable to create migration: The entity type 'OldEntity' requires a primary key
```

**Causes:**
- Database schema incompatible between versions
- Missing intermediate migrations

**Fix:**

For v0.8.x → v1.0.x:
```bash
# WRONG (will fail):
dotnet ef migrations add UpgradeToV10

# RIGHT (step by step):
# 1. Upgrade to v0.9.5 first
dotnet add package Hazina.Core --version 0.9.5
dotnet ef migrations add UpgradeToV09
dotnet ef database update

# 2. Then upgrade to v1.0.x
dotnet add package Hazina.AI.FluentAPI --version 1.0.1
dotnet ef migrations add UpgradeToV10
dotnet ef database update
```

---

### Issue 4: Configuration Not Loading

**Symptom:**
```
InvalidOperationException: OpenAI:ApiKey not found in configuration
```

**Causes:**
- Configuration path changed between versions
- appsettings.json structure incompatible

**Fix:**

```json
// BEFORE (v0.9.x)
{
  "Hazina": {
    "OpenAI": {
      "Key": "sk-..."
    }
  }
}

// AFTER (v1.0.x)
{
  "OpenAI": {
    "ApiKey": "sk-..."
  }
}
```

Update configuration loading:
```csharp
// v1.0.x
var config = OpenAIConfig.FromConfiguration(configuration);
```

---

## Best Practices

### 1. Version Pinning

**DO:** Pin exact versions in production:
```xml
<PackageReference Include="Hazina.AI.FluentAPI" Version="1.0.1" />
```

**DON'T:** Use wildcards or ranges:
```xml
<PackageReference Include="Hazina.AI.FluentAPI" Version="1.0.*" />  ❌
<PackageReference Include="Hazina.AI.FluentAPI" Version="1.*" />    ❌
```

**Why:** Prevents accidental upgrades that introduce breaking changes.

---

### 2. Staged Rollout

For production systems, upgrade in stages:

1. **Dev Environment** (Day 1)
   - Upgrade and test thoroughly
   - Fix any issues

2. **Staging Environment** (Day 3-5)
   - Deploy to staging
   - Run integration tests
   - Performance testing

3. **Production Canary** (Day 7-10)
   - Deploy to 5% of production traffic
   - Monitor closely for 24-48 hours

4. **Full Production** (Day 10-14)
   - Gradual rollout: 25% → 50% → 100%
   - Monitor at each step

---

### 3. Automated Testing

Before upgrading:

```csharp
[Test]
public async Task VerifyProviderCompatibility()
{
    // Test each provider still works
    var openAi = new OpenAIClientWrapper(openAiConfig);
    var response = await openAi.GetResponse(testMessages);
    Assert.IsNotNull(response.Result);
}

[Test]
public async Task VerifyRAGFunctionality()
{
    // Test RAG still works
    var rag = new RAGEngine(ai, vectorStore);
    var answer = await rag.QueryAsync("Test question");
    Assert.IsNotEmpty(answer.Answer);
}

[Test]
public async Task VerifyCostTracking()
{
    // Test cost tracking still works
    var response = await ai.GetResponse(messages);
    Assert.IsTrue(response.TokenUsage.TotalCost > 0);
}
```

---

### 4. Rollback Plan

Always have a rollback plan:

```bash
# Before upgrading
git checkout -b upgrade-to-v101
git push origin upgrade-to-v101

# If upgrade fails
git checkout main
git branch -D upgrade-to-v101

# Or rollback packages
dotnet add package Hazina.AI.FluentAPI --version 1.0.0  # Previous version
```

---

### 5. Documentation Updates

After upgrading, update:

- [ ] Internal documentation (which version, when upgraded)
- [ ] Deployment scripts (updated package versions)
- [ ] CI/CD pipelines (updated build configurations)
- [ ] Team knowledge base (migration notes)

---

## Version-Specific Guides

For detailed migration instructions:

- **v1.x → v2.x:** See [MIGRATION_GUIDE.md](MIGRATION_GUIDE.md)
- **v0.9.x → v1.x:** See [MIGRATION_GUIDE_V09_TO_V10.md](MIGRATION_GUIDE_V09_TO_V10.md) (TODO)
- **v0.8.x → v0.9.x:** See [MIGRATION_GUIDE_V08_TO_V09.md](MIGRATION_GUIDE_V08_TO_V09.md) (TODO)

---

## Support

If you encounter issues not covered here:

1. **Search Issues:** https://github.com/martiendejong/Hazina/issues?q=is%3Aissue+upgrade
2. **Check Discussions:** https://github.com/martiendejong/Hazina/discussions
3. **Create Issue:** Include:
   - Current version
   - Target version
   - Error message
   - Minimal repro

---

**Last Updated:** 2026-03-19
**Current Stable Version:** 1.0.1
**Recommended Upgrade Path:** Always upgrade to latest stable version
