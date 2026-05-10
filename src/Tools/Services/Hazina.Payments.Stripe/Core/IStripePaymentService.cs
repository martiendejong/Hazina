using Hazina.Payments.Stripe.Models.DTO;
using Stripe.Checkout;

namespace Hazina.Payments.Stripe.Core;

/// <summary>
/// Service for creating and managing Stripe payments
/// </summary>
public interface IStripePaymentService
{
    /// <summary>
    /// Creates a Stripe Checkout session for one-time payments
    /// </summary>
    /// <param name="request">Checkout session configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Checkout session details including URL</returns>
    Task<Session> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a payment intent for direct API payments
    /// </summary>
    /// <param name="request">Payment intent configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment intent response</returns>
    Task<PaymentIntentResponse> CreatePaymentIntentAsync(
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a refund for a completed payment
    /// </summary>
    /// <param name="paymentIntentId">Stripe payment intent ID</param>
    /// <param name="amount">Amount to refund (null = full refund)</param>
    /// <param name="reason">Refund reason</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refund details</returns>
    Task<global::Stripe.Refund> RefundPaymentAsync(
        string paymentIntentId,
        long? amount = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a checkout session by ID
    /// </summary>
    /// <param name="sessionId">Stripe session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Session details</returns>
    Task<Session> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}
