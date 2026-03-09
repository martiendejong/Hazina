using Hazina.Auth.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hazina.Auth.Identity.Data;

/// <summary>
/// Application DbContext with Identity support
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Custom configurations for ApplicationUser
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.FullName).HasMaxLength(200);
            entity.Property(e => e.ProfilePictureUrl).HasMaxLength(500);
            entity.Property(e => e.OAuthProvider).HasMaxLength(50);
            entity.Property(e => e.OAuthProviderId).HasMaxLength(200);
            entity.Property(e => e.RefreshToken).HasMaxLength(500);

            // Index on OAuth provider + provider ID for faster lookups
            entity.HasIndex(e => new { e.OAuthProvider, e.OAuthProviderId })
                  .IsUnique()
                  .HasFilter("[OAuthProvider] IS NOT NULL AND [OAuthProviderId] IS NOT NULL");
        });
    }
}
