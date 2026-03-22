using Stripe;

namespace Hazina.Payments.Stripe.Core;

/// <summary>
/// Service for managing Stripe subscriptions
/// </summary>
public interface IStripeSubscriptionService
{
    /// <summary>
    /// Creates a new subscription for a customer
    /// </summary>
    /// <param name="customerId">Stripe customer ID</param>
    /// <param name="priceId">Stripe price ID</param>
    /// <param name="metadata">Additional metadata</param>
    /// <param name="trialDays">Trial period in days (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created subscription</returns>
    Task<Subscription> CreateSubscriptionAsync(
        string customerId,
        string priceId,
        Dictionary<string, string>? metadata = null,
        int? trialDays = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a subscription immediately or at period end
    /// </summary>
    /// <param name="subscriptionId">Stripe subscription ID</param>
    /// <param name="atPeriodEnd">Cancel at period end (true) or immediately (false)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cancelled subscription</returns>
    Task<Subscription> CancelSubscriptionAsync(
        string subscriptionId,
        bool atPeriodEnd = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets subscription by ID
    /// </summary>
    /// <param name="subscriptionId">Stripe subscription ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Subscription or null if not found</returns>
    Task<Subscription?> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists active subscriptions for a customer
    /// </summary>
    /// <param name="customerId">Stripe customer ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active subscriptions</returns>
    Task<List<Subscription>> ListCustomerSubscriptionsAsync(
        string customerId,
        CancellationToken cancellationToken = default);
}
