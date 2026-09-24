using System.Security.Claims;

namespace Hazina.Security.ApiKeys;

/// <summary>Builds the ClaimsPrincipal for a validated key (IAM's claim shape + scope) and reads it back.</summary>
public static class ApiKeyPrincipal
{
    public static ClaimsPrincipal Create(ApiKeyRecord key)
    {
        var claims = new List<Claim>
        {
            new(ApiKeyClaimTypes.KeyId, key.Id),
            new(ApiKeyClaimTypes.KeyName, key.Name),
            new(ApiKeyClaimTypes.KeyPrefix, key.KeyPrefix),
            new(ApiKeyClaimTypes.Scope, key.Scope.ToClaimValue()),
            new(ApiKeyClaimTypes.AuthMethod, ApiKeyClaimTypes.AuthMethodApiKey),
        };

        if (!string.IsNullOrEmpty(key.UserId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, key.UserId));
            claims.Add(new Claim(ApiKeyClaimTypes.Subject, key.UserId));
        }

        if (!string.IsNullOrEmpty(key.TenantId))
            claims.Add(new Claim(ApiKeyClaimTypes.TenantId, key.TenantId));
        else
            claims.Add(new Claim(ApiKeyClaimTypes.Platform, "true"));

        foreach (var permission in key.Permissions)
            claims.Add(new Claim(ApiKeyClaimTypes.Permission, permission));

        // Extra claims can add context but never override the security-relevant ones above.
        foreach (var extra in key.ExtraClaims)
            if (!IsReserved(extra.Type))
                claims.Add(extra);

        return new ClaimsPrincipal(new ClaimsIdentity(claims, ApiKeyDefaults.AuthenticationType));
    }

    private static bool IsReserved(string type) =>
        type is ApiKeyClaimTypes.TenantId or ApiKeyClaimTypes.Scope or ApiKeyClaimTypes.Platform
            or ApiKeyClaimTypes.AuthMethod or ApiKeyClaimTypes.KeyId or ApiKeyClaimTypes.KeyPrefix
            or ApiKeyClaimTypes.KeyName;
}

public static class ApiKeyPrincipalExtensions
{
    /// <summary>True when this principal was authenticated by an API key.</summary>
    public static bool IsApiKey(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && user.HasClaim(ApiKeyClaimTypes.AuthMethod, ApiKeyClaimTypes.AuthMethodApiKey);

    public static string? GetApiKeyTenantId(this ClaimsPrincipal user) =>
        user.FindFirst(ApiKeyClaimTypes.TenantId)?.Value;

    public static string? GetApiKeyId(this ClaimsPrincipal user) =>
        user.FindFirst(ApiKeyClaimTypes.KeyId)?.Value;

    /// <summary>The key's scope, or null for a missing/unknown claim (callers must treat that as no access).</summary>
    public static ApiKeyScope? GetApiKeyScope(this ClaimsPrincipal user) =>
        ApiKeyScopes.TryParse(user.FindFirst(ApiKeyClaimTypes.Scope)?.Value, out var scope) ? scope : null;

    public static bool IsPlatformKey(this ClaimsPrincipal user) =>
        user.HasClaim(ApiKeyClaimTypes.Platform, "true") && user.GetApiKeyTenantId() is null;

    /// <summary>
    /// The tenant rule, in one place. Fails closed:
    /// a tenant key reaches only its own tenant; a platform key (no tenant) reaches tenants only with admin
    /// scope; a principal with neither a tenant nor the platform marker (an "unscoped" claim set) reaches nothing.
    /// Also use this from services to guard a specific resource: <c>if (!User.CanAccessTenant(order.TenantId)) return Forbid();</c>
    /// </summary>
    public static bool CanAccessTenant(this ClaimsPrincipal user, string? requestedTenantId)
    {
        if (!user.IsApiKey()) return false;
        if (string.IsNullOrWhiteSpace(requestedTenantId)) return false;

        var own = user.GetApiKeyTenantId();
        if (!string.IsNullOrEmpty(own))
            return TenantIds.AreEqual(own, requestedTenantId);

        return user.IsPlatformKey() && user.GetApiKeyScope() == ApiKeyScope.Admin;
    }
}

internal static class TenantIds
{
    /// <summary>Guid-aware comparison (upper/lower case GUID text is the same tenant); everything else is ordinal.</summary>
    public static bool AreEqual(string a, string b) =>
        Guid.TryParse(a, out var ga) && Guid.TryParse(b, out var gb)
            ? ga == gb
            : string.Equals(a, b, StringComparison.Ordinal);
}
