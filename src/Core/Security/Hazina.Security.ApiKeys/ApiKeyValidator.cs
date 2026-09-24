using System.Net;

namespace Hazina.Security.ApiKeys;

public sealed record ApiKeyValidationResult(ApiKeyAuditOutcome Outcome, ApiKeyRecord? Key)
{
    public bool Succeeded => Outcome == ApiKeyAuditOutcome.Authenticated;
}

public interface IApiKeyValidator
{
    /// <exception cref="ApiKeyLookupUnavailableException">The key source is down and nothing cached can stand in.</exception>
    Task<ApiKeyValidationResult> ValidateAsync(string? rawKey, IPAddress? remoteIp, CancellationToken ct = default);
}

/// <summary>hash -> cached lookup -> active / expiry / IP allowlist.</summary>
public sealed class ApiKeyValidator : IApiKeyValidator
{
    private readonly CachingApiKeyLookup _lookup;
    private readonly TimeProvider _time;

    public ApiKeyValidator(CachingApiKeyLookup lookup, TimeProvider? time = null)
    {
        _lookup = lookup;
        _time = time ?? TimeProvider.System;
    }

    public async Task<ApiKeyValidationResult> ValidateAsync(string? rawKey, IPAddress? remoteIp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
            return new(ApiKeyAuditOutcome.EmptyKey, null);

        var record = await _lookup.FindByHashAsync(ApiKeyHasher.Hash(rawKey), ct).ConfigureAwait(false);
        if (record is null)
            return new(ApiKeyAuditOutcome.InvalidKey, null);

        if (!record.IsActive)
            return new(ApiKeyAuditOutcome.Revoked, record);

        if (record.IsExpired(_time.GetUtcNow()))
            return new(ApiKeyAuditOutcome.Expired, record);

        if (record.AllowedIps.Count > 0 && !IsAllowedIp(record.AllowedIps, remoteIp))
            return new(ApiKeyAuditOutcome.IpNotAllowed, record);

        return new(ApiKeyAuditOutcome.Authenticated, record);
    }

    // An allowlist with an unknown caller address fails closed.
    private static bool IsAllowedIp(IReadOnlyList<string> allowed, IPAddress? remoteIp)
    {
        if (remoteIp is null) return false;
        var normalized = (remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4() : remoteIp).ToString();
        return allowed.Any(ip => string.Equals(ip, normalized, StringComparison.OrdinalIgnoreCase));
    }
}
