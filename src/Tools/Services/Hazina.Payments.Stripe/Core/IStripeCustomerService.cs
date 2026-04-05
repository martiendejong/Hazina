using Stripe;

namespace Hazina.Payments.Stripe.Core;

/// <summary>
/// Service for managing Stripe customers
/// </summary>
public interface IStripeCustomerService
{
    /// <summary>
    /// Gets existing customer or creates new one
    /// </summary>
    /// <param name="userId">Internal user ID</param>
    /// <param name="email">Customer email</param>
    /// <param name="name">Customer name (optional)</param>
    /// <param name="metadata">Additional metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stripe customer</returns>
    Task<Customer> GetOrCreateCustomerAsync(
        string userId,
        string email,
        string? name = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing customer
    /// </summary>
    /// <param name="customerId">Stripe customer ID</param>
    /// <param name="email">New email (optional)</param>
    /// <param name="name">New name (optional)</param>
    /// <param name="metadata">Updated metadata (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated customer</returns>
    Task<Customer> UpdateCustomerAsync(
        string customerId,
        string? email = null,
        string? name = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets customer by Stripe customer ID
    /// </summary>
    /// <param name="customerId">Stripe customer ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Customer or null if not found</returns>
    Task<Customer?> GetCustomerByIdAsync(
        string customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets customer by internal user ID
    /// </summary>
    /// <param name="userId">Internal user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Customer or null if not found</returns>
    Task<Customer?> GetCustomerByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
