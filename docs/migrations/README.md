# Migration Guides

Step-by-step guides for upgrading between Hazina versions.

| Guide | From | To | Breaking Changes |
|-------|------|----|-----------------|
| [v1-to-v2.md](v1-to-v2.md) | v1.x | v2.0 | Yes — config, namespaces, method signatures |
| [v2.4-to-v2.5.md](v2.4-to-v2.5.md) | v2.4.x | v2.5.0 | No — additive only |

## Which Guide Do I Need?

Check your current Hazina version:

```bash
dotnet list package | grep Hazina
```

- Version `< 2.0` → follow [v1-to-v2.md](v1-to-v2.md) first, then [v2.4-to-v2.5.md](v2.4-to-v2.5.md)
- Version `2.x < 2.5` → follow [v2.4-to-v2.5.md](v2.4-to-v2.5.md)
- Version `>= 2.5` → no migration needed; review [CHANGELOG](../releases/CHANGELOG.md) for additive features

## Related

- [Full Changelog](../releases/CHANGELOG.md)
- [Release Notes](../releases/RELEASE_NOTES.md)
- [API Changelog](../API_CHANGELOG.md)
