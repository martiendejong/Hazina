using Hazina.AI.Routing.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Hazina.AI.Routing.Persistence;

/// <summary>
/// EF Core DbContext for agent routing persistence.
/// Stores agent reputation data across application restarts.
/// </summary>
public class AgentRoutingDbContext : DbContext
{
    public DbSet<AgentReputation> AgentReputations { get; set; } = null!;

    public AgentRoutingDbContext(DbContextOptions<AgentRoutingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AgentReputation>(entity =>
        {
            entity.HasKey(e => new { e.AgentType, e.Category });

            entity.Property(e => e.AgentType)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.Category)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.FailurePatterns)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

            entity.Property(e => e.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(e => e.LastUpdated);
        });
    }
}
