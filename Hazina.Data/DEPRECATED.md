# ⚠️ DEPRECATED

**This package is deprecated and will be removed in Hazina v3.0**

**Deprecation Date:** 2026-03-19
**Removal Target:** v3.0 (estimated Q3 2026)
**Reason:** Legacy package - functionality migrated to Hazina.Tools.Data

## Migration Path

Please migrate to `Hazina.Tools.Data`:

```csharp
// Before
using Hazina.Data;

// After
using Hazina.Tools.Data;
```

## API Compatibility

`Hazina.Tools.Data` provides a superset of the functionality in `Hazina.Data`:

| Feature | Hazina.Data | Hazina.Tools.Data |
|---------|-------------|-------------------|
| Base repository patterns | ✅ | ✅ (enhanced) |
| Entity framework helpers | ✅ | ✅ (enhanced) |
| Query builders | ✅ | ✅ (enhanced) |
| Modern .NET support | ❌ | ✅ (.NET 8/9/10) |

## Migration Steps

1. Replace package reference:
   ```xml
   <!-- Old -->
   <PackageReference Include="Hazina.Data" Version="*" />

   <!-- New -->
   <PackageReference Include="Hazina.Tools.Data" Version="1.0.0" />
   ```

2. Update using statements:
   ```csharp
   using Hazina.Data;        // Remove
   using Hazina.Tools.Data;  // Add
   ```

3. Rebuild and test

## Timeline

- **v2.0-2.9:** This package continues to work but shows obsolete warnings
- **v3.0:** This package will be removed from the repository

## Why Deprecated?

The `Hazina.Data` package was part of the original monolithic architecture. The new `Hazina.Tools.Data` package provides:
- Better modularity
- Modern .NET multi-targeting
- Enhanced functionality
- Active maintenance

## Need Help?

- See: [CONSOLIDATION_PLAN.md](../docs/CONSOLIDATION_PLAN.md)
- See: [MIGRATION_GUIDE.md](../docs/MIGRATION_GUIDE.md) (coming soon)
- GitHub Issues: Tag questions with `migration-help`
