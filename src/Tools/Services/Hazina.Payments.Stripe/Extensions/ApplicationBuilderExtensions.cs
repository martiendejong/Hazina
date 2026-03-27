using Hazina.Payments.Stripe.Middleware;
using Microsoft.AspNetCore.Builder;

namespace Hazina.Payments.Stripe.Extensions;

/// <summary>
/// Application builder extensions for Hazina Stripe webhooks
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Stripe webhook endpoint middleware
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <param name="path">Webhook path (default: /api/webhooks/stripe)</param>
    /// <returns>Application builder for chaining</returns>
    public static IApplicationBuilder UseHazinaStripeWebhooks(
        this IApplicationBuilder app,
        string path = "/api/webhooks/stripe")
    {
        app.Map(path, builder =>
        {
            builder.UseMiddleware<StripeWebhookMiddleware>();
        });

        return app;
    }
}
