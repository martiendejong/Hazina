using Hazina.API.Generic.Entities;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Hazina.API.Generic.Middleware;

/// <summary>
/// Middleware that extracts tenant and user context from the request.
/// This context can be used for automatic filtering of entities.
/// </summary>
public class TenantFilterMiddleware
{
    private readonly RequestDelegate _next;

    public TenantFilterMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Extract tenant from claims, header, or subdomain
        var tenantId = ExtractTenantId(context);
        var userId = ExtractUserId(context);

        tenantContext.TenantId = tenantId;
        tenantContext.UserId = userId;
        tenantContext.IsAuthenticated = context.User.Identity?.IsAuthenticated ?? false;

        await _next(context);
    }

    private Guid? ExtractTenantId(HttpContext context)
    {
        // Try claim first
        var tenantClaim = context.User.FindFirst("tenant_id") ??
                         context.User.FindFirst("TenantId");
        if (tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var tenantId))
        {
            return tenantId;
        }

        // Try header
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue) &&
            Guid.TryParse(headerValue.FirstOrDefault(), out tenantId))
        {
            return tenantId;
        }

        return null;
    }

    private Guid? ExtractUserId(HttpContext context)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier) ??
                         context.User.FindFirst("sub") ??
                         context.User.FindFirst("user_id");

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}

/// <summary>
/// Interface for accessing the current tenant/user context
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The current tenant ID (if multi-tenant)
    /// </summary>
    Guid? TenantId { get; set; }

    /// <summary>
    /// The current user ID (if authenticated)
    /// </summary>
    Guid? UserId { get; set; }

    /// <summary>
    /// Whether the current request is authenticated
    /// </summary>
    bool IsAuthenticated { get; set; }
}

/// <summary>
/// Default implementation of ITenantContext
/// Registered as scoped to persist across the request
/// </summary>
public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public bool IsAuthenticated { get; set; }
}
