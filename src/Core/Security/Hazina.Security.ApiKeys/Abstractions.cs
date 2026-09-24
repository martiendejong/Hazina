namespace Hazina.Security.ApiKeys;

/// <summary>
/// Read side of the key source: resolve a key by the SHA-256 hash of the presented raw key.
/// Implemented by the app's own table, by <see cref="ConfigurationApiKeyLookup"/> (static keys), or
/// by <see cref="HttpApiKeyLookup"/> (introspection against IAM). The middleware always wraps the
/// registered lookup in <see cref="CachingApiKeyLookup"/>.
/// </summary>
public interface IApiKeyLookup
{
    /// <returns>The record, or null when no such key exists. Must NOT filter on revoked/expired: the validator decides.</returns>
    Task<ApiKeyRecord?> FindByHashAsync(string keyHash, CancellationToken ct = default);
}

/// <summary>Write side, needed only by apps that issue keys (via <see cref="IApiKeyManager"/>).</summary>
public interface IApiKeyStore : IApiKeyLookup
{
    Task<ApiKeyRecord?> FindByIdAsync(string id, CancellationToken ct = default);

    /// <summary>Persist a new key. The record carries the HASH only.</summary>
    Task AddAsync(ApiKeyRecord record, CancellationToken ct = default);

    /// <summary>Swap the hash (and vault pointer) of an existing key. False when the key does not exist.</summary>
    Task<bool> ReplaceHashAsync(string id, string newKeyHash, string? vaultReference, CancellationToken ct = default);

    /// <summary>Mark the key inactive. False when the key does not exist.</summary>
    Task<bool> RevokeAsync(string id, CancellationToken ct = default);
}

/// <summary>Optional: told about every successfully validated use (e.g. to stamp LastUsedAt). Implementations should be cheap and never throw.</summary>
public interface IApiKeyUsageRecorder
{
    Task RecordUseAsync(ApiKeyRecord key, DateTimeOffset usedAtUtc, CancellationToken ct = default);
}

/// <summary>Drops cached lookups so revocations / rotations take effect immediately in this process (other processes converge within the cache TTL).</summary>
public interface IApiKeyCache
{
    void Invalidate(string keyHash);
    void InvalidateAll();
}

public sealed record ApiKeySecretEntry(
    string KeyId,
    string Name,
    string KeyPrefix,
    string RawKey,
    string? TenantId,
    string? ExistingReference);

/// <summary>Where raw keys are kept. Production implementation: <see cref="VaultApiKeySecretVault"/>.</summary>
public interface IApiKeySecretVault
{
    /// <summary>Store (or, when <see cref="ApiKeySecretEntry.ExistingReference"/> is set, overwrite) the raw key. Returns an opaque reference.</summary>
    Task<string> StoreAsync(ApiKeySecretEntry entry, CancellationToken ct = default);

    Task DeleteAsync(string reference, CancellationToken ct = default);
}

public enum ApiKeyAuditOutcome
{
    /// <summary>Key accepted (final HTTP status is recorded separately).</summary>
    Authenticated,
    /// <summary>Header present but empty.</summary>
    EmptyKey,
    /// <summary>No such key.</summary>
    InvalidKey,
    Revoked,
    Expired,
    IpNotAllowed,
    /// <summary>The request addressed a tenant the key does not belong to.</summary>
    TenantMismatch,
    /// <summary>Rejected by the rate limiter (HTTP 429).</summary>
    RateLimited,
    /// <summary>Key valid but its scope is too low for the endpoint (HTTP 403).</summary>
    Forbidden,
    /// <summary>The key source could not be reached and no cached record was available.</summary>
    LookupUnavailable,
}

/// <summary>One line of the API-key audit trail. Never contains the raw key.</summary>
public sealed record ApiKeyAuditEntry(
    DateTimeOffset TimestampUtc,
    ApiKeyAuditOutcome Outcome,
    string? KeyPrefix,
    string? KeyId,
    string? TenantId,
    string? Scope,
    string Method,
    string Endpoint,
    int StatusCode,
    string? RemoteIp);

/// <summary>Destination for <see cref="ApiKeyAuditEntry"/>. Register several to fan out; a failing sink never fails the request.</summary>
public interface IApiKeyAuditSink
{
    Task WriteAsync(ApiKeyAuditEntry entry, CancellationToken ct = default);
}
