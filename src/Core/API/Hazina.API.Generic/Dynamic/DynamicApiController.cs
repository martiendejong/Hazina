using Hazina.API.Generic.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Swashbuckle.AspNetCore.Annotations;

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
    /// <remarks>
    /// **IMPORTANT:** Use `/api/{entity}/schema` to see required fields and data types for each entity.
    ///
    /// **Example for Document entity:**
    /// ```json
    /// {
    ///   "title": "My Document Title",
    ///   "content": "Document content goes here...",
    ///   "category": "Tutorial",
    ///   "tags": ["getting-started", "api"],
    ///   "author": "John Doe"
    /// }
    /// ```
    ///
    /// **Example for Task entity:**
    /// ```json
    /// {
    ///   "title": "Complete project documentation",
    ///   "description": "Write comprehensive API docs",
    ///   "priority": "high",
    ///   "dueDate": "2026-01-30T12:00:00Z",
    ///   "completed": false
    /// }
    /// ```
    ///
    /// **Example for Contact entity:**
    /// ```json
    /// {
    ///   "firstName": "Jane",
    ///   "lastName": "Smith",
    ///   "email": "jane.smith@example.com",
    ///   "phone": "+1234567890",
    ///   "company": "Acme Corp",
    ///   "notes": "Met at conference"
    /// }
    /// ```
    ///
    /// **Tips:**
    /// - Don't include `id`, `createdAt`, `updatedAt`, or `isDeleted` - these are auto-generated
    /// - Only required fields must be present (check `/api/{entity}/schema`)
    /// - JSON fields (like `tags`) accept arrays or objects
    /// - DateTime fields should use ISO 8601 format
    /// </remarks>
    /// <param name="entity">Entity type (e.g., 'Document', 'Task', 'Contact')</param>
    /// <param name="body">JSON object with entity data</param>
    /// <response code="201">Entity created successfully</response>
    /// <response code="400">Invalid request body or validation error</response>
    /// <response code="404">Entity type not found</response>
    [HttpPost]
    [SwaggerOperation(
        Summary = "Create a new entity",
        Description = "Creates a new entity of the specified type. Use GET /api/{entity}/schema to see required fields.",
        Tags = new[] { "Dynamic Entities" }
    )]
    [SwaggerResponse(201, "Entity created successfully", typeof(Dictionary<string, object>))]
    [SwaggerResponse(400, "Invalid request body or validation error")]
    [SwaggerResponse(404, "Entity type not found")]
    [Produces("application/json")]
    [Consumes("application/json")]
    public async Task<ActionResult<DynamicEntity>> Create(
        [SwaggerParameter("Entity type (e.g., 'Document', 'Task', 'Contact')", Required = true)] string entity,
        [FromBody][SwaggerRequestBody("JSON object with entity data")] JsonDocument body)
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
    /// <remarks>
    /// Returns complete schema information for the entity including:
    /// - Field names and types
    /// - Required vs optional fields
    /// - Validation rules (max length, min/max values)
    /// - Searchable fields
    /// - Enabled features (CRUD, search, embedding, etc.)
    ///
    /// **Use this endpoint to discover what fields to send when creating/updating entities.**
    /// </remarks>
    /// <param name="entity">Entity type (e.g., 'Document', 'Task', 'Contact')</param>
    /// <response code="200">Schema returned successfully</response>
    /// <response code="404">Entity type not found</response>
    [HttpGet("schema")]
    [SwaggerOperation(
        Summary = "Get entity schema",
        Description = "Returns field definitions, validation rules, and features for the entity type",
        Tags = new[] { "Entity Metadata" }
    )]
    [SwaggerResponse(200, "Schema returned successfully")]
    [SwaggerResponse(404, "Entity type not found")]
    [Produces("application/json")]
    public ActionResult<object> GetSchema(
        [SwaggerParameter("Entity type (e.g., 'Document', 'Task', 'Contact')", Required = true)] string entity)
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
    /// <remarks>
    /// Returns a list of all entity types defined in the YAML configuration.
    ///
    /// For each entity type, you can:
    /// - View schema: `GET /api/{entity}/schema`
    /// - List items: `GET /api/{entity}`
    /// - Create: `POST /api/{entity}`
    /// - Search: `GET /api/{entity}/search?q=keyword`
    /// </remarks>
    /// <response code="200">List of entity types</response>
    [HttpGet("/api/_entities")]
    [SwaggerOperation(
        Summary = "List all entity types",
        Description = "Returns names of all entities defined in the configuration",
        Tags = new[] { "Entity Metadata" }
    )]
    [SwaggerResponse(200, "List of entity types", typeof(List<string>))]
    [Produces("application/json")]
    public ActionResult<List<string>> ListEntityTypes()
    {
        return Ok(_store.GetEntityTypes().ToList());
    }

    /// <summary>
    /// GET /api/_guide
    /// Complete API guide with schemas and examples
    /// </summary>
    /// <remarks>
    /// Returns comprehensive documentation for all entities including:
    /// - Complete field schemas
    /// - Request body examples (JSON)
    /// - Available endpoints
    /// - Validation rules
    ///
    /// **This is your one-stop guide for using the API!**
    /// </remarks>
    /// <response code="200">Complete API guide</response>
    [HttpGet("/api/_guide")]
    [SwaggerOperation(
        Summary = "Complete API documentation guide",
        Description = "Returns comprehensive guide with schemas and examples for all entities",
        Tags = new[] { "Entity Metadata" }
    )]
    [SwaggerResponse(200, "API guide with schemas and examples")]
    [Produces("application/json")]
    public ActionResult<object> GetApiGuide()
    {
        var entities = _store.GetEntityTypes()
            .Select(entityType =>
            {
                var definition = _store.GetDefinition(entityType);
                if (definition == null) return null;

                // Build example JSON
                var example = new Dictionary<string, object?>();
                foreach (var field in definition.Fields.Where(f => f.Required || f.Name == "title" || f.Name == "name"))
                {
                    example[field.Name] = field.Type switch
                    {
                        Configuration.FieldType.String => $"Example {field.Name}",
                        Configuration.FieldType.Text => $"This is example {field.Name} content...",
                        Configuration.FieldType.Int => 42,
                        Configuration.FieldType.Decimal => 99.99m,
                        Configuration.FieldType.Bool => false,
                        Configuration.FieldType.DateTime => DateTime.UtcNow.ToString("o"),
                        Configuration.FieldType.Json => new[] { "example", "tags" },
                        Configuration.FieldType.Guid => Guid.NewGuid(),
                        Configuration.FieldType.Enum => "value",
                        _ => $"example-{field.Name}"
                    };
                }

                return new
                {
                    entity = entityType,
                    pluralName = definition.GetPluralName(),
                    description = definition.Description,
                    endpoints = new
                    {
                        list = $"GET /api/{entityType}?page=1&pageSize=20",
                        getById = $"GET /api/{entityType}/{{id}}",
                        create = $"POST /api/{entityType}",
                        update = $"PUT /api/{entityType}/{{id}}",
                        delete = $"DELETE /api/{entityType}/{{id}}",
                        count = $"GET /api/{entityType}/count",
                        search = $"GET /api/{entityType}/search?q=keyword&limit=20",
                        schema = $"GET /api/{entityType}/schema"
                    },
                    fields = definition.Fields.Select(f => new
                    {
                        name = f.Name,
                        type = f.Type.ToString().ToLowerInvariant(),
                        required = f.Required,
                        maxLength = f.MaxLength,
                        minValue = f.MinValue,
                        maxValue = f.MaxValue,
                        description = f.Description,
                        searchable = f.Searchable,
                        indexed = f.Indexed
                    }),
                    exampleRequest = example,
                    features = definition.Features
                };
            })
            .Where(e => e != null)
            .ToList();

        return Ok(new
        {
            title = "Zero-Code Dynamic API Guide",
            description = "Auto-generated API for entities defined in YAML configuration",
            version = "1.0",
            totalEntities = entities.Count,
            entities
        });
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
