using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Common.Infrastructure.AspNetCore.Authentication
{
    /// <summary>
    /// Extension methods for seeding users at application startup.
    /// </summary>
    public static class UserSeederExtensions
    {
        /// <summary>
        /// Seeds users from AuthOptions configuration with optional callbacks for role assignment and custom logic.
        /// </summary>
        /// <typeparam name="TContext">The DbContext type that will be migrated</typeparam>
        /// <param name="app">The WebApplication instance</param>
        /// <param name="onAdminCreated">Callback invoked when admin user is created. Parameters: (IdentityUser adminUser, UserManager)</param>
        /// <param name="onRegularUserCreated">Callback invoked when regular user is created. Parameters: (IdentityUser regularUser, UserManager)</param>
        /// <param name="onUsersSeeded">Callback invoked after both users are seeded. Parameters: (IdentityUser adminUser, IdentityUser regularUser)</param>
        /// <returns>The WebApplication for method chaining</returns>
        public static async Task<WebApplication> SeedUsersAsync<TContext>(
            this WebApplication app,
            Func<IdentityUser, UserManager<IdentityUser>, Task>? onAdminCreated = null,
            Func<IdentityUser, UserManager<IdentityUser>, Task>? onRegularUserCreated = null,
            Func<IdentityUser, IdentityUser, Task>? onUsersSeeded = null)
            where TContext : DbContext
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                // Migrate database
                var dbContext = services.GetRequiredService<TContext>();
                await dbContext.Database.MigrateAsync();

                var logger = services.GetService<ILogger<UserSeeder>>();
                logger?.LogInformation("Database migration completed");

                // Get required services
                var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
                var authOptions = services.GetRequiredService<IOptions<AuthOptions>>().Value;

                if (authOptions == null)
                {
                    logger?.LogError("AuthOptions is null!");
                    return app;
                }

                logger?.LogInformation("AuthOptions loaded - Admin: {AdminUserName}, Regular: {RegularUserName}",
                    authOptions.SeedAdminUserName,
                    authOptions.SeedRegularUserName);

                // Seed users
                var seeder = new UserSeeder(userManager, authOptions, logger);
                await seeder.SeedUsersAsync(onAdminCreated, onRegularUserCreated, onUsersSeeded);
            }
            catch (Exception ex)
            {
                var logger = services.GetService<ILogger<UserSeeder>>();
                logger?.LogError(ex, "Error during startup seeding: {Message}", ex.Message);
                throw;
            }

            return app;
        }

        /// <summary>
        /// Seeds users from AuthOptions configuration without database migration.
        /// Use this when you've already migrated the database or want to control migration separately.
        /// </summary>
        /// <param name="app">The WebApplication instance</param>
        /// <param name="onAdminCreated">Callback invoked when admin user is created. Parameters: (IdentityUser adminUser, UserManager)</param>
        /// <param name="onRegularUserCreated">Callback invoked when regular user is created. Parameters: (IdentityUser regularUser, UserManager)</param>
        /// <param name="onUsersSeeded">Callback invoked after both users are seeded. Parameters: (IdentityUser adminUser, IdentityUser regularUser)</param>
        /// <returns>The WebApplication for method chaining</returns>
        public static async Task<WebApplication> SeedUsersWithoutMigrationAsync(
            this WebApplication app,
            Func<IdentityUser, UserManager<IdentityUser>, Task>? onAdminCreated = null,
            Func<IdentityUser, UserManager<IdentityUser>, Task>? onRegularUserCreated = null,
            Func<IdentityUser, IdentityUser, Task>? onUsersSeeded = null)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
                var authOptions = services.GetRequiredService<IOptions<AuthOptions>>().Value;
                var logger = services.GetService<ILogger<UserSeeder>>();

                if (authOptions == null)
                {
                    logger?.LogError("AuthOptions is null!");
                    return app;
                }

                logger?.LogInformation("AuthOptions loaded - Admin: {AdminUserName}, Regular: {RegularUserName}",
                    authOptions.SeedAdminUserName,
                    authOptions.SeedRegularUserName);

                var seeder = new UserSeeder(userManager, authOptions, logger);
                await seeder.SeedUsersAsync(onAdminCreated, onRegularUserCreated, onUsersSeeded);
            }
            catch (Exception ex)
            {
                var logger = services.GetService<ILogger<UserSeeder>>();
                logger?.LogError(ex, "Error during startup seeding: {Message}", ex.Message);
                throw;
            }

            return app;
        }
    }
}
