namespace Hazina.Payments.Stripe.Models.DTO;

/// <summary>
/// Response from creating a payment intent
/// </summary>
public class PaymentIntentResponse
{
    /// <summary>
    /// Stripe payment intent ID
    /// </summary>
    public string PaymentIntentId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret for confirming payment on frontend
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Payment status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Amount in smallest currency unit
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// Currency code
    /// </summary>
    public string Currency { get; set; } = string.Empty;
}
