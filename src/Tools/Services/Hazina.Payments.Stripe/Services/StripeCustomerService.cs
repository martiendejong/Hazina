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
/// Implementation of Stripe customer service
/// </summary>
public class StripeCustomerService : IStripeCustomerService
{
    private readonly HazinaStripeOptions _options;
    private readonly HazinaPaymentsDbContext _dbContext;
    private readonly ILogger<StripeCustomerService> _logger;
    private readonly CustomerService _customerService;

    public StripeCustomerService(
        IOptions<HazinaStripeOptions> options,
        HazinaPaymentsDbContext dbContext,
        ILogger<StripeCustomerService> logger)
    {
        _options = options.Value;
        _dbContext = dbContext;
        _logger = logger;

        // Validate configuration
        _options.Validate();

        // Initialize Stripe service
        _customerService = new CustomerService();
    }

    public async Task<Customer> GetOrCreateCustomerAsync(
        string userId,
        string email,
        string? name = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting or creating customer for user {UserId}", userId);

            // Check if customer already exists in database
            var existingCustomer = await _dbContext.Customers
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

            if (existingCustomer != null)
            {
                _logger.LogInformation("Found existing customer {CustomerId} for user {UserId}",
                    existingCustomer.StripeCustomerId, userId);

                // Return existing Stripe customer
                return await _customerService.GetAsync(existingCustomer.StripeCustomerId, cancellationToken: cancellationToken);
            }

            // Create new Stripe customer
            var options = new CustomerCreateOptions
            {
                Email = email,
                Name = name,
                Metadata = metadata ?? new Dictionary<string, string>(),
            };

            // Add user ID to metadata
            options.Metadata["userId"] = userId;

            var customer = await _customerService.CreateAsync(options, cancellationToken: cancellationToken);

            // Save customer record to database
            var hazinaCustomer = new HazinaCustomer
            {
                UserId = userId,
                StripeCustomerId = customer.Id,
                Email = email,
                Name = name,
                Metadata = System.Text.Json.JsonSerializer.Serialize(metadata),
            };

            _dbContext.Customers.Add(hazinaCustomer);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created customer {CustomerId} for user {UserId}", customer.Id, userId);

            return customer;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error creating customer");
            throw new InvalidOperationException($"Failed to create customer: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating customer");
            throw;
        }
    }

    public async Task<Customer> UpdateCustomerAsync(
        string customerId,
        string? email = null,
        string? name = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating customer {CustomerId}", customerId);

            var options = new CustomerUpdateOptions();

            if (!string.IsNullOrEmpty(email))
            {
                options.Email = email;
            }

            if (!string.IsNullOrEmpty(name))
            {
                options.Name = name;
            }

            if (metadata != null)
            {
                options.Metadata = metadata;
            }

            // Update customer via Stripe API
            var customer = await _customerService.UpdateAsync(customerId, options, cancellationToken: cancellationToken);

            // Update database record
            var hazinaCustomer = await _dbContext.Customers
                .FirstOrDefaultAsync(c => c.StripeCustomerId == customerId, cancellationToken);

            if (hazinaCustomer != null)
            {
                if (!string.IsNullOrEmpty(email))
                {
                    hazinaCustomer.Email = email;
                }

                if (!string.IsNullOrEmpty(name))
                {
                    hazinaCustomer.Name = name;
                }

                if (metadata != null)
                {
                    hazinaCustomer.Metadata = System.Text.Json.JsonSerializer.Serialize(metadata);
                }

                hazinaCustomer.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Updated customer {CustomerId}", customerId);

            return customer;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error updating customer");
            throw new InvalidOperationException($"Failed to update customer: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating customer");
            throw;
        }
    }

    public async Task<Customer?> GetCustomerByIdAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting customer {CustomerId}", customerId);

            var customer = await _customerService.GetAsync(customerId, cancellationToken: cancellationToken);

            _logger.LogInformation("Retrieved customer {CustomerId}", customerId);

            return customer;
        }
        catch (StripeException ex) when (ex.StripeError.Type == "invalid_request_error")
        {
            _logger.LogWarning("Customer {CustomerId} not found", customerId);
            return null;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error getting customer");
            throw new InvalidOperationException($"Failed to get customer: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting customer");
            throw;
        }
    }

    public async Task<Customer?> GetCustomerByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting customer for user {UserId}", userId);

            var hazinaCustomer = await _dbContext.Customers
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

            if (hazinaCustomer == null)
            {
                _logger.LogWarning("Customer not found for user {UserId}", userId);
                return null;
            }

            var customer = await _customerService.GetAsync(hazinaCustomer.StripeCustomerId, cancellationToken: cancellationToken);

            _logger.LogInformation("Retrieved customer {CustomerId} for user {UserId}",
                customer.Id, userId);

            return customer;
        }
        catch (StripeException ex) when (ex.StripeError.Type == "invalid_request_error")
        {
            _logger.LogWarning("Customer not found for user {UserId}", userId);
            return null;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error getting customer");
            throw new InvalidOperationException($"Failed to get customer: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting customer");
            throw;
        }
    }
}
