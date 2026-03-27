namespace Hazina.Payments.Stripe.Models.DTO;

/// <summary>
/// Request to create a Stripe Payment Intent
/// </summary>
public class CreatePaymentIntentRequest
{
    /// <summary>
    /// Amount in smallest currency unit (cents for USD/EUR)
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// Currency code (e.g., "usd", "eur")
    /// </summary>
    public string Currency { get; set; } = "eur";

    /// <summary>
    /// Internal user ID (optional)
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Customer ID (optional, creates new customer if not provided)
    /// </summary>
    public string? CustomerId { get; set; }

    /// <summary>
    /// Customer email (required if CustomerId not provided)
    /// </summary>
    public string? CustomerEmail { get; set; }

    /// <summary>
    /// Description of the payment
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Automatic payment methods (default: card)
    /// </summary>
    public List<string> PaymentMethodTypes { get; set; } = new() { "card" };
}
