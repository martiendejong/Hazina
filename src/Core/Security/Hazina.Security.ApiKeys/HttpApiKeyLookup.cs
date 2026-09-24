using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

namespace Hazina.Security.ApiKeys;

/// <summary>
/// Wire contract of the key-introspection endpoint an authority (IAM: <c>POST /api/api-keys/introspect</c>)
/// exposes to other apps. The request carries only the SHA-256 HASH of the presented key, so the
/// authority - and anything on the wire - never sees a raw key.
/// </summary>
public sealed class ApiKeyIntrospectionRequest
{
    public string KeyHash { get; set; } = string.Empty;
}

public sealed class ApiKeyIntrospection
{
    public string KeyId { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Scope { get; set; } = ApiKeyScopes.Read;
    public string? TenantId { get; set; }
    public string? UserId { get; set; }
    public List<string> Permissions { get; set; } = new();
    public List<string> AllowedIps { get; set; } = new();
    public int? RateLimitPerMinute { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public bool Active { get; set; } = true;
    public Dictionary<string, string[]> Claims { get; set; } = new();

    public static ApiKeyIntrospection FromRecord(ApiKeyRecord r) => new()
    {
        KeyId = r.Id,
        KeyPrefix = r.KeyPrefix,
        Name = r.Name,
        Scope = r.Scope.ToClaimValue(),
        TenantId = r.TenantId,
        UserId = r.UserId,
        Permissions = r.Permissions.ToList(),
        AllowedIps = r.AllowedIps.ToList(),
        RateLimitPerMinute = r.RateLimitPerMinute,
        ExpiresAtUtc = r.ExpiresAtUtc,
        Active = r.IsActive,
        Claims = r.ExtraClaims.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.Select(c => c.Value).ToArray()),
    };

    public ApiKeyRecord ToRecord(string keyHash) => new()
    {
        Id = KeyId,
        KeyHash = keyHash,
        KeyPrefix = KeyPrefix,
        Name = Name,
        // An unknown scope from the authority must never widen access: fall back to the lowest.
        Scope = ApiKeyScopes.TryParse(Scope, out var scope) ? scope : ApiKeyScope.Read,
        TenantId = TenantId,
        UserId = UserId,
        Permissions = Permissions,
        AllowedIps = AllowedIps,
        RateLimitPerMinute = RateLimitPerMinute,
        ExpiresAtUtc = ExpiresAtUtc,
        IsActive = Active,
        ExtraClaims = Claims.SelectMany(kv => kv.Value.Select(v => new Claim(kv.Key, v))).ToList(),
    };
}

public sealed class HttpApiKeyLookupOptions
{
    /// <summary>Base address of the authority, e.g. https://iam.example.com/.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string Path { get; set; } = "api/api-keys/introspect";

    /// <summary>The calling app's own API key (needs admin scope, platform-wide, on the authority).</summary>
    public string ServiceApiKey { get; set; } = string.Empty;

    public string HeaderName { get; set; } = ApiKeyDefaults.DefaultHeaderName;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// <see cref="IApiKeyLookup"/> that asks an authority (IAM) about a key hash. Sits BEHIND
/// <see cref="CachingApiKeyLookup"/>: only cache misses / expired entries reach the network.
/// 404 = unknown key; any other failure throws so the cache can serve a stale record.
/// </summary>
public sealed class HttpApiKeyLookup : IApiKeyLookup
{
    public const string HttpClientName = "Hazina.Security.ApiKeys.Introspection";

    private readonly IHttpClientFactory _httpClients;
    private readonly Microsoft.Extensions.Options.IOptions<HttpApiKeyLookupOptions> _options;

    public HttpApiKeyLookup(IHttpClientFactory httpClients, Microsoft.Extensions.Options.IOptions<HttpApiKeyLookupOptions> options)
    {
        _httpClients = httpClients;
        _options = options;
    }

    public async Task<ApiKeyRecord?> FindByHashAsync(string keyHash, CancellationToken ct = default)
    {
        var client = _httpClients.CreateClient(HttpClientName);
        using var response = await client.PostAsJsonAsync(
            _options.Value.Path, new ApiKeyIntrospectionRequest { KeyHash = keyHash }, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiKeyIntrospection>(cancellationToken: ct).ConfigureAwait(false);
        return body?.ToRecord(keyHash);
    }
}
