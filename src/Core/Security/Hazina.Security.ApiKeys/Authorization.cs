using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Hazina.Security.ApiKeys;

/// <summary>
/// Policy names for <c>[Authorize(Policy = HazinaApiKeyPolicies.Write)]</c>. Each policy demands an
/// API-key principal with at least that scope AND passes the tenant-isolation check for any tenant the
/// request addresses (route value, query string, tenant header) or the resource being authorized.
/// </summary>
public static class HazinaApiKeyPolicies
{
    public const string Read = "HazinaApiKey.Read";
    public const string Write = "HazinaApiKey.Write";
    public const string Admin = "HazinaApiKey.Admin";
}

/// <summary>Minimum scope an API key must hold.</summary>
public sealed class ApiKeyScopeRequirement : IAuthorizationRequirement
{
    public ApiKeyScopeRequirement(ApiKeyScope minimum) => Minimum = minimum;
    public ApiKeyScope Minimum { get; }
}

/// <summary>The key must be allowed to touch every tenant the request addresses.</summary>
public sealed class ApiKeyTenantRequirement : IAuthorizationRequirement { }

/// <summary>Resource for <c>IAuthorizationService.AuthorizeAsync(user, new ApiKeyTenantResource(tenantId), policy)</c>.</summary>
public sealed record ApiKeyTenantResource(string TenantId);

internal sealed class ApiKeyScopeHandler : AuthorizationHandler<ApiKeyScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ApiKeyScopeRequirement requirement)
    {
        if (context.User.IsApiKey()
            && context.User.GetApiKeyScope() is { } scope
            && scope.Satisfies(requirement.Minimum))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

internal sealed class ApiKeyTenantHandler : AuthorizationHandler<ApiKeyTenantRequirement>
{
    private readonly IOptions<HazinaApiKeyOptions> _options;
    private readonly IHttpContextAccessor _accessor;

    public ApiKeyTenantHandler(IOptions<HazinaApiKeyOptions> options, IHttpContextAccessor accessor)
    {
        _options = options;
        _accessor = accessor;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ApiKeyTenantRequirement requirement)
    {
        var user = context.User;
        if (!user.IsApiKey())
            return Task.CompletedTask; // not ours to approve

        // An API-key principal with neither a tenant nor the platform marker is "unscoped": never acceptable.
        if (user.GetApiKeyTenantId() is null && !user.IsPlatformKey())
            return Task.CompletedTask;

        var http = context.Resource as HttpContext ?? _accessor.HttpContext;
        var addressed = AddressedTenants(http, context.Resource, _options.Value);

        foreach (var tenant in addressed)
        {
            if (!user.CanAccessTenant(tenant))
            {
                if (http is not null)
                    http.Items[ApiKeyDefaults.AuditOutcomeItemKey] = ApiKeyAuditOutcome.TenantMismatch;
                return Task.CompletedTask; // no Succeed: the policy fails
            }
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }

    internal static IReadOnlyList<string> AddressedTenants(HttpContext? http, object? resource, HazinaApiKeyOptions options)
    {
        var tenants = new List<string>();

        if (resource is ApiKeyTenantResource r && !string.IsNullOrWhiteSpace(r.TenantId))
            tenants.Add(r.TenantId);

        if (http is null) return tenants;

        foreach (var name in options.TenantParameterNames)
        {
            if (http.GetRouteValue(name)?.ToString() is { Length: > 0 } routeValue)
                tenants.Add(routeValue);
            foreach (var q in http.Request.Query[name])
                if (!string.IsNullOrWhiteSpace(q)) tenants.Add(q!);
        }

        foreach (var h in http.Request.Headers[options.TenantHeaderName])
            if (!string.IsNullOrWhiteSpace(h)) tenants.Add(h!);

        return tenants;
    }
}
