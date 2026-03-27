# Hazina.Payments.Stripe

Generic Stripe payment integration module for .NET applications. Provides a complete solution for payment processing, customer management, subscriptions, and webhook handling with built-in database tracking and idempotency.

## Features

- **Checkout Sessions** - Create Stripe Checkout sessions for one-time payments
- **Payment Intents** - Process payments directly via Stripe API
- **Customer Management** - Get-or-create pattern for seamless customer handling
- **Subscriptions** - Full subscription lifecycle management (create, cancel, update)
- **Webhook Handling** - Signature verification, idempotency, event routing
- **Database Tracking** - EF Core integration with payment/customer/subscription/webhook tracking
- **Event Callbacks** - Custom callbacks for payment success, failure, and subscription events
- **Multiple Databases** - Support for SQL Server, PostgreSQL, or in-memory testing

## Installation

```bash
dotnet add package Hazina.Payments.Stripe
```

## Quick Start

### 1. Configure Services

```csharp
using Hazina.Payments.Stripe.Extensions;

// In Program.cs or Startup.cs
builder.Services.AddHazinaStripePaymentsWithSqlServer(
    options =>
    {
        options.SecretKey = builder.Configuration["Stripe:SecretKey"];
        options.PublishableKey = builder.Configuration["Stripe:PublishableKey"];
        options.WebhookSecret = builder.Configuration["Stripe:WebhookSecret"];

        // Optional: Register event callbacks
        options.OnPaymentSucceeded = async args =>
        {
            var logger = args.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Payment succeeded: {PaymentId}", args.Payment.Id);

            // Your custom logic here (e.g., unlock content, send email)
        };

        options.OnPaymentFailed = async args =>
        {
            var logger = args.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError("Payment failed: {Reason}", args.FailureReason);
        };
    },
    connectionString: builder.Configuration.GetConnectionString("DefaultConnection")
);
```

### 2. Configure Webhook Middleware

```csharp
// In Program.cs
app.UseHazinaStripeWebhooks("/api/webhooks/stripe");
```

### 3. Apply Database Migrations

```bash
# Create migration
dotnet ef migrations add InitialCreate --project YourProject --context HazinaPaymentsDbContext

# Apply migration
dotnet ef database update --project YourProject --context HazinaPaymentsDbContext
```

Or programmatically:

```csharp
// In Program.cs (before app.Run())
await app.Services.EnsureHazinaPaymentsDatabaseAsync();
```

## Usage Examples

### Create Checkout Session

```csharp
using Hazina.Payments.Stripe.Core;
using Hazina.Payments.Stripe.Models.DTO;

public class PaymentController : ControllerBase
{
    private readonly IStripePaymentService _paymentService;

    public PaymentController(IStripePaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutRequest request)
    {
        var checkoutRequest = new CreateCheckoutSessionRequest
        {
            Amount = 9900, // $99.00 in cents
            Currency = "usd",
            ProductName = "Premium Plan",
            Description = "One month subscription",
            SuccessUrl = "https://yourapp.com/success?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = "https://yourapp.com/cancel",
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CustomerEmail = User.FindFirstValue(ClaimTypes.Email),
            Metadata = new Dictionary<string, string>
            {
                { "plan", "premium" },
                { "duration", "monthly" }
            }
        };

        var session = await _paymentService.CreateCheckoutSessionAsync(checkoutRequest);

        return Ok(new { sessionId = session.Id, url = session.Url });
    }
}
```

### Create Payment Intent (for custom UI)

```csharp
[HttpPost("payment-intent")]
public async Task<IActionResult> CreatePaymentIntent([FromBody] PaymentRequest request)
{
    var paymentRequest = new CreatePaymentIntentRequest
    {
        Amount = request.Amount,
        Currency = "usd",
        Description = request.Description,
        UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
        Metadata = new Dictionary<string, string>
        {
            { "orderId", request.OrderId }
        }
    };

    var response = await _paymentService.CreatePaymentIntentAsync(paymentRequest);

    return Ok(new { clientSecret = response.ClientSecret });
}
```

### Customer Management

```csharp
using Hazina.Payments.Stripe.Core;

public class CustomerService
{
    private readonly IStripeCustomerService _customerService;

    public CustomerService(IStripeCustomerService customerService)
    {
        _customerService = customerService;
    }

    public async Task<string> GetOrCreateCustomerIdAsync(string userId, string email, string name)
    {
        var customer = await _customerService.GetOrCreateCustomerAsync(
            userId: userId,
            email: email,
            name: name,
            metadata: new Dictionary<string, string>
            {
                { "source", "web_app" }
            }
        );

        return customer.Id;
    }
}
```

### Subscription Management

```csharp
using Hazina.Payments.Stripe.Core;

public class SubscriptionService
{
    private readonly IStripeSubscriptionService _subscriptionService;
    private readonly IStripeCustomerService _customerService;

    public async Task<string> CreateSubscriptionAsync(string userId, string email, string priceId)
    {
        // Get or create customer
        var customer = await _customerService.GetOrCreateCustomerAsync(
            userId: userId,
            email: email
        );

        // Create subscription
        var subscription = await _subscriptionService.CreateSubscriptionAsync(
            customerId: customer.Id,
            priceId: priceId,
            metadata: new Dictionary<string, string>
            {
                { "userId", userId }
            },
            trialDays: 14 // Optional 14-day trial
        );

        return subscription.Id;
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, bool immediate = false)
    {
        await _subscriptionService.CancelSubscriptionAsync(
            subscriptionId: subscriptionId,
            atPeriodEnd: !immediate
        );
    }
}
```

