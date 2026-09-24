namespace Hazina.Security.ApiKeys;

public sealed class HazinaApiKeyOptions
{
    /// <summary>Request header carrying the key. Default X-Api-Key.</summary>
    public string HeaderName { get; set; } = ApiKeyDefaults.DefaultHeaderName;

    /// <summary>Requests under these path prefixes skip API-key auth entirely (e.g. "/oauth/", "/.well-known/").</summary>
    public IList<string> ExcludedPathPrefixes { get; } = new List<string>();

    /// <summary>Non-secret prefix base for newly generated keys (1-8 chars a-z0-9), e.g. "iam" gives "iam_a3f9_...".</summary>
    public string KeyPrefixBase { get; set; } = "hzn";

    // ---- tenant isolation -------------------------------------------------------------

    /// <summary>
    /// When true the middleware rejects (403) any request whose tenant header/query parameter names a
    /// tenant the key does not belong to, before the request reaches routing. Route values and resource
    /// checks are enforced by the scope policies (see <see cref="HazinaApiKeyPolicies"/>).
    /// </summary>
    public bool EnforceTenantHints { get; set; } = true;

    public string TenantHeaderName { get; set; } = ApiKeyDefaults.DefaultTenantHeaderName;

    /// <summary>Query-string parameter AND route value names that carry a tenant id.</summary>
    public IList<string> TenantParameterNames { get; } = new List<string> { ApiKeyDefaults.DefaultTenantParameterName };

    // ---- hybrid cache (option C) ------------------------------------------------------

    /// <summary>How long a validated record is served without asking the key source again. Bounds how long a revocation elsewhere takes to propagate.</summary>
    public TimeSpan CachePositiveTtl { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>How long an unknown key is remembered (protects the key source from repeated bad-key lookups).</summary>
    public TimeSpan CacheNegativeTtl { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// If the key source is unreachable, a previously validated record may be served for up to this long
    /// past its TTL, so an IAM outage does not take every app down. Zero disables stale serving.
    /// </summary>
    public TimeSpan CacheMaxStaleOnError { get; set; } = TimeSpan.FromMinutes(15);

    // ---- rate limiting ----------------------------------------------------------------

    /// <summary>Per-key rate limiting through Microsoft.AspNetCore.RateLimiting (a chained GlobalLimiter partitioned by key id).</summary>
    public bool RateLimitingEnabled { get; set; } = true;

    /// <summary>Requests per minute for keys without their own <see cref="ApiKeyRecord.RateLimitPerMinute"/>.</summary>
    public int DefaultRequestsPerMinute { get; set; } = 300;

    /// <summary>
    /// UseHazinaApiKeyAuth() also adds app.UseRateLimiter(), so per-key limits work with that single call.
    /// Set this to false if the app already calls UseRateLimiter() itself (e.g. after UseRouting() for
    /// endpoint policies): calling it twice would count each request twice.
    /// </summary>
    public bool AddRateLimiterMiddleware { get; set; } = true;
}
