using Hazina.Payments.Stripe.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Hazina.Payments.Stripe.Middleware;

/// <summary>
/// Middleware for handling Stripe webhook requests
/// </summary>
public class StripeWebhookMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<StripeWebhookMiddleware> _logger;

    public StripeWebhookMiddleware(
        RequestDelegate next,
        ILogger<StripeWebhookMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IStripeWebhookHandler webhookHandler)
    {
        try
        {
            _logger.LogInformation("Stripe webhook request received");

            // Read request body
            using var reader = new StreamReader(context.Request.Body);
            var requestBody = await reader.ReadToEndAsync();

            // Get Stripe signature header
            if (!context.Request.Headers.TryGetValue("Stripe-Signature", out var stripeSignature))
            {
                _logger.LogWarning("Missing Stripe-Signature header");
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Missing Stripe-Signature header");
                return;
            }

            // Process webhook
            var result = await webhookHandler.ProcessWebhookAsync(
                requestBody,
                stripeSignature.ToString(),
                context.RequestAborted);

            if (result.Success)
            {
                _logger.LogInformation("Webhook processed successfully: {EventType}", result.EventType);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var response = JsonSerializer.Serialize(new
                {
                    success = true,
                    eventId = result.EventId,
                    eventType = result.EventType,
                    alreadyProcessed = result.AlreadyProcessed,
                });
                await context.Response.WriteAsync(response);
            }
            else
            {
                _logger.LogError("Webhook processing failed: {Error}", result.ErrorMessage);
                context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json";
                var response = JsonSerializer.Serialize(new
                {
                    success = false,
                    error = result.ErrorMessage,
                });
                await context.Response.WriteAsync(response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing webhook");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error");
        }
    }
}
