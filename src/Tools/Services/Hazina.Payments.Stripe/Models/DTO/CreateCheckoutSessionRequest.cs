namespace Hazina.Payments.Stripe.Models.DTO;

/// <summary>
/// Request to create a Stripe Checkout session
/// </summary>
public class CreateCheckoutSessionRequest
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
    /// Product/service name
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Product description (optional)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// URL to redirect after successful payment
    /// </summary>
    public string SuccessUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL to redirect if payment is cancelled
    /// </summary>
    public string CancelUrl { get; set; } = string.Empty;

    /// <summary>
    /// Internal user ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Customer email (required if user is not logged in)
    /// </summary>
    public string? CustomerEmail { get; set; }

    /// <summary>
    /// Additional metadata to attach to the payment
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Quantity of items (default: 1)
    /// </summary>
    public long Quantity { get; set; } = 1;
}
