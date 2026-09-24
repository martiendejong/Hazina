using System.Security.Claims;

namespace Hazina.Security.ApiKeys;

/// <summary>
/// A stored API key as the middleware sees it. Only the SHA-256 hash of the key is ever held
/// here (or in the app's database); the raw key lives in Vault and is shown to its creator once.
/// </summary>
public sealed class ApiKeyRecord
{
    /// <summary>Stable key id (any string: GUID, integer...).</summary>
    public required string Id { get; init; }

    /// <summary>Lowercase hex SHA-256 of the raw key (see <see cref="ApiKeyHasher"/>).</summary>
    public required string KeyHash { get; init; }

    /// <summary>Non-secret identifying prefix, e.g. "iam_a3f9_". Safe to log.</summary>
    public required string KeyPrefix { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public ApiKeyScope Scope { get; init; } = ApiKeyScope.Read;

    /// <summary>Tenant this key is confined to. Null = platform-wide key (see <see cref="ApiKeyClaimTypes.Platform"/>).</summary>
    public string? TenantId { get; init; }

    /// <summary>Owning user, when the key acts on behalf of one.</summary>
    public string? UserId { get; init; }

    /// <summary>App-specific fine-grained permissions, emitted as "permission" claims (IAM's shape).</summary>
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();

    /// <summary>IP allowlist (exact addresses). Empty = any address.</summary>
    public IReadOnlyList<string> AllowedIps { get; init; } = Array.Empty<string>();

    /// <summary>Per-key requests/minute override. Null = <see cref="HazinaApiKeyOptions.DefaultRequestsPerMinute"/>.</summary>
    public int? RateLimitPerMinute { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ExpiresAtUtc { get; init; }

    /// <summary>False once revoked.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Opaque pointer to the Vault credential holding the raw key.</summary>
    public string? VaultReference { get; init; }

    /// <summary>Extra app-specific claims (e.g. email / display name) added to the principal.</summary>
    public IReadOnlyList<Claim> ExtraClaims { get; init; } = Array.Empty<Claim>();

    public bool IsExpired(DateTimeOffset nowUtc) => ExpiresAtUtc is { } exp && exp <= nowUtc;
}
