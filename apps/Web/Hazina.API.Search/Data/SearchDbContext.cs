using Hazina.API.Search.Models;
using Microsoft.EntityFrameworkCore;

namespace Hazina.API.Search.Data;

/// <summary>
/// Database context for Search API metadata storage
/// </summary>
public class SearchDbContext : DbContext
{
    public SearchDbContext(DbContextOptions<SearchDbContext> options)
        : base(options)
    {
    }

    public DbSet<RAGStore> RAGStores { get; set; } = null!;
    public DbSet<RAGStoreConfig> RAGStoreConfigs { get; set; } = null!;
    public DbSet<DocumentMetadata> Documents { get; set; } = null!;
    public DbSet<DocumentChunk> DocumentChunks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // RAGStore configuration
        modelBuilder.Entity<RAGStore>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            // One-to-one with RAGStoreConfig
            entity.HasOne(e => e.Config)
                .WithOne()
                .HasForeignKey<RAGStoreConfig>(c => c.RAGStoreId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RAGStoreConfig configuration
        modelBuilder.Entity<RAGStoreConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmbeddingModel).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LLMModel).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ChunkSize).IsRequired();
            entity.Property(e => e.ChunkOverlap).IsRequired();
            entity.Property(e => e.TopK).IsRequired();
            entity.Property(e => e.MinRelevanceScore).IsRequired();
        });

        // DocumentMetadata configuration
        modelBuilder.Entity<DocumentMetadata>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RAGStoreId);
            entity.HasIndex(e => e.OriginalFilename);
            entity.Property(e => e.OriginalFilename).IsRequired().HasMaxLength(500);
            entity.Property(e => e.MimeType).IsRequired().HasMaxLength(200);
            entity.Property(e => e.UploadedAt).IsRequired();

            // One-to-many with DocumentChunks
            entity.HasMany(e => e.Chunks)
                .WithOne()
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DocumentChunk configuration
        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DocumentId);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.ChunkIndex).IsRequired();
        });
    }
}

/// <summary>
/// Document metadata stored in SQLite
/// </summary>
public class DocumentMetadata
{
    public Guid Id { get; set; }
    public Guid RAGStoreId { get; set; }
    public string OriginalFilename { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int PageCount { get; set; }
    public int ChunkCount { get; set; }
    public DateTime UploadedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    public List<DocumentChunk> Chunks { get; set; } = new();
}
