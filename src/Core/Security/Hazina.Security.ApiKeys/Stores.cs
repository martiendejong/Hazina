using System.Collections.Concurrent;
using System.Security.Claims;

namespace Hazina.Security.ApiKeys;

/// <summary>Thread-safe in-memory <see cref="IApiKeyStore"/>: tests, samples and small single-process apps.</summary>
public sealed class InMemoryApiKeyStore : IApiKeyStore
{
    private readonly ConcurrentDictionary<string, ApiKeyRecord> _byId = new(StringComparer.Ordinal);

    public Task<ApiKeyRecord?> FindByHashAsync(string keyHash, CancellationToken ct = default) =>
        Task.FromResult(_byId.Values.FirstOrDefault(k => string.Equals(k.KeyHash, keyHash, StringComparison.Ordinal)));

    public Task<ApiKeyRecord?> FindByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_byId.TryGetValue(id, out var record) ? record : null);

    public Task AddAsync(ApiKeyRecord record, CancellationToken ct = default)
    {
        if (!_byId.TryAdd(record.Id, record))
            throw new InvalidOperationException($"API key '{record.Id}' already exists.");
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceHashAsync(string id, string newKeyHash, string? vaultReference, CancellationToken ct = default)
    {
        if (!_byId.TryGetValue(id, out var existing)) return Task.FromResult(false);
        _byId[id] = Copy(existing, keyHash: newKeyHash, vaultReference: vaultReference ?? existing.VaultReference, isActive: existing.IsActive);
        return Task.FromResult(true);
    }

    public Task<bool> RevokeAsync(string id, CancellationToken ct = default)
    {
        if (!_byId.TryGetValue(id, out var existing)) return Task.FromResult(false);
        _byId[id] = Copy(existing, keyHash: existing.KeyHash, vaultReference: existing.VaultReference, isActive: false);
        return Task.FromResult(true);
    }

    private static ApiKeyRecord Copy(ApiKeyRecord r, string keyHash, string? vaultReference, bool isActive) => new()
    {
        Id = r.Id,
        KeyHash = keyHash,
        KeyPrefix = r.KeyPrefix,
        Name = r.Name,
        Description = r.Description,
        Scope = r.Scope,
        TenantId = r.TenantId,
        UserId = r.UserId,
        Permissions = r.Permissions,
        AllowedIps = r.AllowedIps,
        RateLimitPerMinute = r.RateLimitPerMinute,
        CreatedAtUtc = r.CreatedAtUtc,
        ExpiresAtUtc = r.ExpiresAtUtc,
        IsActive = isActive,
        VaultReference = vaultReference,
        ExtraClaims = r.ExtraClaims,
    };
}

/// <summary>A key defined in configuration or code (small apps without a key table). Hashed at startup; the raw value is not retained.</summary>
public sealed class StaticApiKey
{
    public string? RawKey { get; set; }
    public string Name { get; set; } = "static";
    public ApiKeyScope Scope { get; set; } = ApiKeyScope.Admin;

    /// <summary>Null = platform-wide key.</summary>
    public string? TenantId { get; set; }

    public string? UserId { get; set; }
    public List<string> Permissions { get; set; } = new();
    public List<string> AllowedIps { get; set; } = new();
    public int? RateLimitPerMinute { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>Extra claims (type -> values) added to the principal, e.g. a legacy "role" claim an app still keys off.</summary>
    public Dictionary<string, string[]> Claims { get; set; } = new();
}

/// <summary><see cref="IApiKeyLookup"/> over a fixed set of keys.</summary>
public sealed class ConfigurationApiKeyLookup : IApiKeyLookup
{
    private readonly Dictionary<string, ApiKeyRecord> _byHash = new(StringComparer.Ordinal);

    public ConfigurationApiKeyLookup(IEnumerable<StaticApiKey> keys)
    {
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key.RawKey)) continue; // unset placeholder: key auth stays off for it

            var hash = ApiKeyHasher.Hash(key.RawKey);
            var claims = key.Claims.SelectMany(kv => kv.Value.Select(v => new Claim(kv.Key, v))).ToList();
            _byHash[hash] = new ApiKeyRecord
            {
                Id = $"static:{key.Name}",
                KeyHash = hash,
                // Derived from the hash, never from the raw key.
                KeyPrefix = $"cfg_{hash[..4]}_",
                Name = key.Name,
                Scope = key.Scope,
                TenantId = key.TenantId,
                UserId = key.UserId,
                Permissions = key.Permissions.ToArray(),
                AllowedIps = key.AllowedIps.ToArray(),
                RateLimitPerMinute = key.RateLimitPerMinute,
                ExpiresAtUtc = key.ExpiresAtUtc,
                ExtraClaims = claims,
            };
        }
    }

    public int Count => _byHash.Count;

    public Task<ApiKeyRecord?> FindByHashAsync(string keyHash, CancellationToken ct = default) =>
        Task.FromResult(_byHash.TryGetValue(keyHash, out var record) ? record : null);
}

/// <summary>
/// <see cref="IApiKeySecretVault"/> that keeps raw keys in process memory. For tests and local development
/// only: nothing survives a restart, so it must never back a real environment.
/// </summary>
public sealed class InMemoryApiKeySecretVault : IApiKeySecretVault
{
    private int _next;
    private readonly ConcurrentDictionary<string, ApiKeySecretEntry> _secrets = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ApiKeySecretEntry> Secrets => _secrets;

    public Task<string> StoreAsync(ApiKeySecretEntry entry, CancellationToken ct = default)
    {
        var reference = entry.ExistingReference ?? $"mem-{Interlocked.Increment(ref _next)}";
        _secrets[reference] = entry;
        return Task.FromResult(reference);
    }

    public Task DeleteAsync(string reference, CancellationToken ct = default)
    {
        _secrets.TryRemove(reference, out _);
        return Task.CompletedTask;
    }
}

/// <summary>Registered when no vault is configured: key issuing fails closed with a clear message instead of storing a raw key nowhere.</summary>
public sealed class UnconfiguredApiKeySecretVault : IApiKeySecretVault
{
    private const string Message = "No Vault is configured for API key secrets; refusing to issue or rotate a key whose raw value could not be recovered.";

    public Task<string> StoreAsync(ApiKeySecretEntry entry, CancellationToken ct = default) => throw new InvalidOperationException(Message);

    public Task DeleteAsync(string reference, CancellationToken ct = default) => Task.CompletedTask;
}
