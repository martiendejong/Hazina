using Hazina.API.DocumentStore.Configuration;
using Hazina.API.DocumentStore.Controllers;
using Hazina.API.DocumentStore.Data;
using Hazina.API.DocumentStore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hazina.API.DocumentStore.Extensions;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Maps Hazina Document Store API middleware and optionally auto-creates the database.
    /// Call this after UseRouting() and before MapControllers().
    /// </summary>
    public static IApplicationBuilder UseHazinaDocumentStoreApi(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<DocumentStoreApiOptions>>().Value;

        // Auto-migrate database
        if (options.AutoMigrateDatabase)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DocumentStoreDbContext>();
            dbContext.Database.EnsureCreated();
        }

        // Conditionally exclude AuthController from the application part manager
        if (!options.IncludeAuthController)
        {
            var partManager = app.ApplicationServices.GetRequiredService<ApplicationPartManager>();
            partManager.FeatureProviders.Add(
                new ExcludeControllerFeatureProvider(typeof(AuthController)));
        }

        // Register exception handling middleware
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        return app;
    }
}
