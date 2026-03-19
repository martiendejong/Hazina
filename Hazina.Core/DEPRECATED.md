# ⚠️ DEPRECATED

**This package is deprecated and will be removed in Hazina v3.0**

**Deprecation Date:** 2026-03-19
**Removal Target:** v3.0 (estimated Q3 2026)
**Reason:** Legacy package - functionality migrated to modern equivalents

## Migration Path

Please migrate to the appropriate modern packages:

### For Core AI Functionality
```csharp
// Before
using Hazina.Core;

// After
using Hazina.AI.Core;
using Hazina.LLMs.Classes;
```

### Recommended Replacements

| Old (Hazina.Core) | New Package |
|-------------------|-------------|
| Base AI classes | → `Hazina.AI.Core` |
| LLM data models | → `Hazina.LLMs.Classes` |
| Chat functionality | → `Hazina.LLMs.Client` |
| Storage abstractions | → `Hazina.Store.*` packages |

## Timeline

- **v2.0-2.9:** This package continues to work but shows obsolete warnings
- **v3.0:** This package will be removed from the repository

## Why Deprecated?

The original `Hazina.Core` package has been superseded by a more modular architecture:
- Better separation of concerns
- Clearer package boundaries
- Improved maintainability
- Modern .NET multi-targeting (8/9/10)

## Need Help?

- See: [CONSOLIDATION_PLAN.md](../docs/CONSOLIDATION_PLAN.md)
- See: [MIGRATION_GUIDE.md](../docs/MIGRATION_GUIDE.md) (coming soon)
- GitHub Issues: Tag questions with `migration-help`

## Questions?

Contact: Hazina maintainers via GitHub Discussions
