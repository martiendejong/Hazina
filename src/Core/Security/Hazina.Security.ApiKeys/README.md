# Hazina.Security.ApiKeys

One reusable `X-Api-Key` authentication module for every .NET Jengo app. Wire it with one call pair,
the same pattern as `AddHazinaSecurity`:

```csharp
builder.Services
    .AddHazinaApiKeyAuth(o => o.KeyPrefixBase = "iam")      // options: header, cache TTLs, rate limits, tenant hints...
    .UseStore<MyApiKeyStore>()                              // the app's own key table (IApiKeyStore)
    .UseVault(o => { o.BaseUrl = ...; o.ApiKey = ...; o.ProjectId = ...; });   // raw keys live in Vault

app.UseHazinaApiKeyAuth();      // API-key auth + per-key rate limiting (adds UseRateLimiter())
app.UseAuthorization();

[Authorize(Policy = HazinaApiKeyPolicies.Write)]            // Read | Write | Admin
```

## What it does

| Concern | How |
|---|---|
| Header parsing | `X-Api-Key` (configurable). No header: request passes through untouched (JWT/cookie carry on). Header present but bad: 401 immediately. Several header values: rejected as ambiguous. |
| Key storage | The app database holds only the SHA-256 hash (`ApiKeyHasher`). `IApiKeyManager` mints keys (`{base}_{4 chars}_{256-bit token}`), writes the raw key to Vault, returns it to the caller once. Rotation and revocation update Vault and drop the cache. |
| Principal | `ClaimsPrincipal` in IAM's claim shape: `api_key_id`, `api_key_name`, `api_key_prefix`, `auth_method=api_key`, `tenant_id`, `sub`, `permission*`, plus `api_key_scope` (read/write/admin). |
| Scopes | `admin` implies `write` implies `read`. Enforce with `[Authorize(Policy = HazinaApiKeyPolicies.X)]`. |
| Tenant isolation | Fail closed. A tenant key only reaches its own tenant (route value, `tenantId` query, `X-Tenant-Id` header, or an `ApiKeyTenantResource`); a platform key (no tenant) reaches tenants only with admin scope; an API-key principal with neither a tenant nor the platform marker reaches nothing. From code: `User.CanAccessTenant(id)`. |
| Audit | One `ApiKeyAuditEntry` per API-key request (key prefix, key id, tenant, scope, method, route pattern, final status, timestamp, IP, outcome) to every registered `IApiKeyAuditSink`. Default sink logs to `Hazina.Security.ApiKeys.Audit`. The raw key never appears; for unknown keys a prefix is only reported when the value looks like a key we mint. |
| Rate limiting | A sliding-window limiter per key id, chained into `Microsoft.AspNetCore.RateLimiting`'s `GlobalLimiter` (no hand-rolled limiter). Per-key `RateLimitPerMinute` or `DefaultRequestsPerMinute`. 429 + `Retry-After`. If your app already calls `UseRateLimiter()`, set `AddRateLimiterMiddleware = false`. |

## Hybrid validation (local hash lookup + short-lived cache refreshed from IAM)

`CachingApiKeyLookup` sits in front of whatever `IApiKeyLookup` you register:

* hot path: hash the presented key, answer from the in-memory cache, no network hop;
* a miss or an expired entry (`CachePositiveTtl`, default 2 min) refreshes from the source, so a revocation made
  elsewhere takes effect within minutes, or immediately in-process via `IApiKeyCache.Invalidate`;
* unknown keys are remembered briefly (`CacheNegativeTtl`, 15 s), and concurrent misses cost one source call;
* if the source is down, previously validated keys keep working for `CacheMaxStaleOnError` (15 min); with nothing cached
  the middleware answers 503 and audits `LookupUnavailable` (fail closed).

Key sources: `UseStore<T>()` / `UseLookup<T>()` (the app's table, e.g. IAM itself), `UseStaticKeys(...)` (config keys,
hashed at startup), `UseInMemoryStore()`, or `UseHttpLookup(...)` which asks an authority (IAM
`POST /api/api-keys/introspect`) with only the key's hash.

## Referencing it

`Hazina.Security.ApiKeys` targets `net10.0` and `net9.0`. Apps in this workspace reference it by project
(`$(HazinaRoot)src\Core\Security\Hazina.Security.ApiKeys\...`); the NuGet package is published with the other Hazina packages.