### Refund Payment

```csharp
[HttpPost("refund")]
public async Task<IActionResult> RefundPayment([FromBody] RefundRequest request)
{
    var refund = await _paymentService.RefundPaymentAsync(
        paymentIntentId: request.PaymentIntentId,
        amount: request.Amount, // null for full refund
        reason: request.Reason
    );

    return Ok(new { refundId = refund.Id, status = refund.Status });
}
```

### Custom Webhook Handlers

```csharp
using Hazina.Payments.Stripe.Core;
using Stripe;

public class WebhookConfiguration
{
    public static void ConfigureWebhooks(IStripeWebhookHandler handler)
    {
        // Register custom handler for specific event
        handler.RegisterEventHandler(
            "customer.subscription.updated",
            async (stripeEvent, cancellationToken) =>
            {
                var subscription = stripeEvent.Data.Object as Subscription;

                // Your custom logic here
                Console.WriteLine($"Subscription {subscription.Id} updated");
            }
        );

        // Register handler for invoice payment failed
        handler.RegisterEventHandler(
            "invoice.payment_failed",
            async (stripeEvent, cancellationToken) =>
            {
                // Send notification to user
                // Update subscription status
                // etc.
            }
        );
    }
}
```

## Configuration Options

### Database Options

**SQL Server:**
```csharp
services.AddHazinaStripePaymentsWithSqlServer(
    configureOptions: options => { /* ... */ },
    connectionString: "Server=...;Database=...;..."
);
```

**PostgreSQL:**
```csharp
services.AddHazinaStripePaymentsWithPostgreSql(
    configureOptions: options => { /* ... */ },
    connectionString: "Host=...;Database=...;..."
);
```

**Custom DbContext:**
```csharp
services.AddHazinaStripePayments(
    configureOptions: options => { /* ... */ },
    configureDbContext: dbOptions =>
    {
        dbOptions.UseSqlServer(connectionString);
        dbOptions.EnableSensitiveDataLogging(isDevelopment);
    }
);
```

**In-Memory (Testing):**
```csharp
services.AddHazinaStripePayments(
    configureOptions: options => { /* ... */ }
    // No database configuration = uses in-memory database
);
```

### Event Callbacks

All callbacks receive a service provider for dependency injection:

```csharp
options.OnPaymentSucceeded = async args =>
{
    // Access payment details
    var payment = args.Payment;
    var metadata = args.Metadata;

    // Resolve services
    var emailService = args.ServiceProvider.GetRequiredService<IEmailService>();
    var dbContext = args.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Custom logic
    await emailService.SendPaymentConfirmationAsync(payment.UserId);

    // Update application state
    var order = await dbContext.Orders.FindAsync(metadata["orderId"]);
    order.Status = "Paid";
    await dbContext.SaveChangesAsync();
};
```

## Database Schema

### HazinaPayment
- Tracks all payment transactions
- Links to Stripe payment intents and checkout sessions
- Stores amount, currency, status, metadata

### HazinaCustomer
- Maps internal user IDs to Stripe customer IDs
- One-to-many with payments and subscriptions

### HazinaSubscription
- Tracks subscription lifecycle
- Stores period dates, trial info, cancellation status

### HazinaWebhookEvent
- Idempotency log for all webhook events
- Retry tracking with exponential backoff
- Audit trail for debugging

## Webhook Events Handled

The following Stripe events are automatically processed:

- `checkout.session.completed` - Updates payment status, triggers OnPaymentSucceeded
- `payment_intent.succeeded` - Updates payment status, triggers OnPaymentSucceeded
- `payment_intent.payment_failed` - Updates payment status, triggers OnPaymentFailed
- `customer.subscription.created` - Creates subscription record, triggers OnSubscriptionCreated
- `customer.subscription.updated` - Updates subscription status
- `customer.subscription.deleted` - Marks subscription as cancelled, triggers OnSubscriptionCancelled

Additional events can be handled via `RegisterEventHandler()`.

## Security

### Webhook Signature Verification

All webhooks are automatically verified using your webhook secret. Invalid signatures are rejected with a 400 response.

### Idempotency

Webhook events are logged to the database with unique event IDs. Duplicate events are automatically detected and skipped to prevent double-processing.

### Retry Logic

Failed webhook events are marked for retry with exponential backoff (2^n minutes).

## Testing

### Unit Testing

```csharp
// Use in-memory database for testing
services.AddHazinaStripePayments(
    options =>
    {
        options.SecretKey = "sk_test_...";
        options.WebhookSecret = "whsec_test_...";
    }
    // No database configuration = in-memory
);
```

### Integration Testing

Use Stripe test mode keys and test payment methods:
- Card: `4242 4242 4242 4242`
- Expiry: Any future date
- CVC: Any 3 digits

## Requirements

- .NET 9.0 or higher
- Entity Framework Core 9.0
- Stripe.net 45.23.0 or higher

## License

MIT

## Support

For issues and questions, please open an issue on [GitHub](https://github.com/martiendejong/Hazina/issues).

## Contributing

Contributions are welcome! Please submit pull requests to the [Hazina repository](https://github.com/martiendejong/Hazina).
