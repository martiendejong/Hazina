# ⚠️ DEPRECATED

**This package is deprecated and will be removed in Hazina v3.0**

**Deprecation Date:** 2026-03-19
**Removal Target:** v3.0 (estimated Q3 2026)
**Reason:** Experimental package - functionality merged into Hazina.CodeIntelligence

## Migration Path

Please migrate to `Hazina.CodeIntelligence`:

```csharp
// Before
using Hazina.AI.OpenCode;

// After
using Hazina.CodeIntelligence;
```

## What Happened?

`Hazina.AI.OpenCode` was an experimental package exploring AI-driven code analysis. Its functionality has been:

1. **Refined** and improved
2. **Merged** into `Hazina.CodeIntelligence`
3. **Enhanced** with additional features
4. **Production-hardened** with better error handling

## Feature Migration

| Feature | OpenCode (Old) | CodeIntelligence (New) |
|---------|----------------|------------------------|
| Code parsing | ✅ Basic | ✅ Enhanced |
| Syntax analysis | ✅ | ✅ Improved |
| Code generation | ✅ Experimental | ✅ Production-ready |
| Multi-language | ❌ Limited | ✅ Extensive |
| Error handling | ⚠️ Basic | ✅ Robust |

## Migration Steps

1. Replace package reference:
   ```xml
   <!-- Old -->
   <PackageReference Include="Hazina.AI.OpenCode" Version="*" />

   <!-- New -->
   <PackageReference Include="Hazina.CodeIntelligence" Version="1.0.0" />
   ```

2. Update using statements:
   ```csharp
   using Hazina.AI.OpenCode;      // Remove
   using Hazina.CodeIntelligence; // Add
   ```

3. Review API changes (most APIs preserved, some renamed)

4. Test thoroughly (behavior improvements may affect results)

## API Changes

Most APIs are preserved, with some improvements:

```csharp
// Before (OpenCode)
var result = await analyzer.AnalyzeCode(code);

// After (CodeIntelligence) - Same API!
var result = await analyzer.AnalyzeCode(code);

// New features available:
var enhanced = await analyzer.AnalyzeCodeWithContext(code, context);
```

## Timeline

- **v2.0-2.9:** This package continues to work but shows obsolete warnings
- **v3.0:** This package will be removed from the repository

## Why Merged?

The experimental `OpenCode` package proved successful and was:
- **Integrated** into the main CodeIntelligence package
- **Enhanced** with production features
- **Stabilized** with better testing
- **Renamed** to better reflect its purpose

## Need Help?

- See: [CONSOLIDATION_PLAN.md](../docs/CONSOLIDATION_PLAN.md)
- See: `Hazina.CodeIntelligence` documentation
- GitHub Issues: Tag questions with `migration-help`
