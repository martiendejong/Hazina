using Stripe;

namespace Hazina.Payments.Stripe.Core;

/// <summary>
/// Handles Stripe webhook events with signature verification
/// </summary>
public interface IStripeWebhookHandler
{
    /// <summary>
    /// Processes a webhook event from Stripe
    /// </summary>
    /// <param name="requestBody">Raw webhook request body</param>
    /// <param name="stripeSignature">Stripe-Signature header value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    Task<WebhookProcessingResult> ProcessWebhookAsync(
        string requestBody,
        string stripeSignature,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a custom event handler for specific Stripe event types
    /// </summary>
    /// <param name="eventType">Stripe event type (e.g., "customer.subscription.created")</param>
    /// <param name="handler">Handler function</param>
    void RegisterEventHandler(
        string eventType,
        Func<Event, CancellationToken, Task> handler);
}

/// <summary>
/// Result of webhook processing
/// </summary>
public class WebhookProcessingResult
{
    /// <summary>
    /// Whether the webhook was processed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Event type that was processed
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Event ID from Stripe
    /// </summary>
    public string? EventId { get; set; }

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether this event was already processed (idempotency check)
    /// </summary>
    public bool AlreadyProcessed { get; set; }
}
