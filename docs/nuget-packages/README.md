# Hazina Packages — Storage, Security, Auth, Observability

This directory documents the NuGet packages produced for the Storage,
Security/Auth, and Observability tracks of the Hazina framework.

| Track                | Package                                  |
| -------------------- | ---------------------------------------- |
| Storage              | [`Hazina.Store.DocumentStore`](./vector-store.md)        |
| Storage              | [`Hazina.Store.EmbeddingStore`](./vector-store.md)       |
| Storage              | [`Hazina.Store.FactsStore`](./vector-store.md)           |
| Storage              | [`Hazina.Store.PgVector`](./vector-store.md)             |
| Storage              | [`Hazina.Store.Sqlite`](./vector-store.md)               |
| Storage              | [`Hazina.Indexing`](./vector-store.md)                   |
| Security             | [`Hazina.Security.Core`](./security-best-practices.md)   |
| Security             | [`Hazina.Security.AspNetCore`](./security-best-practices.md) |
| Auth                 | [`Hazina.Auth.Core`](./security-best-practices.md)       |
| Auth                 | [`Hazina.Auth.Identity`](./security-best-practices.md)   |
| Observability        | [`Hazina.Observability.Core`](./observability.md)        |
| Observability        | [`Hazina.Observability.AspNetCore`](./observability.md)  |
| Observability        | [`Hazina.Observability.LLMLogs`](./observability.md)     |

## Common configuration

All 13 packages inherit shared metadata from `Directory.Build.props` at the
repo root:

- Authors, Company, Copyright, License (`MIT`), RepositoryUrl
- `IncludeSymbols=true`, `SymbolPackageFormat=snupkg`
- SourceLink (`Microsoft.SourceLink.GitHub`) — embedded in Release builds
- Deterministic builds in CI (`ContinuousIntegrationBuild=true`)

Each project sets only its own `<PackageId>`, `<Description>`, and
`<PackageTags>`. Versions roll forward via the `v*.*.*` git tag handled by
`.github/workflows/nuget-publish.yml`.

## Publishing flow

1. Tag a release: `git tag v1.2.3 && git push origin v1.2.3`
2. `nuget-publish.yml` builds the solution, packs every `IsPackable=true`
   project, and pushes both `.nupkg` and `.snupkg` to nuget.org with
   `--skip-duplicate`.
3. The same tag publishes a GitHub Release with the artifacts attached.

To dry-run locally:

```bash
dotnet pack Hazina.sln --configuration Release -p:PackageVersion=0.0.0-local --output ./artifacts
ls ./artifacts/*.nupkg
```
