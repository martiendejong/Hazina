namespace Hazina.Payments.Stripe.Models;

/// <summary>
/// Webhook event log for idempotency and audit trail
/// </summary>
public class HazinaWebhookEvent
{
    /// <summary>
    /// Primary key
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Stripe event ID (for idempotency)
    /// </summary>
    public string StripeEventId { get; set; } = string.Empty;

    /// <summary>
    /// Event type (e.g., "checkout.session.completed")
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Raw webhook payload (JSON)
    /// </summary>
    public string RawPayload { get; set; } = string.Empty;

    /// <summary>
    /// When event was processed (null if pending)
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Processing status (pending, succeeded, failed)
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// When to retry next (null if not scheduled)
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// When event was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
