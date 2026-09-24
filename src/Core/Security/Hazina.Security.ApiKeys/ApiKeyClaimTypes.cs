namespace Hazina.Security.ApiKeys;

/// <summary>
/// Claim names on the principal built for an authenticated API key. Mirrors the shape IAM's
/// original ApiKeyAuthenticationMiddleware produced (api_key_*, auth_method, tenant_id,
/// sub, permission) plus the new <see cref="Scope"/> and <see cref="Platform"/> claims.
/// </summary>
public static class ApiKeyClaimTypes
{
    public const string KeyId = "api_key_id";
    public const string KeyName = "api_key_name";
    public const string KeyPrefix = "api_key_prefix";

    /// <summary>read | write | admin.</summary>
    public const string Scope = "api_key_scope";

    public const string AuthMethod = "auth_method";
    public const string AuthMethodApiKey = "api_key";

    public const string TenantId = "tenant_id";
    public const string Subject = "sub";
    public const string Permission = "permission";

    /// <summary>
    /// "true" on a key that belongs to no tenant (platform-wide). Such a key only passes the
    /// tenant check for tenant-addressed requests when its scope is admin; a principal with
    /// neither a tenant_id nor this claim is treated as unscoped and fails closed.
    /// </summary>
    public const string Platform = "api_key_platform";
}

public static class ApiKeyDefaults
{
    /// <summary>ClaimsIdentity.AuthenticationType of an API-key principal.</summary>
    public const string AuthenticationType = "ApiKey";

    public const string DefaultHeaderName = "X-Api-Key";
    public const string DefaultTenantHeaderName = "X-Tenant-Id";
    public const string DefaultTenantParameterName = "tenantId";

    /// <summary>HttpContext.Items key holding the validated <see cref="ApiKeyRecord"/>.</summary>
    public const string RecordItemKey = "Hazina.ApiKeys.Record";

    /// <summary>HttpContext.Items key: the key prefix (kept for apps that gated on IAM's old "ApiKeyPrefix" marker).</summary>
    public const string PrefixItemKey = "ApiKeyPrefix";

    internal const string AuditOutcomeItemKey = "Hazina.ApiKeys.AuditOutcome";
}
