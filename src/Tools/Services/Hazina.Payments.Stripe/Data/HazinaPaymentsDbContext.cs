using Hazina.Payments.Stripe.Models;
using Microsoft.EntityFrameworkCore;

namespace Hazina.Payments.Stripe.Data;

/// <summary>
/// DbContext for Hazina payment tracking
/// </summary>
public class HazinaPaymentsDbContext : DbContext
{
    public HazinaPaymentsDbContext(DbContextOptions<HazinaPaymentsDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Payment records
    /// </summary>
    public DbSet<HazinaPayment> Payments { get; set; } = null!;

    /// <summary>
    /// Customer records
    /// </summary>
    public DbSet<HazinaCustomer> Customers { get; set; } = null!;

    /// <summary>
    /// Subscription records
    /// </summary>
    public DbSet<HazinaSubscription> Subscriptions { get; set; } = null!;

    /// <summary>
    /// Webhook event log
    /// </summary>
    public DbSet<HazinaWebhookEvent> WebhookEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure HazinaPayment
        modelBuilder.Entity<HazinaPayment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.StripePaymentIntentId)
                .IsUnique()
                .HasFilter("[StripePaymentIntentId] IS NOT NULL");

            entity.HasIndex(e => e.StripeSessionId)
                .IsUnique()
                .HasFilter("[StripeSessionId] IS NOT NULL");

            entity.HasIndex(e => e.UserId);

            entity.HasIndex(e => e.Status);

            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsRequired();

            // Foreign key to customer
            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Payments)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure HazinaCustomer
        modelBuilder.Entity<HazinaCustomer>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId)
                .IsUnique();

            entity.HasIndex(e => e.StripeCustomerId)
                .IsUnique();

            entity.HasIndex(e => e.Email);

            entity.Property(e => e.UserId)
                .HasMaxLength(450)
                .IsRequired();

            entity.Property(e => e.StripeCustomerId)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasMaxLength(255);
        });

        // Configure HazinaSubscription
        modelBuilder.Entity<HazinaSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.StripeSubscriptionId)
                .IsUnique();

            entity.HasIndex(e => e.StripeCustomerId);

            entity.HasIndex(e => e.UserId);

            entity.HasIndex(e => e.Status);

            entity.HasIndex(e => e.CurrentPeriodEnd);

            entity.Property(e => e.StripeSubscriptionId)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.StripeCustomerId)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.StripePriceId)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.PlanType)
                .HasMaxLength(100);

            // Foreign key to customer
            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Subscriptions)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure HazinaWebhookEvent
        modelBuilder.Entity<HazinaWebhookEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.StripeEventId)
                .IsUnique();

            entity.HasIndex(e => e.EventType);

            entity.HasIndex(e => e.Status);

            entity.HasIndex(e => e.ProcessedAt);

            entity.HasIndex(e => e.NextRetryAt)
                .HasFilter("[NextRetryAt] IS NOT NULL");

            entity.Property(e => e.StripeEventId)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.EventType)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsRequired();
        });
    }
}
