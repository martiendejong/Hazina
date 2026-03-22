namespace Hazina.Payments.Stripe.Models;

/// <summary>
/// Payment record stored in database
/// </summary>
public class HazinaPayment
{
    /// <summary>
    /// Primary key
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Internal user ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Hazina customer ID (foreign key to HazinaCustomer)
    /// </summary>
    public Guid? CustomerId { get; set; }

    /// <summary>
    /// Stripe payment intent ID
    /// </summary>
    public string? StripePaymentIntentId { get; set; }

    /// <summary>
    /// Stripe charge ID (when payment completes)
    /// </summary>
    public string? StripeChargeId { get; set; }

    /// <summary>
    /// Stripe checkout session ID (for checkout sessions)
    /// </summary>
    public string? StripeSessionId { get; set; }

    /// <summary>
    /// Amount in smallest currency unit
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// Currency code (e.g., "usd", "eur")
    /// </summary>
    public string Currency { get; set; } = "eur";

    /// <summary>
    /// Payment status (pending, succeeded, failed, refunded)
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Application-specific metadata (JSON)
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Idempotency key for duplicate prevention
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// When payment was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When payment was completed (null if pending)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Failure reason if payment failed
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Navigation property to customer
    /// </summary>
    public virtual HazinaCustomer? Customer { get; set; }
}
