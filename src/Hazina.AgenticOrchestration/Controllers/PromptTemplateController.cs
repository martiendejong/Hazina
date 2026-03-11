using Hazina.AgenticOrchestration.Models;
using Hazina.AgenticOrchestration.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hazina.AgenticOrchestration.Controllers;

/// <summary>
/// API endpoints for managing predefined prompt templates.
/// Provides CRUD operations and template usage tracking.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PromptTemplateController : ControllerBase
{
    private readonly IPromptTemplateService _templateService;
    private readonly ILogger<PromptTemplateController> _logger;

    public PromptTemplateController(
        IPromptTemplateService templateService,
        ILogger<PromptTemplateController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// Get all templates, optionally filtered by category
    /// </summary>
    /// <param name="category">Optional category filter</param>
    /// <returns>List of prompt templates</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PromptTemplate>>> GetAllTemplates([FromQuery] string? category = null)
    {
        var templates = await _templateService.GetAllTemplatesAsync(category);
        return Ok(templates);
    }

    /// <summary>
    /// Get a specific template by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PromptTemplate>> GetTemplate(string id)
    {
        var template = await _templateService.GetTemplateAsync(id);
        if (template == null)
        {
            return NotFound(new { Message = $"Template with ID {id} not found" });
        }

        return Ok(template);
    }

    /// <summary>
    /// Get all favorite templates
    /// </summary>
    [HttpGet("favorites")]
    public async Task<ActionResult<IEnumerable<PromptTemplate>>> GetFavorites()
    {
        var favorites = await _templateService.GetFavoriteTemplatesAsync();
        return Ok(favorites);
    }

    /// <summary>
    /// Get recently used templates
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<IEnumerable<PromptTemplate>>> GetRecent([FromQuery] int limit = 5)
    {
        var recent = await _templateService.GetRecentTemplatesAsync(limit);
        return Ok(recent);
    }

    /// <summary>
    /// Get all unique categories
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories()
    {
        var categories = await _templateService.GetCategoriesAsync();
        return Ok(categories);
    }

    /// <summary>
    /// Create a new template
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PromptTemplate>> CreateTemplate([FromBody] PromptTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Name))
        {
            return BadRequest(new { Message = "Template name is required" });
        }

        if (string.IsNullOrWhiteSpace(template.PromptText))
        {
            return BadRequest(new { Message = "Template prompt text is required" });
        }

        var created = await _templateService.CreateTemplateAsync(template);
        return CreatedAtAction(nameof(GetTemplate), new { id = created.Id }, created);
    }

    /// <summary>
    /// Update an existing template
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<PromptTemplate>> UpdateTemplate(string id, [FromBody] PromptTemplate template)
    {
        if (id != template.Id)
        {
            return BadRequest(new { Message = "Template ID in URL does not match template ID in body" });
        }

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            return BadRequest(new { Message = "Template name is required" });
        }

        if (string.IsNullOrWhiteSpace(template.PromptText))
        {
            return BadRequest(new { Message = "Template prompt text is required" });
        }

        try
        {
            var updated = await _templateService.UpdateTemplateAsync(template);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a template
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteTemplate(string id)
    {
        var deleted = await _templateService.DeleteTemplateAsync(id);
        if (!deleted)
        {
            return NotFound(new { Message = $"Template with ID {id} not found" });
        }

        return NoContent();
    }

    /// <summary>
    /// Record that a template was used (increment usage count)
    /// </summary>
    [HttpPost("{id}/use")]
    public async Task<ActionResult> RecordUsage(string id)
    {
        await _templateService.RecordTemplateUsageAsync(id);
        return Ok(new { Message = "Usage recorded successfully" });
    }

    /// <summary>
    /// Toggle favorite status for a template
    /// </summary>
    [HttpPost("{id}/toggle-favorite")]
    public async Task<ActionResult<bool>> ToggleFavorite(string id)
    {
        var isFavorite = await _templateService.ToggleFavoriteAsync(id);
        return Ok(new { IsFavorite = isFavorite });
    }
}
