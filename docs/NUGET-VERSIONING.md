# NuGet Package Versioning and Publishing Guide

This document explains how to manage versions and publish NuGet packages for the DevGPT project.

## Quick Start

### One-Command Publishing

To increment the version and publish all packages in one go:

```bat
increment-and-publish.bat
```

This will:
1. Increment the PATCH version (e.g., 1.0.5 → 1.0.6)
2. Update all .csproj files
3. Build all packages
4. Publish to NuGet

### Version Increment Options

```bat
# Increment patch version (1.0.5 → 1.0.6) - for bug fixes
increment-and-publish.bat patch

# Increment minor version (1.0.5 → 1.1.0) - for new features
increment-and-publish.bat minor

# Increment major version (1.0.5 → 2.0.0) - for breaking changes
increment-and-publish.bat major
```

### Increment Without Publishing

To only update version numbers without publishing:

```bat
increment-and-publish.bat patch --no-publish
```

## Setup

### First Time Setup

1. **Set your NuGet API Key** (one of these methods):

   **Method A - Environment Variable (Recommended):**
   ```bat
   setx NUGET_API_KEY "your-api-key-here"
   ```
   You'll need to restart your terminal after this.

   **Method B - Pass as argument:**
   ```bat
   nuget-publish.bat your-api-key-here
   ```

2. **Verify setup:**
   ```bat
   echo %NUGET_API_KEY%
   ```

## Manual Workflow

If you prefer to do things step by step:

### 1. Update Versions Manually

Edit the version in `updateversions.ps1`:
```powershell
$targetVersion = "1.0.6"
```

Then run:
```bat
powershell -ExecutionPolicy Bypass -File updateversions.ps1
```

### 2. Build Packages

```bat
dotnet clean
dotnet build -c Release
dotnet pack -c Release --no-build
```

### 3. Publish to NuGet

```bat
nuget-publish.bat
```

## Versioning Strategy

This project uses **Synchronized Semantic Versioning** across all packages.

### Semantic Versioning (SemVer)

Version format: `MAJOR.MINOR.PATCH`

- **MAJOR** (1.x.x → 2.x.x): Breaking changes - API changes that break compatibility
- **MINOR** (x.1.x → x.2.x): New features - Backwards compatible additions
- **PATCH** (x.x.1 → x.x.2): Bug fixes - Backwards compatible fixes

### Synchronized Versions

All DevGPT packages share the same version number:
- ✅ Simple to manage
- ✅ Clear which packages belong together
- ✅ Less confusion for consumers
- ✅ Easy dependency management

Example: If you update `DevGPT.Classes`, all packages get version bump (even if unchanged).

### When to Increment

| Change Type | Version Change | Example |
|-------------|----------------|---------|
| Bug fix | PATCH | 1.0.5 → 1.0.6 |
| New feature (compatible) | MINOR | 1.0.5 → 1.1.0 |
| Breaking change | MAJOR | 1.0.5 → 2.0.0 |

## Package List

The following packages are managed:

1. DevGPT.AgentFactory
2. DevGPT.Classes
3. DevGPT.DocumentStore
4. DevGPT.EmbeddingStore
5. DevGPT.Generator
6. DevGPT.Helpers
7. DevGPT.HuggingFace
8. DevGPT.LLMClient
9. DevGPT.LLMClientTools
10. DevGPT.OpenAI

## Git Workflow

After publishing, don't forget to commit and tag:

```bat
git add .
git commit -m "Bump version to 1.0.6"
git tag v1.0.6
git push
git push --tags
```

## Troubleshooting

### "API key not found"
Make sure you've set the `NUGET_API_KEY` environment variable and restarted your terminal.

### "Package not found in bin\Release"
Run `dotnet pack -c Release` first to build the packages.

### "Package already exists"
The publish script uses `--skip-duplicate`, so this should be handled automatically. If you see this, the package is already on NuGet.

### "Failed to publish"
- Check your API key is valid
- Verify you have permissions for the package on nuget.org
- Check your internet connection

## Security Notes

**IMPORTANT**: Never commit the API key to git!

- ✅ Use environment variables
- ✅ Use `.gitignore` to exclude `nuget.bat.old` (which contains the old key)
- ❌ Don't hardcode API keys in scripts
- ❌ Don't commit API keys to version control

## Files

- `increment-and-publish.bat` - Main script for version increment and publishing
- `nuget-publish.bat` - Publishing script (can be used standalone)
- `updateversions.ps1` - PowerShell script that updates .csproj files
- `nuget.bat.old` - Backup of old script (contains API key - don't commit!)

## Examples

### Release a Bug Fix

```bat
increment-and-publish.bat patch
# Commits and tags
git add .
git commit -m "Fix: Resolved null reference in DevGPTGeneratedImage"
git tag v1.0.6
git push && git push --tags
```

### Release New Features

```bat
increment-and-publish.bat minor
# Commits and tags
git add .
git commit -m "Feature: Add support for GPT-4 Turbo"
git tag v1.1.0
git push && git push --tags
```

### Preview Changes Without Publishing

```bat
increment-and-publish.bat patch --no-publish
# Review the packages in bin/Release folders
# If satisfied, publish manually:
nuget-publish.bat
```
