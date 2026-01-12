using Hazina.Brain.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Hazina.Brain.Infrastructure;

/// <summary>
/// EF Core DbContext for Hazina.Brain persistent memory storage.
/// Supports both SQLite (development/small deployments) and PostgreSQL (production/large scale).
/// </summary>
public class BrainDbContext : DbContext
{
    public DbSet<MemoryEpisode> Episodes { get; set; } = null!;
    public DbSet<MemoryFact> Facts { get; set; } = null!;
    public DbSet<ConceptNode> ConceptNodes { get; set; } = null!;
    public DbSet<ConceptEdge> ConceptEdges { get; set; } = null!;

    public BrainDbContext(DbContextOptions<BrainDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MemoryEpisode configuration
        modelBuilder.Entity<MemoryEpisode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StoreId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.AgentId).HasMaxLength(255);
            entity.Property(e => e.UserId).HasMaxLength(255);
            entity.Property(e => e.RawInput).IsRequired();
            entity.Property(e => e.RawOutput).IsRequired();

            // Composite index for efficient querying
            entity.HasIndex(e => new { e.StoreId, e.CreatedAt });
            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.UserId);

            // Embedding storage (JSON for SQLite compatibility)
            entity.Property(e => e.Embedding)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<float[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<float>());

            // Tags storage (JSON array)
            entity.Property(e => e.Tags)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<string>());
        });

        // MemoryFact configuration
        modelBuilder.Entity<MemoryFact>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StoreId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.AgentId).HasMaxLength(255);
            entity.Property(e => e.UserId).HasMaxLength(255);
            entity.Property(e => e.Text).IsRequired();

            // Composite index for efficient querying
            entity.HasIndex(e => new { e.StoreId, e.Weight });
            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.UserId);

            // Embedding storage (JSON for SQLite compatibility)
            entity.Property(e => e.Embedding)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<float[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<float>());
        });

        // ConceptNode configuration
        modelBuilder.Entity<ConceptNode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StoreId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Type).HasMaxLength(100);

            entity.HasIndex(e => new { e.StoreId, e.Label });

            // Embedding storage
            entity.Property(e => e.Embedding)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<float[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<float>());

            // Metadata storage (JSON)
            entity.Property(e => e.Metadata)
                .HasConversion(
                    v => v != null ? JsonSerializer.Serialize(v, (JsonSerializerOptions?)null) : null,
                    v => v != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) : null);
        });

        // ConceptEdge configuration
        modelBuilder.Entity<ConceptEdge>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Relation).IsRequired().HasMaxLength(100);

            entity.HasIndex(e => e.SourceId);
            entity.HasIndex(e => e.TargetId);

            // Relationships
            entity.HasOne(e => e.Source)
                .WithMany()
                .HasForeignKey(e => e.SourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Target)
                .WithMany()
                .HasForeignKey(e => e.TargetId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
