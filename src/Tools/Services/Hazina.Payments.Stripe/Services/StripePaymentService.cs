using Hazina.Payments.Stripe.Configuration;
using Hazina.Payments.Stripe.Core;
using Hazina.Payments.Stripe.Data;
using Hazina.Payments.Stripe.Models;
using Hazina.Payments.Stripe.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Hazina.Payments.Stripe.Services;

/// <summary>
/// Implementation of Stripe payment service
/// </summary>
public class StripePaymentService : IStripePaymentService
{
    private readonly HazinaStripeOptions _options;
    private readonly HazinaPaymentsDbContext _dbContext;
    private readonly ILogger<StripePaymentService> _logger;
    private readonly SessionService _sessionService;
    private readonly PaymentIntentService _paymentIntentService;
    private readonly RefundService _refundService;

    public StripePaymentService(
        IOptions<HazinaStripeOptions> options,
        HazinaPaymentsDbContext dbContext,
        ILogger<StripePaymentService> logger)
    {
        _options = options.Value;
        _dbContext = dbContext;
        _logger = logger;

        // Validate configuration
        _options.Validate();

        // Initialize Stripe services
        _sessionService = new SessionService();
        _paymentIntentService = new PaymentIntentService();
        _refundService = new RefundService();
    }

    public async Task<Session> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating checkout session for {Amount} {Currency}",
                request.Amount, request.Currency);

            // Create line item for the session
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = request.Currency,
                            UnitAmount = request.Amount,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = request.ProductName,
                                Description = request.Description,
                            },
                        },
                        Quantity = request.Quantity,
                    },
                },
                Mode = "payment",
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                CustomerEmail = request.CustomerEmail,
                Metadata = request.Metadata ?? new Dictionary<string, string>(),
            };

            // Add user ID to metadata if provided
            if (!string.IsNullOrEmpty(request.UserId))
            {
                options.Metadata["userId"] = request.UserId;
            }

            // Create session via Stripe API
            var session = await _sessionService.CreateAsync(options, cancellationToken: cancellationToken);

            // Save payment record to database
            var payment = new HazinaPayment
            {
                UserId = request.UserId ?? string.Empty,
                StripeSessionId = session.Id,
                Amount = request.Amount * request.Quantity,
                Currency = request.Currency,
                Status = "pending",
                Metadata = System.Text.Json.JsonSerializer.Serialize(request.Metadata),
                IdempotencyKey = session.Id,
            };

            _dbContext.Payments.Add(payment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created checkout session {SessionId}", session.Id);

            return session;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error creating checkout session");
            throw new InvalidOperationException($"Failed to create checkout session: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating checkout session");
            throw;
        }
    }

    public async Task<PaymentIntentResponse> CreatePaymentIntentAsync(
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating payment intent for {Amount} {Currency}",
                request.Amount, request.Currency);

            var options = new PaymentIntentCreateOptions
            {
                Amount = request.Amount,
                Currency = request.Currency,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                },
                Description = request.Description,
                Metadata = request.Metadata ?? new Dictionary<string, string>(),
            };

            // Add customer if provided
            if (!string.IsNullOrEmpty(request.CustomerId))
            {
                options.Customer = request.CustomerId;
            }

            // Add user ID to metadata if provided
            if (!string.IsNullOrEmpty(request.UserId))
            {
                options.Metadata["userId"] = request.UserId;
            }

            // Create payment intent via Stripe API
            var paymentIntent = await _paymentIntentService.CreateAsync(options, cancellationToken: cancellationToken);

            // Save payment record to database
            var payment = new HazinaPayment
            {
                UserId = request.UserId ?? string.Empty,
                StripePaymentIntentId = paymentIntent.Id,
                Amount = request.Amount,
                Currency = request.Currency,
                Status = "pending",
                Metadata = System.Text.Json.JsonSerializer.Serialize(request.Metadata),
                IdempotencyKey = paymentIntent.Id,
            };

            _dbContext.Payments.Add(payment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created payment intent {PaymentIntentId}", paymentIntent.Id);

            return new PaymentIntentResponse
            {
                PaymentIntentId = paymentIntent.Id,
                ClientSecret = paymentIntent.ClientSecret,
                Status = paymentIntent.Status,
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error creating payment intent");
            throw new InvalidOperationException($"Failed to create payment intent: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating payment intent");
            throw;
        }
    }

    public async Task<global::Stripe.Refund> RefundPaymentAsync(
        string paymentIntentId,
        long? amount = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing refund for payment intent {PaymentIntentId}", paymentIntentId);

            var options = new RefundCreateOptions
            {
                PaymentIntent = paymentIntentId,
            };

            if (amount.HasValue)
            {
                options.Amount = amount.Value;
            }

            if (!string.IsNullOrEmpty(reason))
            {
                options.Reason = reason;
            }

            // Create refund via Stripe API
            var refund = await _refundService.CreateAsync(options, cancellationToken: cancellationToken);

            // Update payment record in database
            var payment = await _dbContext.Payments
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId, cancellationToken);

            if (payment != null)
            {
                payment.Status = amount.HasValue && amount.Value < payment.Amount ? "partially_refunded" : "refunded";
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Refund created {RefundId} for payment intent {PaymentIntentId}",
                refund.Id, paymentIntentId);

            return refund;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error processing refund");
            throw new InvalidOperationException($"Failed to process refund: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing refund");
            throw;
        }
    }

    public async Task<Session> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving checkout session {SessionId}", sessionId);

            var session = await _sessionService.GetAsync(sessionId, cancellationToken: cancellationToken);

            _logger.LogInformation("Retrieved checkout session {SessionId}", sessionId);

            return session;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error retrieving checkout session");
            throw new InvalidOperationException($"Failed to retrieve checkout session: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving checkout session");
            throw;
        }
    }
}
