using Hazina.Payments.Stripe.Configuration;
using Hazina.Payments.Stripe.Core;
using Hazina.Payments.Stripe.Data;
using Hazina.Payments.Stripe.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace Hazina.Payments.Stripe.Services;

/// <summary>
/// Implementation of Stripe webhook handler
/// </summary>
public class StripeWebhookHandler : IStripeWebhookHandler
{
    private readonly HazinaStripeOptions _options;
    private readonly HazinaPaymentsDbContext _dbContext;
    private readonly ILogger<StripeWebhookHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, List<Func<Event, CancellationToken, Task>>> _eventHandlers;

    public StripeWebhookHandler(
        IOptions<HazinaStripeOptions> options,
        HazinaPaymentsDbContext dbContext,
        ILogger<StripeWebhookHandler> logger,
        IServiceProvider serviceProvider)
    {
        _options = options.Value;
        _dbContext = dbContext;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _eventHandlers = new Dictionary<string, List<Func<Event, CancellationToken, Task>>>();

        // Validate configuration
        _options.Validate();
    }

    public async Task<WebhookProcessingResult> ProcessWebhookAsync(
        string requestBody,
        string stripeSignature,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing webhook with signature");

            // Verify signature
            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    requestBody,
                    stripeSignature,
                    _options.WebhookSecret
                );
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Webhook signature verification failed");
                return new WebhookProcessingResult
                {
                    Success = false,
                    ErrorMessage = "Signature verification failed",
                };
            }

            _logger.LogInformation("Webhook event {EventId} of type {EventType}",
                stripeEvent.Id, stripeEvent.Type);

            // Check idempotency - has this event been processed?
            var existingEvent = await _dbContext.WebhookEvents
                .FirstOrDefaultAsync(e => e.StripeEventId == stripeEvent.Id, cancellationToken);

            if (existingEvent != null)
            {
                _logger.LogWarning("Event {EventId} already processed, skipping", stripeEvent.Id);
                return new WebhookProcessingResult
                {
                    Success = true,
                    EventType = stripeEvent.Type,
                    EventId = stripeEvent.Id,
                    AlreadyProcessed = true,
                };
            }

            // Log event to database
            var webhookEvent = new HazinaWebhookEvent
            {
                StripeEventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                RawPayload = requestBody,
                Status = "pending",
            };

            _dbContext.WebhookEvents.Add(webhookEvent);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Process event based on type
            try
            {
                await ProcessEventByTypeAsync(stripeEvent, cancellationToken);

                // Call registered custom handlers
                if (_eventHandlers.TryGetValue(stripeEvent.Type, out var handlers))
                {
                    foreach (var handler in handlers)
                    {
                        await handler(stripeEvent, cancellationToken);
                    }
                }

                // Mark as succeeded
                webhookEvent.Status = "succeeded";
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Successfully processed webhook event {EventId}", stripeEvent.Id);

                return new WebhookProcessingResult
                {
                    Success = true,
                    EventType = stripeEvent.Type,
                    EventId = stripeEvent.Id,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook event {EventId}", stripeEvent.Id);

                // Mark as failed
                webhookEvent.Status = "failed";
                webhookEvent.ErrorMessage = ex.Message;
                webhookEvent.RetryCount++;
                webhookEvent.NextRetryAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, webhookEvent.RetryCount)); // Exponential backoff
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new WebhookProcessingResult
                {
                    Success = false,
                    EventType = stripeEvent.Type,
                    EventId = stripeEvent.Id,
                    ErrorMessage = ex.Message,
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing webhook");
            return new WebhookProcessingResult
            {
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }

    public void RegisterEventHandler(
        string eventType,
        Func<Event, CancellationToken, Task> handler)
    {
        if (!_eventHandlers.ContainsKey(eventType))
        {
            _eventHandlers[eventType] = new List<Func<Event, CancellationToken, Task>>();
        }

        _eventHandlers[eventType].Add(handler);
        _logger.LogInformation("Registered custom handler for event type {EventType}", eventType);
    }

    private async Task ProcessEventByTypeAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutSessionCompletedAsync(stripeEvent, cancellationToken);
                break;

            case "payment_intent.succeeded":
                await HandlePaymentIntentSucceededAsync(stripeEvent, cancellationToken);
                break;

            case "payment_intent.payment_failed":
                await HandlePaymentIntentFailedAsync(stripeEvent, cancellationToken);
                break;

            case "customer.subscription.created":
                await HandleSubscriptionCreatedAsync(stripeEvent, cancellationToken);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(stripeEvent, cancellationToken);
                break;

            case "customer.subscription.updated":
                await HandleSubscriptionUpdatedAsync(stripeEvent, cancellationToken);
                break;

            default:
                _logger.LogInformation("Unhandled event type {EventType}", stripeEvent.Type);
                break;
        }
    }

    private async Task HandleCheckoutSessionCompletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var session = stripeEvent.Data.Object as global::Stripe.Checkout.Session;
        if (session == null) return;

        _logger.LogInformation("Checkout session completed {SessionId}", session.Id);

        // Update payment record
        var payment = await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.StripeSessionId == session.Id, cancellationToken);

        if (payment != null)
        {
            payment.Status = "succeeded";
            payment.StripePaymentIntentId = session.PaymentIntentId;
            payment.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Call OnPaymentSucceeded callback
            if (_options.OnPaymentSucceeded != null)
            {
                await _options.OnPaymentSucceeded(new PaymentSucceededEventArgs
                {
                    Payment = payment,
                    Metadata = session.Metadata ?? new Dictionary<string, string>(),
                    ServiceProvider = _serviceProvider,
                });
            }
        }
    }

    private async Task HandlePaymentIntentSucceededAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null) return;

        _logger.LogInformation("Payment intent succeeded {PaymentIntentId}", paymentIntent.Id);

        // Update payment record
        var payment = await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id, cancellationToken);

        if (payment != null)
        {
            payment.Status = "succeeded";
            payment.StripeChargeId = paymentIntent.LatestChargeId;
            payment.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Call OnPaymentSucceeded callback
            if (_options.OnPaymentSucceeded != null)
            {
                await _options.OnPaymentSucceeded(new PaymentSucceededEventArgs
                {
                    Payment = payment,
                    Metadata = paymentIntent.Metadata ?? new Dictionary<string, string>(),
                    ServiceProvider = _serviceProvider,
                });
            }
        }
    }

    private async Task HandlePaymentIntentFailedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null) return;

        _logger.LogInformation("Payment intent failed {PaymentIntentId}", paymentIntent.Id);

        // Update payment record
        var payment = await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id, cancellationToken);

        if (payment != null)
        {
            payment.Status = "failed";
            payment.FailureReason = paymentIntent.LastPaymentError?.Message ?? "Unknown error";
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Call OnPaymentFailed callback
            if (_options.OnPaymentFailed != null)
            {
                await _options.OnPaymentFailed(new PaymentFailedEventArgs
                {
                    Payment = payment,
                    FailureReason = payment.FailureReason,
                    ServiceProvider = _serviceProvider,
                });
            }
        }
    }

    private async Task HandleSubscriptionCreatedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null) return;

        _logger.LogInformation("Subscription created {SubscriptionId}", subscription.Id);

        // Check if already exists
        var existing = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id, cancellationToken);

        if (existing == null)
        {
            // Create subscription record
            var hazinaSubscription = new HazinaSubscription
            {
                StripeSubscriptionId = subscription.Id,
                StripeCustomerId = subscription.CustomerId,
                StripePriceId = subscription.Items.Data[0].Price.Id,
                Status = subscription.Status,
                CurrentPeriodStart = subscription.CurrentPeriodStart,
                CurrentPeriodEnd = subscription.CurrentPeriodEnd,
                TrialStart = subscription.TrialStart,
                TrialEnd = subscription.TrialEnd,
                Metadata = System.Text.Json.JsonSerializer.Serialize(subscription.Metadata),
            };

            _dbContext.Subscriptions.Add(hazinaSubscription);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Call OnSubscriptionCreated callback
            if (_options.OnSubscriptionCreated != null)
            {
                await _options.OnSubscriptionCreated(new SubscriptionCreatedEventArgs
                {
                    Subscription = hazinaSubscription,
                    ServiceProvider = _serviceProvider,
                });
            }
        }
    }

    private async Task HandleSubscriptionDeletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null) return;

        _logger.LogInformation("Subscription deleted {SubscriptionId}", subscription.Id);

        // Update subscription record
        var hazinaSubscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id, cancellationToken);

        if (hazinaSubscription != null)
        {
            hazinaSubscription.Status = "cancelled";
            hazinaSubscription.CancelledAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Call OnSubscriptionCancelled callback
            if (_options.OnSubscriptionCancelled != null)
            {
                await _options.OnSubscriptionCancelled(new SubscriptionCancelledEventArgs
                {
                    Subscription = hazinaSubscription,
                    ServiceProvider = _serviceProvider,
                });
            }
        }
    }

    private async Task HandleSubscriptionUpdatedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null) return;

        _logger.LogInformation("Subscription updated {SubscriptionId}", subscription.Id);

        // Update subscription record
        var hazinaSubscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id, cancellationToken);

        if (hazinaSubscription != null)
        {
            hazinaSubscription.Status = subscription.Status;
            hazinaSubscription.CurrentPeriodStart = subscription.CurrentPeriodStart;
            hazinaSubscription.CurrentPeriodEnd = subscription.CurrentPeriodEnd;
            hazinaSubscription.CancelAtPeriodEnd = subscription.CancelAtPeriodEnd;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
