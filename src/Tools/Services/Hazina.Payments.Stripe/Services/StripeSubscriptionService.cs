using Hazina.Payments.Stripe.Configuration;
using Hazina.Payments.Stripe.Core;
using Hazina.Payments.Stripe.Data;
using Hazina.Payments.Stripe.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace Hazina.Payments.Stripe.Services;

/// <summary>
/// Implementation of Stripe subscription service
/// </summary>
public class StripeSubscriptionService : IStripeSubscriptionService
{
    private readonly HazinaStripeOptions _options;
    private readonly HazinaPaymentsDbContext _dbContext;
    private readonly ILogger<StripeSubscriptionService> _logger;
    private readonly SubscriptionService _subscriptionService;

    public StripeSubscriptionService(
        IOptions<HazinaStripeOptions> options,
        HazinaPaymentsDbContext dbContext,
        ILogger<StripeSubscriptionService> logger)
    {
        _options = options.Value;
        _dbContext = dbContext;
        _logger = logger;

        // Validate configuration
        _options.Validate();

        // Initialize Stripe service
        _subscriptionService = new SubscriptionService();
    }

    public async Task<Subscription> CreateSubscriptionAsync(
        string customerId,
        string priceId,
        Dictionary<string, string>? metadata = null,
        int? trialDays = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating subscription for customer {CustomerId} with price {PriceId}",
                customerId, priceId);

            var options = new SubscriptionCreateOptions
            {
                Customer = customerId,
                Items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions
                    {
                        Price = priceId,
                    },
                },
                Metadata = metadata ?? new Dictionary<string, string>(),
            };

            if (trialDays.HasValue)
            {
                options.TrialPeriodDays = trialDays.Value;
            }

            // Create subscription via Stripe API
            var subscription = await _subscriptionService.CreateAsync(options, cancellationToken: cancellationToken);

            // Save subscription record to database
            var hazinaSubscription = new HazinaSubscription
            {
                StripeSubscriptionId = subscription.Id,
                StripeCustomerId = customerId,
                StripePriceId = priceId,
                Status = subscription.Status,
                CurrentPeriodStart = subscription.CurrentPeriodStart,
                CurrentPeriodEnd = subscription.CurrentPeriodEnd,
                TrialStart = subscription.TrialStart,
                TrialEnd = subscription.TrialEnd,
                Metadata = System.Text.Json.JsonSerializer.Serialize(metadata),
            };

            _dbContext.Subscriptions.Add(hazinaSubscription);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created subscription {SubscriptionId}", subscription.Id);

            return subscription;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error creating subscription");
            throw new InvalidOperationException($"Failed to create subscription: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating subscription");
            throw;
        }
    }

    public async Task<Subscription> CancelSubscriptionAsync(
        string subscriptionId,
        bool atPeriodEnd = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Cancelling subscription {SubscriptionId} (at period end: {AtPeriodEnd})",
                subscriptionId, atPeriodEnd);

            Subscription subscription;

            if (atPeriodEnd)
            {
                // Mark for cancellation at period end
                var options = new SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = true,
                };

                subscription = await _subscriptionService.UpdateAsync(subscriptionId, options, cancellationToken: cancellationToken);
            }
            else
            {
                // Cancel immediately
                subscription = await _subscriptionService.CancelAsync(subscriptionId, cancellationToken: cancellationToken);
            }

            // Update database record
            var hazinaSubscription = await _dbContext.Subscriptions
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId, cancellationToken);

            if (hazinaSubscription != null)
            {
                hazinaSubscription.Status = subscription.Status;
                hazinaSubscription.CancelledAt = atPeriodEnd ? null : DateTime.UtcNow;
                hazinaSubscription.CancelAtPeriodEnd = atPeriodEnd;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Cancelled subscription {SubscriptionId}", subscriptionId);

            return subscription;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error cancelling subscription");
            throw new InvalidOperationException($"Failed to cancel subscription: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error cancelling subscription");
            throw;
        }
    }

    public async Task<Subscription?> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting subscription {SubscriptionId}", subscriptionId);

            var subscription = await _subscriptionService.GetAsync(subscriptionId, cancellationToken: cancellationToken);

            _logger.LogInformation("Retrieved subscription {SubscriptionId}", subscriptionId);

            return subscription;
        }
        catch (StripeException ex) when (ex.StripeError.Type == "invalid_request_error")
        {
            _logger.LogWarning("Subscription {SubscriptionId} not found", subscriptionId);
            return null;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error getting subscription");
            throw new InvalidOperationException($"Failed to get subscription: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting subscription");
            throw;
        }
    }

    public async Task<List<Subscription>> ListCustomerSubscriptionsAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Listing subscriptions for customer {CustomerId}", customerId);

            var options = new SubscriptionListOptions
            {
                Customer = customerId,
                Status = "all",
            };

            var subscriptions = await _subscriptionService.ListAsync(options, cancellationToken: cancellationToken);

            _logger.LogInformation("Retrieved {Count} subscriptions for customer {CustomerId}",
                subscriptions.Data.Count, customerId);

            return subscriptions.Data.ToList();
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error listing subscriptions");
            throw new InvalidOperationException($"Failed to list subscriptions: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing subscriptions");
            throw;
        }
    }
}
