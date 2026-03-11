using Hazina.API.Generic.Entities;
using Hazina.Tools.Data.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.Json;

namespace Hazina.API.Generic.Controllers;

/// <summary>
/// Generic CRUD controller that provides standard REST endpoints for any entity type.
/// Inherit from this to get automatic CRUD operations, or use convention-based routing.
///
/// Example usage:
/// <code>
/// public class ProductsController : GenericEntityController&lt;Product, Guid&gt;
/// {
///     public ProductsController(IRepository&lt;Product&gt; repository) : base(repository) { }
/// }
/// </code>
///
/// This gives you:
/// - GET /api/products - List all (with pagination, filtering, sorting)
/// - GET /api/products/{id} - Get by ID
/// - POST /api/products - Create
/// - PUT /api/products/{id} - Update
/// - DELETE /api/products/{id} - Delete (soft or hard)
/// - POST /api/products/bulk - Bulk create
/// - GET /api/products/search?q=term - Full-text search
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
/// <typeparam name="TKey">The primary key type</typeparam>
[ApiController]
public abstract class GenericEntityController<TEntity, TKey> : ControllerBase
    where TEntity : class, IEntity<TKey>
    where TKey : IEquatable<TKey>
{
    protected readonly IRepository<TEntity> Repository;
    protected readonly JsonSerializerOptions JsonOptions;

    protected GenericEntityController(IRepository<TEntity> repository)
    {
        Repository = repository;
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// GET /api/{entities}
    /// List entities with optional pagination, filtering, and sorting
    /// </summary>
    [HttpGet]
    public virtual async Task<ActionResult<PagedResult<TEntity>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = false,
        [FromQuery] string? filter = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = Repository.Query();

        // Apply soft-delete filter if supported
        if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            query = query.Where(e => !((ISoftDelete)e).IsDeleted);
        }

        // Apply custom filter expression
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterExpression = BuildFilterExpression(filter);
            if (filterExpression != null)
            {
                query = query.Where(filterExpression);
            }
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = ApplySorting(query, sortBy, sortDesc);

        // Apply pagination
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<TEntity>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    /// <summary>
    /// GET /api/{entities}/{id}
    /// Get a single entity by ID
    /// </summary>
    [HttpGet("{id}")]
    public virtual async Task<ActionResult<TEntity>> GetById(TKey id)
    {
        var entity = await Repository.GetByIdAsync(id);

        if (entity == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Entity not found",
                Detail = $"No {typeof(TEntity).Name} found with ID {id}",
                Status = 404
            });
        }

        // Check soft-delete
        if (entity is ISoftDelete softDelete && softDelete.IsDeleted)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Entity not found",
                Detail = $"No {typeof(TEntity).Name} found with ID {id}",
                Status = 404
            });
        }

        return Ok(entity);
    }

    /// <summary>
    /// POST /api/{entities}
    /// Create a new entity
    /// </summary>
    [HttpPost]
    public virtual async Task<ActionResult<TEntity>> Create([FromBody] TEntity entity)
    {
        // Set timestamps if supported
        if (entity is IHasTimestamps timestamped)
        {
            timestamped.CreatedAt = DateTime.UtcNow;
            timestamped.UpdatedAt = null;
        }

        // Ensure new ID
        if (entity.Id.Equals(default(TKey)))
        {
            if (typeof(TKey) == typeof(Guid))
            {
                entity.Id = (TKey)(object)Guid.NewGuid();
            }
        }

        await Repository.AddAsync(entity);
        await Repository.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    /// <summary>
    /// PUT /api/{entities}/{id}
    /// Update an existing entity
    /// </summary>
    [HttpPut("{id}")]
    public virtual async Task<ActionResult<TEntity>> Update(TKey id, [FromBody] TEntity entity)
    {
        if (!id.Equals(entity.Id))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ID mismatch",
                Detail = "The ID in the URL does not match the ID in the request body",
                Status = 400
            });
        }

        var existing = await Repository.GetByIdAsync(id);
        if (existing == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Entity not found",
                Detail = $"No {typeof(TEntity).Name} found with ID {id}",
                Status = 404
            });
        }

        // Preserve created timestamp
        if (entity is IHasTimestamps timestamped && existing is IHasTimestamps existingTimestamped)
        {
            timestamped.CreatedAt = existingTimestamped.CreatedAt;
            timestamped.UpdatedAt = DateTime.UtcNow;
        }

        await Repository.UpdateAsync(entity);
        await Repository.SaveChangesAsync();

        return Ok(entity);
    }

    /// <summary>
    /// PATCH /api/{entities}/{id}
    /// Partial update of an entity
    /// </summary>
    [HttpPatch("{id}")]
    public virtual async Task<ActionResult<TEntity>> PartialUpdate(TKey id, [FromBody] JsonDocument patchDoc)
    {
        var existing = await Repository.GetByIdAsync(id);
        if (existing == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Entity not found",
                Detail = $"No {typeof(TEntity).Name} found with ID {id}",
                Status = 404
            });
        }

        // Apply patch
        var entityJson = JsonSerializer.Serialize(existing, JsonOptions);
        using var originalDoc = JsonDocument.Parse(entityJson);

        var mergedJson = MergeJsonDocuments(originalDoc, patchDoc);
        var updated = JsonSerializer.Deserialize<TEntity>(mergedJson, JsonOptions);

        if (updated == null)
        {
            return BadRequest("Failed to apply patch");
        }

        // Ensure ID is preserved
        updated.Id = id;

        // Update timestamp
        if (updated is IHasTimestamps timestamped)
        {
            timestamped.UpdatedAt = DateTime.UtcNow;
        }

        await Repository.UpdateAsync(updated);
        await Repository.SaveChangesAsync();

        return Ok(updated);
    }

    /// <summary>
    /// DELETE /api/{entities}/{id}
    /// Delete an entity (soft or hard delete based on entity type)
    /// </summary>
    [HttpDelete("{id}")]
    public virtual async Task<IActionResult> Delete(TKey id, [FromQuery] bool hard = false)
    {
        var entity = await Repository.GetByIdAsync(id);
        if (entity == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Entity not found",
                Detail = $"No {typeof(TEntity).Name} found with ID {id}",
                Status = 404
            });
        }

        // Soft delete if supported and not forcing hard delete
        if (!hard && entity is ISoftDelete softDelete)
        {
            softDelete.IsDeleted = true;
            softDelete.DeletedAt = DateTime.UtcNow;
            await Repository.UpdateAsync(entity);
        }
        else
        {
            await Repository.DeleteAsync(entity);
        }

        await Repository.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// POST /api/{entities}/bulk
    /// Create multiple entities at once
    /// </summary>
    [HttpPost("bulk")]
    public virtual async Task<ActionResult<BulkResult>> BulkCreate([FromBody] List<TEntity> entities)
    {
        var created = new List<TEntity>();
        var errors = new List<BulkError>();

        for (int i = 0; i < entities.Count; i++)
        {
            try
            {
                var entity = entities[i];

                if (entity is IHasTimestamps timestamped)
                {
                    timestamped.CreatedAt = DateTime.UtcNow;
                }

                if (entity.Id.Equals(default(TKey)) && typeof(TKey) == typeof(Guid))
                {
                    entity.Id = (TKey)(object)Guid.NewGuid();
                }

                await Repository.AddAsync(entity);
                created.Add(entity);
            }
            catch (Exception ex)
            {
                errors.Add(new BulkError { Index = i, Message = ex.Message });
            }
        }

        await Repository.SaveChangesAsync();

        return Ok(new BulkResult
        {
            SuccessCount = created.Count,
            ErrorCount = errors.Count,
            Errors = errors
        });
    }

    /// <summary>
    /// DELETE /api/{entities}/bulk
    /// Delete multiple entities at once
    /// </summary>
    [HttpDelete("bulk")]
    public virtual async Task<ActionResult<BulkResult>> BulkDelete([FromBody] List<TKey> ids, [FromQuery] bool hard = false)
    {
        var deleted = 0;
        var errors = new List<BulkError>();

        for (int i = 0; i < ids.Count; i++)
        {
            try
            {
                var entity = await Repository.GetByIdAsync(ids[i]);
                if (entity != null)
                {
                    if (!hard && entity is ISoftDelete softDelete)
                    {
                        softDelete.IsDeleted = true;
                        softDelete.DeletedAt = DateTime.UtcNow;
                        await Repository.UpdateAsync(entity);
                    }
                    else
                    {
                        await Repository.DeleteAsync(entity);
                    }
                    deleted++;
                }
            }
            catch (Exception ex)
            {
                errors.Add(new BulkError { Index = i, Message = ex.Message });
            }
        }

        await Repository.SaveChangesAsync();

        return Ok(new BulkResult
        {
            SuccessCount = deleted,
            ErrorCount = errors.Count,
            Errors = errors
        });
    }

    /// <summary>
    /// GET /api/{entities}/count
    /// Get total count of entities
    /// </summary>
    [HttpGet("count")]
    public virtual async Task<ActionResult<CountResult>> Count([FromQuery] string? filter = null)
    {
        var query = Repository.Query();

        if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            query = query.Where(e => !((ISoftDelete)e).IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterExpression = BuildFilterExpression(filter);
            if (filterExpression != null)
            {
                query = query.Where(filterExpression);
            }
        }

        var count = await query.CountAsync();
        return Ok(new CountResult { Count = count });
    }

    /// <summary>
    /// GET /api/{entities}/exists/{id}
    /// Check if an entity exists
    /// </summary>
    [HttpGet("exists/{id}")]
    public virtual async Task<ActionResult<ExistsResult>> Exists(TKey id)
    {
        var entity = await Repository.GetByIdAsync(id);
        var exists = entity != null;

        if (exists && entity is ISoftDelete softDelete)
        {
            exists = !softDelete.IsDeleted;
        }

        return Ok(new ExistsResult { Exists = exists });
    }

    // ===== PROTECTED HELPER METHODS =====

    /// <summary>
    /// Override to customize filter expression building
    /// </summary>
    protected virtual Expression<Func<TEntity, bool>>? BuildFilterExpression(string filter)
    {
        // Default implementation: no filtering
        // Subclasses can override to implement custom filtering
        return null;
    }

    /// <summary>
    /// Override to customize sorting
    /// </summary>
    protected virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, string? sortBy, bool sortDesc)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            // Default: sort by CreatedAt if available, otherwise by Id
            if (typeof(IHasTimestamps).IsAssignableFrom(typeof(TEntity)))
            {
                return sortDesc
                    ? query.OrderByDescending(e => ((IHasTimestamps)e).CreatedAt)
                    : query.OrderBy(e => ((IHasTimestamps)e).CreatedAt);
            }
            return query;
        }

        // Use reflection to sort by property name
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var property = typeof(TEntity).GetProperty(sortBy);
        if (property == null)
        {
            return query; // Property not found, skip sorting
        }

        var propertyAccess = Expression.MakeMemberAccess(parameter, property);
        var orderByExp = Expression.Lambda(propertyAccess, parameter);

        var methodName = sortDesc ? "OrderByDescending" : "OrderBy";
        var resultExp = Expression.Call(
            typeof(Queryable),
            methodName,
            new[] { typeof(TEntity), property.PropertyType },
            query.Expression,
            Expression.Quote(orderByExp));

        return query.Provider.CreateQuery<TEntity>(resultExp);
    }

    private static string MergeJsonDocuments(JsonDocument original, JsonDocument patch)
    {
        var merged = new Dictionary<string, JsonElement>();

        foreach (var prop in original.RootElement.EnumerateObject())
        {
            merged[prop.Name] = prop.Value;
        }

        foreach (var prop in patch.RootElement.EnumerateObject())
        {
            merged[prop.Name] = prop.Value;
        }

        return JsonSerializer.Serialize(merged);
    }
}

/// <summary>
/// Convenience class for entities with Guid primary keys
/// </summary>
public abstract class GenericEntityController<TEntity> : GenericEntityController<TEntity, Guid>
    where TEntity : class, IEntity<Guid>
{
    protected GenericEntityController(IRepository<TEntity> repository) : base(repository)
    {
    }
}
