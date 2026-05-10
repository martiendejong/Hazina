# Hazina Security & Auth — Best Practices

Hazina ships four packages that together cover secret handling, transport
hardening, and end-user authentication.

| Package                         | Layer                                | Purpose                                                       |
| ------------------------------- | ------------------------------------ | ------------------------------------------------------------- |
| `Hazina.Security.Core`          | Application                          | Secret store, key rotation, encryption helpers, DataProtection |
| `Hazina.Security.AspNetCore`    | ASP.NET Core middleware              | Security headers, hardened defaults, DI wiring                 |
| `Hazina.Auth.Core`              | Authentication contracts             | Provider-agnostic identity models and DTOs                     |
| `Hazina.Auth.Identity`          | ASP.NET Identity + JWT/OAuth         | Concrete identity store (EF Core / SQLite default)             |

## Package signing & supply-chain integrity

All Hazina NuGet packages produced by the publishing workflow are
**deterministic builds with embedded SourceLink and `.snupkg` symbol packages**
published to nuget.org. Verify a downloaded package before using it in a
sensitive pipeline:

```bash
# Confirm the package is signed by the publisher you expect
dotnet nuget verify Hazina.Security.Core.<version>.nupkg

# Pin packages by exact version + lockfile (Directory.Build.props already enables this)
dotnet restore --use-lock-file --locked-mode
```

CI publishing enforces:

- Tag-driven releases (`v*.*.*`) — no untagged pushes.
- `--skip-duplicate` so a re-run cannot silently overwrite a published version.
- NuGet API key stored only in `secrets.NUGET_API_KEY` on the publishing
  workflow; never inlined into the repo.

## Secret handling — `Hazina.Security.Core`

```csharp
using Hazina.Security;

builder.Services.AddHazinaSecurity(o =>
{
    o.MasterKey = builder.Configuration["Hazina:Security:MasterKey"]; // 32-byte base64
    o.RotationDays = 90;
});
```

Rules of thumb:

1. **Never log secrets.** `ISecretRedactor` (in `Hazina.Security.Core`)
   redacts known secret shapes from log scopes; combine with
   `Hazina.Observability.LLMLogs.RedactPromptValues = true` in production.
2. **Rotate keys.** `IKeyRotationService` will re-encrypt at-rest secrets on
   the configured cadence. Ensure database backups are encrypted with a
   *separate* key.
3. **Bind master keys to the deployment**, not the repo. Use a secret manager
   (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, GitHub OIDC, …) and
   inject through `IConfiguration`.

## Hardening ASP.NET Core — `Hazina.Security.AspNetCore`

```csharp
using Hazina.Security.AspNetCore;

builder.Services.AddHazinaAspNetCoreSecurity();

var app = builder.Build();

app.UseHazinaSecurityHeaders();   // HSTS, X-Content-Type-Options, Referrer-Policy, CSP, …
app.UseHazinaAntiforgery();
```

Defaults:

- HSTS with 1-year max-age, includeSubDomains, preload-ready.
- `Strict-Origin-When-Cross-Origin` referrer policy.
- CSP set to `default-src 'self'` — opt in to wider sources explicitly.
- `X-Frame-Options: DENY`.

## Authentication — `Hazina.Auth.Identity`

```csharp
using Hazina.Auth.Identity;

builder.Services.AddHazinaIdentity(o =>
{
    o.SignInKey    = builder.Configuration["Hazina:Auth:JwtSigningKey"]!;
    o.Issuer       = "https://auth.example.com";
    o.Audience     = "my-api";
    o.ExternalProviders.UseGoogle(builder.Configuration.GetSection("Auth:Google"));
    o.ExternalProviders.UseMicrosoft(builder.Configuration.GetSection("Auth:Microsoft"));
});
```

Best practices:

- **Short-lived access tokens (≤ 15 min) with refresh rotation.**
- **Always run behind TLS** — `UseHazinaSecurityHeaders()` enables HSTS, but
  the platform must also reject plain-HTTP traffic at the load balancer.
- **Store user passwords with the default identity hasher.** Do not roll your
  own; the package configures PBKDF2 with current OWASP-recommended iteration
  counts.
- **Lock down EF migrations**: the SQLite default is fine for development and
  small deployments, but production should switch to `UseHazinaIdentity(o =>
  o.UseSqlServer(...))` (or Postgres) and back the database with point-in-time
  restore.

## Threat model checklist

Before shipping a Hazina-based service, confirm:

- [ ] No secrets in `appsettings*.json` committed to the repo.
- [ ] LLM prompts/responses redacted in production logs.
- [ ] CSP, HSTS, frame-options enforced via `UseHazinaSecurityHeaders()`.
- [ ] JWT signing key rotated at least every 90 days.
- [ ] NuGet packages restored with `--locked-mode` in CI.
- [ ] `dotnet nuget verify` runs against signed packages on the build agent.
- [ ] Rate limiting / abuse protection enabled on the auth endpoints.
