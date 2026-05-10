using Hazina.Payments.Stripe.Models;

namespace Hazina.Payments.Stripe.Configuration;

/// <summary>
/// Configuration options for Hazina Stripe integration
/// </summary>
public class HazinaStripeOptions
{
    /// <summary>
    /// Stripe secret key (required)
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Stripe publishable key (required for frontend)
    /// </summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>
    /// Webhook secret for signature verification (required)
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Callback invoked when payment succeeds
    /// </summary>
    public Func<PaymentSucceededEventArgs, Task>? OnPaymentSucceeded { get; set; }

    /// <summary>
    /// Callback invoked when payment fails
    /// </summary>
    public Func<PaymentFailedEventArgs, Task>? OnPaymentFailed { get; set; }

    /// <summary>
    /// Callback invoked when subscription is created
    /// </summary>
    public Func<SubscriptionCreatedEventArgs, Task>? OnSubscriptionCreated { get; set; }

    /// <summary>
    /// Callback invoked when subscription is cancelled
    /// </summary>
    public Func<SubscriptionCancelledEventArgs, Task>? OnSubscriptionCancelled { get; set; }

    /// <summary>
    /// Validates configuration
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SecretKey))
            throw new InvalidOperationException("Stripe SecretKey is required");

        if (string.IsNullOrWhiteSpace(WebhookSecret))
            throw new InvalidOperationException("Stripe WebhookSecret is required");
    }
}

/// <summary>
/// Event args for payment succeeded callback
/// </summary>
public class PaymentSucceededEventArgs
{
    /// <summary>
    /// Payment details
    /// </summary>
    public required HazinaPayment Payment { get; init; }

    /// <summary>
    /// Payment metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Service provider for dependency injection
    /// </summary>
    public required IServiceProvider ServiceProvider { get; init; }
}

/// <summary>
/// Event args for payment failed callback
/// </summary>
public class PaymentFailedEventArgs
{
    /// <summary>
    /// Payment details
    /// </summary>
    public required HazinaPayment Payment { get; init; }

    /// <summary>
    /// Failure reason
    /// </summary>
    public required string FailureReason { get; init; }

    /// <summary>
    /// Service provider for dependency injection
    /// </summary>
    public required IServiceProvider ServiceProvider { get; init; }
}

/// <summary>
/// Event args for subscription created callback
/// </summary>
public class SubscriptionCreatedEventArgs
{
    /// <summary>
    /// Subscription details
    /// </summary>
    public required HazinaSubscription Subscription { get; init; }

    /// <summary>
    /// Service provider for dependency injection
    /// </summary>
    public required IServiceProvider ServiceProvider { get; init; }
}

/// <summary>
/// Event args for subscription cancelled callback
/// </summary>
public class SubscriptionCancelledEventArgs
{
    /// <summary>
    /// Subscription details
    /// </summary>
    public required HazinaSubscription Subscription { get; init; }

    /// <summary>
    /// Service provider for dependency injection
    /// </summary>
    public required IServiceProvider ServiceProvider { get; init; }
}
