using Hazina.Demo.GenericApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hazina.Demo.GenericApi.Data;

/// <summary>
/// Database context for the demo application.
/// Uses SQLite for simplicity - no external database required.
/// </summary>
public class DemoDbContext : DbContext
{
    public DemoDbContext(DbContextOptions<DemoDbContext> options) : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Document configuration
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => e.DocumentType);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.IsDeleted);

            // Store embedding as JSON (SQLite doesn't support arrays natively)
            entity.Property(e => e.Embedding)
                .HasConversion(
                    v => v == null ? null : string.Join(",", v),
                    v => v == null ? null : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(float.Parse).ToArray()
                );
        });

        // Note configuration
        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsPinned);
        });

        // Tag configuration
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Seed some example data
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        var now = DateTime.UtcNow;

        // Seed tags
        modelBuilder.Entity<Tag>().HasData(
            new Tag { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "AI", Color = "#3B82F6", UsageCount = 0, CreatedAt = now },
            new Tag { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Tutorial", Color = "#10B981", UsageCount = 0, CreatedAt = now },
            new Tag { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Documentation", Color = "#8B5CF6", UsageCount = 0, CreatedAt = now }
        );

        // Seed example documents
        modelBuilder.Entity<Document>().HasData(
            new Document
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Title = "Introduction to Hazina Framework",
                Content = "Hazina is a comprehensive framework for building AI-powered applications. It provides tools for LLM integration, RAG (Retrieval-Augmented Generation), embedding storage, and more. The framework is designed to be modular and extensible.",
                DocumentType = "article",
                Tags = "AI, Framework, Documentation",
                Author = "Hazina Team",
                CreatedAt = now
            },
            new Document
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Title = "Building REST APIs with Hazina.API.Generic",
                Content = "The Hazina.API.Generic library provides a convention-over-configuration approach to building REST APIs. Instead of writing boilerplate CRUD code for each entity, you can inherit from GenericEntityController and get all standard operations automatically. This dramatically reduces code and ensures consistency.",
                DocumentType = "tutorial",
                Tags = "API, Tutorial, Generic",
                Author = "Claude",
                CreatedAt = now
            },
            new Document
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Title = "Semantic Search with Embeddings",
                Content = "Semantic search allows finding documents based on meaning rather than exact keyword matches. By converting text to vector embeddings using models like OpenAI's text-embedding-ada-002, you can find similar documents even when they use different words. This is the foundation of RAG systems.",
                DocumentType = "article",
                Tags = "AI, Search, Embeddings, RAG",
                Author = "Hazina Team",
                CreatedAt = now
            }
        );

        // Seed example notes
        modelBuilder.Entity<Note>().HasData(
            new Note
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                Title = "Remember to update docs",
                Content = "The API documentation needs to be updated after the Generic API release.",
                Category = "Work",
                IsPinned = true,
                CreatedAt = now
            },
            new Note
            {
                Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                Title = "Meeting notes",
                Content = "Discussed the Kenya team training schedule. Focus on YAML entity definitions first.",
                Category = "Meetings",
                IsPinned = false,
                CreatedAt = now
            }
        );
    }
}
