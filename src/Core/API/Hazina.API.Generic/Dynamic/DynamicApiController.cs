using Hazina.API.Generic.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Hazina.API.Generic.Dynamic;

/// <summary>
/// Catch-all controller that handles ALL entity types defined in YAML.
/// No individual controller files needed!
///
/// Routes:
///   GET    /api/{entity}          - List all
///   GET    /api/{entity}/{id}     - Get by ID
///   POST   /api/{entity}          - Create
///   PUT    /api/{entity}/{id}     - Update
///   DELETE /api/{entity}/{id}     - Delete
///   GET    /api/{entity}/count    - Count
///   GET    /api/{entity}/search   - Search
///   GET    /api/{entity}/schema   - Get field definitions
/// </summary>
[ApiController]
[Route("api/{entity}")]
public class DynamicApiController : ControllerBase
{
    private readonly DynamicEntityStore _store;
    private readonly ILogger<DynamicApiController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public DynamicApiController(DynamicEntityStore store, ILogger<DynamicApiController> logger)
    {
        _store = store;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// GET /api/{entity}
    /// List all entities with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<DynamicEntity>>> GetAll(
        string entity,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var items = await _store.GetAllAsync(entity, page, pageSize);
            var totalCount = await _store.CountAsync(entity);

            return Ok(new PagedResult<DynamicEntity>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Entity type not found",
                Detail = ex.Message,
                Status = 404
            });
        }
    }

    /// <summary>
    /// GET /api/{entity}/{id}
    /// Get entity by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DynamicEntity>> GetById(string entity, Guid id)
    {
        try
        {
            var result = await _store.GetByIdAsync(entity, id);
            if (result == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Entity not found",
                    Detail = $"No {entity} found with ID {id}",
                    Status = 404
                });
            }

            return Ok(result.ToDictionary());
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Entity type not found",
                Detail = ex.Message,
                Status = 404
            });
        }
    }

    /// <summary>
    /// POST /api/{entity}
    /// Create a new entity
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DynamicEntity>> Create(string entity, [FromBody] JsonDocument body)
    {
        try
        {
            var data = new Dictionary<string, object?>();
            foreach (var prop in body.RootElement.EnumerateObject())
            {
                data[prop.Name] = ConvertJsonElement(prop.Value);
            }

            var dynamicEntity = DynamicEntity.FromDictionary(data, entity);
            var created = await _store.CreateAsync(entity, dynamicEntity);

            _logger.LogInformation("Created {Entity} with ID {Id}", entity, created.Id);

            return CreatedAtAction(
                nameof(GetById),
                new { entity, id = created.Id },
                created.ToDictionary());
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Entity type not found",
                Detail = ex.Message,
                Status = 404
            });
        }
    }

    /// <summary>
    /// PUT /api/{entity}/{id}
    /// Update an entity
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DynamicEntity>> Update(string entity, Guid id, [FromBody] JsonDocument body)
    {
        try
        {
            var data = new Dictionary<string, object?>();
            foreach (var prop in body.RootElement.EnumerateObject())
            {
                data[prop.Name] = ConvertJsonElement(prop.Value);
            }

            var dynamicEntity = DynamicEntity.FromDictionary(data, entity);
            var updated = await _store.UpdateAsync(entity, id, dynamicEntity);

            if (updated == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Entity not found",
                    Detail = $"No {entity} found with ID {id}",
                    Status = 404
                });
            }

            _logger.LogInformation("Updated {Entity} with ID {Id}", entity, id);
            return Ok(updated.ToDictionary());
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Entity type not found",
                Detail = ex.Message,
                Status = 404
            });
        }
    }

    /// <summary>
    /// DELETE /api/{entity}/{id}
    /// Delete an entity (soft delete by default)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(string entity, Guid id, [FromQuery] bool hard = false)
    {
        try
        {
            var deleted = await _store.DeleteAsync(entity, id, hard);
            if (!deleted)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Entity not found",
                    Detail = $"No {entity} found with ID {id}",
                    Status = 404
                });
            }

            _logger.LogInformation("Deleted {Entity} with ID {Id} (hard={Hard})", entity, id, hard);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Entity type not found",
                Detail = ex.Message,
                Status = 404
            });
        }
    }

    /// <summary>
    /// GET /api/{entity}/count
    /// Get count of entities
    /// </summary>
    [HttpGet("count")]
    public async Task<ActionResult<CountResult>> Count(string entity)
    {
        try
        {
            var count = await _store.CountAsync(entity);
            return Ok(new CountResult { Count = count });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Entity type not found",
                Detail = ex.Message,
                Status = 404
            });
        }
    }

    /// <summary>
    /// GET /api/{entity}/search?q=term
    /// Search entities
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<DynamicEntity>>> Search(
        string entity,
        [FromQuery] string q,
        [FromQuery] int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid query",
                Detail = "Search query 'q' is required",
                Status = 400
            });
        }

        try
        {
            var results = await _store.SearchAsync(entity, q, limit);
            return Ok(results.Select(e => e.ToDictionary()).ToList());
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Entity type not found",
                Detail = ex.Message,
                Status = 404
            });
        }
    }

    /// <summary>
    /// GET /api/{entity}/schema
    /// Get the field definitions for this entity type
    /// </summary>
    [HttpGet("schema")]
    public ActionResult<object> GetSchema(string entity)
    {
        var definition = _store.GetDefinition(entity);
        if (definition == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Entity type not found",
                Detail = $"Unknown entity type: {entity}. Available types: {string.Join(", ", _store.GetEntityTypes())}",
                Status = 404
            });
        }

        return Ok(new
        {
            name = definition.Name,
            pluralName = definition.GetPluralName(),
            description = definition.Description,
            fields = definition.Fields.Select(f => new
            {
                name = f.Name,
                type = f.Type.ToString().ToLowerInvariant(),
                required = f.Required,
                maxLength = f.MaxLength,
                description = f.Description,
                searchable = f.Searchable
            }),
            features = new
            {
                definition.Features.Crud,
                definition.Features.Search,
                definition.Features.Embedding,
                definition.Features.SoftDelete,
                definition.Features.Pagination
            }
        });
    }

    /// <summary>
    /// GET /api/_entities
    /// List all available entity types
    /// </summary>
    [HttpGet("/api/_entities")]
    public ActionResult<List<string>> ListEntityTypes()
    {
        return Ok(_store.GetEntityTypes().ToList());
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String when Guid.TryParse(element.GetString(), out var guid) => guid,
            JsonValueKind.String when DateTime.TryParse(element.GetString(), out var dt) => dt,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => element.GetRawText()
        };
    }
}
