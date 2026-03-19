# Hazina Release Notes v[VERSION]

**Release Date:** [YYYY-MM-DD]
**Release Type:** [Major | Minor | Patch]
**Breaking Changes:** [Yes | No]

---

## Overview

[2-3 sentence summary of this release. What's the main focus? What problem does it solve?]

---

## Highlights

### 🎯 Major Features

#### [Feature Name 1]

**What:** [Brief description]
**Why:** [User benefit]
**How:** [Code example]

```csharp
// Example usage
var example = new FeatureExample();
```

**Documentation:** [Link to guide]

---

#### [Feature Name 2]

**What:** [Brief description]
**Why:** [User benefit]
**How:** [Code example]

```csharp
// Example usage
```

**Documentation:** [Link to guide]

---

### ✨ Improvements

- **[Category]:** [Description] ([#PR_NUMBER](link))
- **[Category]:** [Description] ([#PR_NUMBER](link))
- **[Category]:** [Description] ([#PR_NUMBER](link))

---

### 🐛 Bug Fixes

- **Fixed:** [Description] ([#ISSUE_NUMBER](link))
- **Fixed:** [Description] ([#ISSUE_NUMBER](link))
- **Fixed:** [Description] ([#ISSUE_NUMBER](link))

---

### 📚 Documentation

- **Added:** [Documentation type] ([#PR_NUMBER](link))
- **Updated:** [Documentation type] ([#PR_NUMBER](link))
- **Improved:** [Documentation type] ([#PR_NUMBER](link))

---

## Breaking Changes

[If "Yes" above, detail ALL breaking changes here. If "No", state "None in this release."]

### [Breaking Change 1]: [Short Title]

**What Changed:**
```csharp
// BEFORE (v[OLD_VERSION])
var oldWay = new OldClass(param1, param2);

// AFTER (v[NEW_VERSION])
var newWay = new NewClass { Prop1 = param1, Prop2 = param2 };
```

**Why:** [Reason for breaking change]

**Migration:**
1. [Step 1]
2. [Step 2]
3. [Step 3]

**Estimated Effort:** [X minutes / hours]

---

### [Breaking Change 2]: [Short Title]

[Same structure as above]

---

## Upgrade Guide

### Quick Upgrade (No Breaking Changes)

If this release has NO breaking changes:

```bash
# Update packages
dotnet add package Hazina.AI.FluentAPI --version [VERSION]
dotnet add package Hazina.AI.RAG --version [VERSION]
# ... repeat for all Hazina packages

# Restore and build
dotnet restore
dotnet build
```

---

### Full Migration (With Breaking Changes)

If this release has breaking changes, follow the [Migration Guide](MIGRATION_GUIDE.md).

**Estimated Migration Time:** [X minutes to Y hours]

**Steps:**
1. [Step 1 with link to migration guide section]
2. [Step 2]
3. [Step 3]

---

## New Packages

[List any new NuGet packages introduced in this release]

- **[Package Name]** ([link](https://www.nuget.org/packages/...))
  - **Description:** [What it does]
  - **Installation:** `dotnet add package [Package] --version [VERSION]`
  - **Use Case:** [When to use it]

---

## Deprecations

[List features/APIs marked as deprecated in this release]

- **[API/Feature Name]**
  - **Deprecated In:** v[VERSION]
  - **Removal Planned:** v[FUTURE_VERSION]
  - **Replacement:** [New API/feature]
  - **Migration:** [Link to guide]

---

## Performance Improvements

[If applicable, document performance improvements with benchmarks]

### [Performance Improvement 1]

**Before:**
- Metric 1: [value]
- Metric 2: [value]

**After:**
- Metric 1: [value] ([+/-X%])
- Metric 2: [value] ([+/-X%])

**Impact:** [Description of user-visible impact]

---

## Security Updates

[If applicable, list security fixes. Use CVE numbers if available]

- **[CVE-YYYY-NNNNN]:** [Description] ([Severity: Critical | High | Medium | Low])
  - **Impact:** [What was vulnerable]
  - **Fix:** [How it's fixed]
  - **Action Required:** [What users need to do]

---

## Known Issues

[List any known issues in this release with workarounds]

- **[Issue Description]** ([#ISSUE_NUMBER](link))
  - **Impact:** [Who is affected]
  - **Workaround:** [Temporary solution]
  - **Fix Planned:** [When it will be fixed]

---

## Compatibility

### Supported Platforms

- ✅ .NET 8.0
- ✅ .NET 9.0
- ✅ .NET 10.0
- ✅ Windows 10/11
- ✅ Linux (Ubuntu 22.04+, Debian 11+)
- ✅ macOS 12+

### LLM Provider Compatibility

| Provider | Minimum Version | Maximum Version | Status |
|----------|----------------|-----------------|--------|
| OpenAI | GPT-3.5 | GPT-4o | ✅ Fully Supported |
| Anthropic | Claude 2 | Claude 3.5 Sonnet | ✅ Fully Supported |
| Ollama | 0.1.0 | Latest | ✅ Fully Supported |
| Gemini | 1.0 | Latest | ✅ Fully Supported |
| Azure OpenAI | GPT-3.5 | GPT-4o | ⚠️ Preview |

### Database Compatibility

| Database | Version | Status |
|----------|---------|--------|
| PostgreSQL | 12+ with pgvector | ✅ Fully Supported |
| SQLite | 3.35+ | ✅ Fully Supported |
| SQL Server | 2019+ | ✅ Fully Supported |
| Supabase | Latest | ✅ Fully Supported |

---

## Contributors

This release was made possible by:

- [@username1](link) - [contribution summary]
- [@username2](link) - [contribution summary]
- [@username3](link) - [contribution summary]

**Total Contributors:** [N]
**Commits:** [N]
**Files Changed:** [N]
**Lines Added/Removed:** +[N]/-[N]

---

## Installation

### New Installation

```bash
# Install core packages
dotnet add package Hazina.AI.FluentAPI --version [VERSION]
dotnet add package Hazina.AI.RAG --version [VERSION]
dotnet add package Hazina.AI.Agents --version [VERSION]

# Or use templates
dotnet new install Hazina.Templates::[VERSION]
dotnet new hazina-rag -n MyRAGApp
```

### Upgrade from v[PREVIOUS_VERSION]

```bash
# Update all Hazina packages
dotnet list package | grep Hazina | awk '{print $2}' | xargs -I {} dotnet add package {} --version [VERSION]

# Or manually
dotnet add package Hazina.AI.FluentAPI --version [VERSION]
# ... repeat for each package
```

---

## Verification

After upgrading, verify your installation:

```bash
# Build
dotnet build

# Run tests
dotnet test

# Check version
dotnet list package | grep Hazina
```

**Expected Output:**
```
> Hazina.AI.FluentAPI        [VERSION]
> Hazina.AI.RAG              [VERSION]
> Hazina.AI.Agents           [VERSION]
```

---

## Rollback

If you encounter issues:

### Option 1: Rollback to Previous Version

```bash
# Downgrade to v[PREVIOUS_VERSION]
dotnet add package Hazina.AI.FluentAPI --version [PREVIOUS_VERSION]
dotnet add package Hazina.AI.RAG --version [PREVIOUS_VERSION]
# ... repeat for all packages

dotnet restore
dotnet build
```

### Option 2: Revert Git Branch

```bash
git checkout backup-before-hazina-v[VERSION]
git branch -D main
git checkout -b main
```

---

## Resources

### Documentation

- [Getting Started Guide](../README.md#quick-start)
- [Migration Guide](MIGRATION_GUIDE.md)
- [API Reference](apidoc/index.html)
- [RAG Guide](RAG_GUIDE.md)
- [Agents Guide](AGENTS_GUIDE.md)
- [UpdateStore Safety Policies](UPDATESTORE_SAFETY_POLICIES.md)

### Examples

- [QuickStart Templates](../templates/quickstart/)
- [Sample Projects](../samples/)
- [Integration Tests](../Tests/)

### Community

- [GitHub Repository](https://github.com/martiendejong/Hazina)
- [Issue Tracker](https://github.com/martiendejong/Hazina/issues)
- [Discussions](https://github.com/martiendejong/Hazina/discussions)
- [Contributing Guide](../CONTRIBUTING.md)

---

## Changelog

Full changelog available at: [CHANGELOG.md](../CHANGELOG.md#v[VERSION_ANCHOR])

### All Changes

[Link to GitHub compare: `v[PREVIOUS_VERSION]...v[VERSION]`]

---

## Next Release

Preview of v[NEXT_VERSION] (planned for [DATE]):

- [Planned feature 1]
- [Planned feature 2]
- [Planned feature 3]

Track progress: [GitHub Milestone](link)

---

## Feedback

We'd love to hear your feedback on this release:

- 🐛 **Found a bug?** [Create an issue](https://github.com/martiendejong/Hazina/issues/new?template=bug_report.md)
- 💡 **Have a feature request?** [Start a discussion](https://github.com/martiendejong/Hazina/discussions/new?category=ideas)
- 📣 **Share your experience:** [Twitter/X](https://twitter.com/...) | [LinkedIn](https://linkedin.com/...)

---

**Published:** [YYYY-MM-DD HH:MM UTC]
**Authors:** [Primary author(s)]
**License:** MIT
