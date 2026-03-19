# ⚠️ DEPRECATED

**This package is deprecated and will be removed in Hazina v3.0**

**Deprecation Date:** 2026-03-19
**Removal Target:** v3.0 (estimated Q3 2026)
**Reason:** Niche use case (PDOK-specific) - being archived as example/sample

## Migration Path

This package provided integration with the Dutch PDOK (Publieke Dienstverlening Op de Kaart) geospatial service. Due to its niche use case, it will be:

1. **Archived** as a sample/example project
2. **Moved** to `apps/Demos/Hazina.Demo.PDOK` (already exists)
3. **Documented** as a reference implementation

### If You Use This Package

**Option 1:** Copy code into your own project
- The code will remain available in git history
- Consider it a reference implementation

**Option 2:** Use the demo as reference
- See: `apps/Demos/Hazina.Demo.PDOK`
- Contains updated implementation

**Option 3:** Create your own integration
- Use `Hazina.Tools.Services.Web` for HTTP requests
- Use `Hazina.Tools.Services.DataGathering` for data collection

## Why Deprecated?

This package was too specific for the core Hazina framework:
- **Specific to Netherlands** - Limited international applicability
- **Single-purpose** - PDOK integration only
- **Better as example** - Educational value as reference implementation
- **Maintenance burden** - Niche package requiring specialized knowledge

## Timeline

- **v2.0-2.9:** This package continues to work but shows obsolete warnings
- **v3.0:** This package will be removed from core framework
- **Post-v3.0:** Code remains available in git history and demo project

## Alternative Solutions

For geospatial/mapping functionality:

| Need | Recommended Package |
|------|---------------------|
| HTTP requests | `Hazina.Tools.Services.Web` |
| Data gathering | `Hazina.Tools.Services.DataGathering` |
| General mapping | Third-party: NetTopologySuite, GeoAPI |

## Need Help?

- See: [CONSOLIDATION_PLAN.md](../docs/CONSOLIDATION_PLAN.md)
- Demo project: `apps/Demos/Hazina.Demo.PDOK`
- GitHub Issues: Tag questions with `migration-help`
