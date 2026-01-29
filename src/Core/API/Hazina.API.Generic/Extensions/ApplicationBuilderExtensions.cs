using Hazina.API.Generic.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hazina.API.Generic.Extensions;

/// <summary>
/// Extension methods for configuring the application pipeline
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the tenant filtering middleware to the pipeline.
    /// Call this after authentication middleware.
    /// </summary>
    public static IApplicationBuilder UseGenericEntityApi(this IApplicationBuilder app)
    {
        // Ensure tenant context is registered
        using var scope = app.ApplicationServices.CreateScope();
        var tenantContext = scope.ServiceProvider.GetService<ITenantContext>();

        if (tenantContext == null)
        {
            throw new InvalidOperationException(
                "ITenantContext is not registered. Call services.AddScoped<ITenantContext, TenantContext>() in ConfigureServices.");
        }

        app.UseMiddleware<TenantFilterMiddleware>();

        return app;
    }
}
