namespace Hazina.Payments.Stripe.Models;

/// <summary>
/// Subscription record
/// </summary>
public class HazinaSubscription
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
    /// Hazina customer ID (foreign key)
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Stripe customer ID
    /// </summary>
    public string StripeCustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Stripe subscription ID
    /// </summary>
    public string StripeSubscriptionId { get; set; } = string.Empty;

    /// <summary>
    /// Stripe price ID
    /// </summary>
    public string StripePriceId { get; set; } = string.Empty;

    /// <summary>
    /// Subscription status (active, canceled, past_due, etc.)
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// Plan type/name (application-specific)
    /// </summary>
    public string? PlanType { get; set; }

    /// <summary>
    /// Current period start
    /// </summary>
    public DateTime CurrentPeriodStart { get; set; }

    /// <summary>
    /// Current period end
    /// </summary>
    public DateTime CurrentPeriodEnd { get; set; }

    /// <summary>
    /// When subscription will be canceled (null if not scheduled)
    /// </summary>
    public DateTime? CancelAt { get; set; }

    /// <summary>
    /// When subscription was cancelled (null if active)
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Whether subscription cancels at period end
    /// </summary>
    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>
    /// Trial start date (null if no trial)
    /// </summary>
    public DateTime? TrialStart { get; set; }

    /// <summary>
    /// Trial end date (null if no trial)
    /// </summary>
    public DateTime? TrialEnd { get; set; }

    /// <summary>
    /// Additional metadata (JSON)
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// When subscription was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When subscription was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to customer
    /// </summary>
    public virtual HazinaCustomer? Customer { get; set; }
}
