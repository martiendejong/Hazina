using Hazina.AgenticOrchestration.Models;

namespace Hazina.AgenticOrchestration.Services;

/// <summary>
/// Service for managing predefined prompt templates.
/// Provides CRUD operations and persistence for terminal session prompt templates.
/// </summary>
public interface IPromptTemplateService
{
    /// <summary>
    /// Get all templates, optionally filtered by category
    /// </summary>
    Task<IEnumerable<PromptTemplate>> GetAllTemplatesAsync(string? category = null);

    /// <summary>
    /// Get a specific template by ID
    /// </summary>
    Task<PromptTemplate?> GetTemplateAsync(string id);

    /// <summary>
    /// Get favorite templates (IsFavorite = true), ordered by SortOrder
    /// </summary>
    Task<IEnumerable<PromptTemplate>> GetFavoriteTemplatesAsync();

    /// <summary>
    /// Get most recently used templates (limit to top N)
    /// </summary>
    Task<IEnumerable<PromptTemplate>> GetRecentTemplatesAsync(int limit = 5);

    /// <summary>
    /// Create a new template
    /// </summary>
    Task<PromptTemplate> CreateTemplateAsync(PromptTemplate template);

    /// <summary>
    /// Update an existing template
    /// </summary>
    Task<PromptTemplate> UpdateTemplateAsync(PromptTemplate template);

    /// <summary>
    /// Delete a template by ID
    /// </summary>
    Task<bool> DeleteTemplateAsync(string id);

    /// <summary>
    /// Record that a template was used (increment usage count, update LastUsedAt)
    /// </summary>
    Task RecordTemplateUsageAsync(string id);

    /// <summary>
    /// Toggle favorite status for a template
    /// </summary>
    Task<bool> ToggleFavoriteAsync(string id);

    /// <summary>
    /// Get all unique categories
    /// </summary>
    Task<IEnumerable<string>> GetCategoriesAsync();
}
