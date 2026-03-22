namespace Hazina.Payments.Stripe.Models;

/// <summary>
/// Customer record linked to Stripe customer
/// </summary>
public class HazinaCustomer
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
    /// Stripe customer ID
    /// </summary>
    public string StripeCustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Customer email
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Customer name (optional)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Additional metadata (JSON)
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// When customer was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When customer was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to payments
    /// </summary>
    public virtual ICollection<HazinaPayment> Payments { get; set; } = new List<HazinaPayment>();

    /// <summary>
    /// Navigation property to subscriptions
    /// </summary>
    public virtual ICollection<HazinaSubscription> Subscriptions { get; set; } = new List<HazinaSubscription>();
}
