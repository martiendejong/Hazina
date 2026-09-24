using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hazina.Security.ApiKeys;

public sealed class ApiKeyCreateRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public ApiKeyScope Scope { get; init; } = ApiKeyScope.Read;

    /// <summary>Null = platform-wide key (only useful with admin scope; see <see cref="ApiKeyClaimTypes.Platform"/>).</summary>
    public string? TenantId { get; init; }

    public string? UserId { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedIps { get; init; } = Array.Empty<string>();
    public int? RateLimitPerMinute { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public IReadOnlyList<Claim> ExtraClaims { get; init; } = Array.Empty<Claim>();

    /// <summary>Optional caller-chosen id (default: a new GUID).</summary>
    public string? Id { get; init; }
}

/// <summary>The issued key. <see cref="RawKey"/> is returned to the caller exactly once; only its hash is stored by the app.</summary>
public sealed record ApiKeyIssueResult(ApiKeyRecord Record, string RawKey);

public sealed class ApiKeyIssuanceException : Exception
{
    public ApiKeyIssuanceException(string message, Exception? inner = null) : base(message, inner) { }
}

public interface IApiKeyManager
{
    Task<ApiKeyIssueResult> CreateAsync(ApiKeyCreateRequest request, CancellationToken ct = default);

    /// <returns>The new key, or null when the key does not exist.</returns>
    Task<ApiKeyIssueResult?> RotateAsync(string keyId, CancellationToken ct = default);

    Task<bool> RevokeAsync(string keyId, CancellationToken ct = default);
}

/// <summary>
/// Key lifecycle. The invariant it protects: the raw key is stored in Vault, the app database
/// (<see cref="IApiKeyStore"/>) only ever gets the hash, and the in-process cache is dropped whenever a key
/// changes so revocation / rotation take effect immediately here (elsewhere within the cache TTL).
/// Each step is compensated on failure so a half-issued key is never left usable-but-unrecoverable.
/// </summary>
public sealed class ApiKeyManager : IApiKeyManager
{
    private readonly IApiKeyStore _store;
    private readonly IApiKeySecretVault _vault;
    private readonly IApiKeyCache _cache;
    private readonly IOptions<HazinaApiKeyOptions> _options;
    private readonly ILogger<ApiKeyManager> _logger;
    private readonly TimeProvider _time;

    public ApiKeyManager(
        IApiKeyStore store,
        IApiKeySecretVault vault,
        IApiKeyCache cache,
        IOptions<HazinaApiKeyOptions> options,
        ILogger<ApiKeyManager> logger,
        TimeProvider? time = null)
    {
        _store = store;
        _vault = vault;
        _cache = cache;
        _options = options;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public async Task<ApiKeyIssueResult> CreateAsync(ApiKeyCreateRequest request, CancellationToken ct = default)
    {
        var generated = ApiKeyGenerator.Generate(_options.Value.KeyPrefixBase);
        var id = request.Id ?? Guid.NewGuid().ToString("D");

        // Vault first: a raw key with no hash row is inert, while a hash row with no recoverable raw key would be a live orphan.
        string reference;
        try
        {
            reference = await _vault.StoreAsync(
                new ApiKeySecretEntry(id, request.Name, generated.KeyPrefix, generated.RawKey, request.TenantId, ExistingReference: null), ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ApiKeyIssuanceException("The raw key could not be stored in Vault; no key was issued.", ex);
        }

        var record = new ApiKeyRecord
        {
            Id = id,
            KeyHash = generated.KeyHash,
            KeyPrefix = generated.KeyPrefix,
            Name = request.Name,
            Description = request.Description,
            Scope = request.Scope,
            TenantId = request.TenantId,
            UserId = request.UserId,
            Permissions = request.Permissions,
            AllowedIps = request.AllowedIps,
            RateLimitPerMinute = request.RateLimitPerMinute,
            CreatedAtUtc = _time.GetUtcNow(),
            ExpiresAtUtc = request.ExpiresAtUtc,
            IsActive = true,
            VaultReference = reference,
            ExtraClaims = request.ExtraClaims,
        };

        try
        {
            await _store.AddAsync(record, ct).ConfigureAwait(false);
        }
        catch
        {
            await TryDeleteFromVaultAsync(reference).ConfigureAwait(false);
            throw;
        }

        return new ApiKeyIssueResult(record, generated.RawKey);
    }

    public async Task<ApiKeyIssueResult?> RotateAsync(string keyId, CancellationToken ct = default)
    {
        var existing = await _store.FindByIdAsync(keyId, ct).ConfigureAwait(false);
        if (existing is null) return null;

        var generated = ApiKeyGenerator.GenerateWithPrefix(existing.KeyPrefix);

        // Swap the hash first, then vault; if vault fails, put the old hash back so the old key (still what Vault holds) stays the valid one.
        if (!await _store.ReplaceHashAsync(keyId, generated.KeyHash, existing.VaultReference, ct).ConfigureAwait(false))
            return null;

        string reference;
        try
        {
            reference = await _vault.StoreAsync(
                new ApiKeySecretEntry(keyId, existing.Name, existing.KeyPrefix, generated.RawKey, existing.TenantId, existing.VaultReference), ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try
            {
                await _store.ReplaceHashAsync(keyId, existing.KeyHash, existing.VaultReference, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception revertFailure)
            {
                _logger.LogCritical(revertFailure,
                    "Rotation of API key {KeyPrefix} failed AND could not be rolled back: the stored hash matches no known raw key. Rotate it again.",
                    existing.KeyPrefix);
            }

            _cache.Invalidate(existing.KeyHash);
            _cache.Invalidate(generated.KeyHash);
            throw new ApiKeyIssuanceException("The rotated key could not be stored in Vault; the previous key remains valid.", ex);
        }

        if (!string.Equals(reference, existing.VaultReference, StringComparison.Ordinal))
        {
            // A key issued before Vault support gained its vault pointer just now.
            await _store.ReplaceHashAsync(keyId, generated.KeyHash, reference, ct).ConfigureAwait(false);
        }

        _cache.Invalidate(existing.KeyHash);
        _cache.Invalidate(generated.KeyHash);

        var rotated = await _store.FindByIdAsync(keyId, ct).ConfigureAwait(false) ?? existing;
        return new ApiKeyIssueResult(rotated, generated.RawKey);
    }

    public async Task<bool> RevokeAsync(string keyId, CancellationToken ct = default)
    {
        var existing = await _store.FindByIdAsync(keyId, ct).ConfigureAwait(false);
        if (existing is null) return false;

        var revoked = await _store.RevokeAsync(keyId, ct).ConfigureAwait(false);
        _cache.Invalidate(existing.KeyHash);

        if (revoked && existing.VaultReference is { Length: > 0 } reference)
            await TryDeleteFromVaultAsync(reference).ConfigureAwait(false);

        return revoked;
    }

    private async Task TryDeleteFromVaultAsync(string reference)
    {
        try { await _vault.DeleteAsync(reference, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete Vault credential {Reference} of a revoked/aborted API key", reference); }
    }
}
