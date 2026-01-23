using System.ComponentModel.DataAnnotations;

namespace Hazina.API.Generic.Entities;

/// <summary>
/// Base class for entities with Guid primary key.
/// Inherit from this for standard entities.
/// </summary>
public abstract class EntityBase : IEntity, IHasTimestamps
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Base class for entities with soft delete support.
/// Use when you need to keep deleted records in the database.
/// </summary>
public abstract class SoftDeleteEntityBase : EntityBase, ISoftDelete
{
    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}

/// <summary>
/// Base class for multi-tenant entities.
/// Use when the application supports multiple tenants.
/// </summary>
public abstract class TenantEntityBase : SoftDeleteEntityBase, IHasTenant
{
    public Guid TenantId { get; set; }
}

/// <summary>
/// Base class for user-owned entities.
/// Use when entities belong to specific users.
/// </summary>
public abstract class OwnedEntityBase : SoftDeleteEntityBase, IHasOwner
{
    public Guid OwnerId { get; set; }
}

/// <summary>
/// Full-featured base class with tenant, owner, soft delete, and timestamps.
/// Use this when you need all common features.
/// </summary>
public abstract class FullEntityBase : EntityBase, ISoftDelete, IHasTenant, IHasOwner
{
    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid TenantId { get; set; }

    public Guid OwnerId { get; set; }
}

/// <summary>
/// Base class for RAG-enabled entities.
/// Use when entities should be searchable via embeddings.
/// </summary>
public abstract class EmbeddableEntityBase : SoftDeleteEntityBase, IEmbeddable
{
    public float[]? Embedding { get; set; }

    public DateTime? EmbeddingComputedAt { get; set; }

    /// <summary>
    /// Override this to provide the text that will be embedded for RAG search.
    /// </summary>
    public abstract string GetSearchableText();
}
