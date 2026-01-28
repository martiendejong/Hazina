namespace Hazina.API.Generic.Entities;

/// <summary>
/// Base interface for all entities managed by the generic API.
/// Entities must have a unique identifier.
/// </summary>
/// <typeparam name="TKey">The type of the primary key (usually Guid, int, or string)</typeparam>
public interface IEntity<TKey> where TKey : IEquatable<TKey>
{
    /// <summary>
    /// The unique identifier for this entity
    /// </summary>
    TKey Id { get; set; }
}

/// <summary>
/// Convenience interface for entities using Guid as primary key (most common case)
/// </summary>
public interface IEntity : IEntity<Guid>
{
}

/// <summary>
/// Interface for entities that track creation and modification timestamps
/// </summary>
public interface IHasTimestamps
{
    /// <summary>
    /// When the entity was created (UTC)
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the entity was last modified (UTC)
    /// </summary>
    DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Interface for entities that support soft deletion
/// </summary>
public interface ISoftDelete
{
    /// <summary>
    /// Whether the entity has been soft-deleted
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// When the entity was deleted (UTC), if deleted
    /// </summary>
    DateTime? DeletedAt { get; set; }
}

/// <summary>
/// Interface for entities that belong to a tenant (multi-tenant support)
/// </summary>
public interface IHasTenant
{
    /// <summary>
    /// The tenant identifier this entity belongs to
    /// </summary>
    Guid TenantId { get; set; }
}

/// <summary>
/// Interface for entities that belong to a user
/// </summary>
public interface IHasOwner
{
    /// <summary>
    /// The user identifier who owns this entity
    /// </summary>
    Guid OwnerId { get; set; }
}

/// <summary>
/// Interface for entities that can be searched via text
/// </summary>
public interface ISearchable
{
    /// <summary>
    /// Gets the searchable text representation of this entity.
    /// Used for full-text search and RAG embeddings.
    /// </summary>
    string GetSearchableText();
}

/// <summary>
/// Interface for entities that support embedding/RAG integration
/// </summary>
public interface IEmbeddable : ISearchable
{
    /// <summary>
    /// The embedding vector for this entity (if computed)
    /// </summary>
    float[]? Embedding { get; set; }

    /// <summary>
    /// When the embedding was last computed (UTC)
    /// </summary>
    DateTime? EmbeddingComputedAt { get; set; }
}
