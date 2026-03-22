using Hazina.Payments.Stripe.Configuration;
using Hazina.Payments.Stripe.Core;
using Hazina.Payments.Stripe.Data;
using Hazina.Payments.Stripe.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stripe;

namespace Hazina.Payments.Stripe.Extensions;

/// <summary>
/// Dependency injection extensions for Hazina Stripe payments
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Hazina Stripe payment services to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Configuration callback</param>
    /// <param name="configureDbContext">DbContext configuration callback (optional)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddHazinaStripePayments(
        this IServiceCollection services,
        Action<HazinaStripeOptions> configureOptions,
        Action<DbContextOptionsBuilder>? configureDbContext = null)
    {
        // Register configuration
        services.Configure(configureOptions);

        // Validate and configure Stripe API key
        var options = new HazinaStripeOptions();
        configureOptions(options);
        options.Validate();

        // Set global Stripe API key
        StripeConfiguration.ApiKey = options.SecretKey;

        // Register DbContext
        if (configureDbContext != null)
        {
            services.AddDbContext<HazinaPaymentsDbContext>(configureDbContext);
        }
        else
        {
            // Default: use in-memory database for testing
            services.AddDbContext<HazinaPaymentsDbContext>(options =>
                options.UseInMemoryDatabase("HazinaPayments"));
        }

        // Register services
        services.AddScoped<IStripePaymentService, StripePaymentService>();
        services.AddScoped<IStripeCustomerService, StripeCustomerService>();
        services.AddScoped<IStripeSubscriptionService, StripeSubscriptionService>();
        services.AddScoped<IStripeWebhookHandler, StripeWebhookHandler>();

        return services;
    }

    /// <summary>
    /// Adds Hazina Stripe payment services with SQL Server database
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Configuration callback</param>
    /// <param name="connectionString">SQL Server connection string</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddHazinaStripePaymentsWithSqlServer(
        this IServiceCollection services,
        Action<HazinaStripeOptions> configureOptions,
        string connectionString)
    {
        return services.AddHazinaStripePayments(
            configureOptions,
            options => options.UseSqlServer(connectionString));
    }

    /// <summary>
    /// Adds Hazina Stripe payment services with PostgreSQL database
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Configuration callback</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddHazinaStripePaymentsWithPostgreSql(
        this IServiceCollection services,
        Action<HazinaStripeOptions> configureOptions,
        string connectionString)
    {
        return services.AddHazinaStripePayments(
            configureOptions,
            options => options.UseNpgsql(connectionString));
    }

    /// <summary>
    /// Ensures the Hazina Payments database is created and migrations are applied
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <returns>Task</returns>
    public static async Task EnsureHazinaPaymentsDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HazinaPaymentsDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
