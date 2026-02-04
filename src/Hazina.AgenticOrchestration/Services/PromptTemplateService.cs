using System.Text.Json;
using Hazina.AgenticOrchestration.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.AgenticOrchestration.Services;

/// <summary>
/// File-based implementation of prompt template service.
/// Stores templates in JSON file for persistence.
/// For production, consider migrating to database storage.
/// </summary>
public class PromptTemplateService : IPromptTemplateService
{
    private readonly ILogger<PromptTemplateService> _logger;
    private readonly string _storageFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private List<PromptTemplate> _templates = new();

    public PromptTemplateService(ILogger<PromptTemplateService> logger)
    {
        _logger = logger;

        // Store templates in application data directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var hazinaDir = Path.Combine(appDataPath, "Hazina", "AgenticOrchestration");
        Directory.CreateDirectory(hazinaDir);

        _storageFilePath = Path.Combine(hazinaDir, "prompt-templates.json");

        // Load existing templates on startup
        _templates = LoadTemplatesFromFile();

        _logger.LogInformation("PromptTemplateService initialized. Storage: {Path}, Templates loaded: {Count}",
            _storageFilePath, _templates.Count);
    }

    private List<PromptTemplate> LoadTemplatesFromFile()
    {
        try
        {
            if (!File.Exists(_storageFilePath))
            {
                _logger.LogInformation("No existing templates file found. Starting with empty template list.");
                return CreateDefaultTemplates();
            }

            var json = File.ReadAllText(_storageFilePath);
            var templates = JsonSerializer.Deserialize<List<PromptTemplate>>(json) ?? new List<PromptTemplate>();

            _logger.LogInformation("Loaded {Count} templates from {Path}", templates.Count, _storageFilePath);
            return templates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load templates from {Path}. Starting with empty list.", _storageFilePath);
            return CreateDefaultTemplates();
        }
    }

    private List<PromptTemplate> CreateDefaultTemplates()
    {
        // Create some useful default templates
        return new List<PromptTemplate>
        {
            new PromptTemplate
            {
                Name = "Debug Frontend Build",
                Description = "Help me debug frontend build errors and fix TypeScript issues",
                PromptText = "I'm having frontend build errors. Can you help me debug and fix them?",
                Category = "debugging",
                Icon = "bug",
                AutoExecute = true,
                IsFavorite = true,
                SortOrder = 1
            },
            new PromptTemplate
            {
                Name = "Run Tests",
                Description = "Run unit tests and fix any failures",
                PromptText = "Run the unit tests and help me fix any failures",
                Category = "testing",
                Icon = "test-tube",
                AutoExecute = true,
                IsFavorite = true,
                SortOrder = 2
            },
            new PromptTemplate
            {
                Name = "Code Review",
                Description = "Review my recent changes and suggest improvements",
                PromptText = "Review my recent code changes and suggest improvements for code quality, performance, and best practices",
                Category = "review",
                Icon = "clipboard-check",
                AutoExecute = true,
                SortOrder = 3
            },
            new PromptTemplate
            {
                Name = "Implement Feature",
                Description = "Start implementing a new feature from ClickUp task",
                PromptText = "I have a new feature to implement. Let me share the ClickUp task details...",
                Category = "development",
                Icon = "code",
                AutoExecute = false, // Wait for user to paste task URL
                SortOrder = 4
            }
        };
    }

    private async Task SaveTemplatesAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(_templates, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_storageFilePath, json);
            _logger.LogDebug("Saved {Count} templates to {Path}", _templates.Count, _storageFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save templates to {Path}", _storageFilePath);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public Task<IEnumerable<PromptTemplate>> GetAllTemplatesAsync(string? category = null)
    {
        var templates = _templates.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            templates = templates.Where(t => t.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true);
        }

        return Task.FromResult<IEnumerable<PromptTemplate>>(templates.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToList());
    }

    public Task<PromptTemplate?> GetTemplateAsync(string id)
    {
        return Task.FromResult(_templates.FirstOrDefault(t => t.Id == id));
    }

    public Task<IEnumerable<PromptTemplate>> GetFavoriteTemplatesAsync()
    {
        return Task.FromResult(_templates
            .Where(t => t.IsFavorite)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .AsEnumerable());
    }

    public Task<IEnumerable<PromptTemplate>> GetRecentTemplatesAsync(int limit = 5)
    {
        return Task.FromResult(_templates
            .Where(t => t.LastUsedAt != null)
            .OrderByDescending(t => t.LastUsedAt)
            .Take(limit)
            .AsEnumerable());
    }

    public async Task<PromptTemplate> CreateTemplateAsync(PromptTemplate template)
    {
        // Ensure unique ID
        template.Id = Guid.NewGuid().ToString("N");
        template.CreatedAt = DateTime.UtcNow;

        _templates.Add(template);
        await SaveTemplatesAsync();

        _logger.LogInformation("Created new template '{Name}' with ID {Id}", template.Name, template.Id);
        return template;
    }

    public async Task<PromptTemplate> UpdateTemplateAsync(PromptTemplate template)
    {
        var existingIndex = _templates.FindIndex(t => t.Id == template.Id);
        if (existingIndex == -1)
        {
            throw new InvalidOperationException($"Template with ID {template.Id} not found");
        }

        _templates[existingIndex] = template;
        await SaveTemplatesAsync();

        _logger.LogInformation("Updated template '{Name}' ({Id})", template.Name, template.Id);
        return template;
    }

    public async Task<bool> DeleteTemplateAsync(string id)
    {
        var template = _templates.FirstOrDefault(t => t.Id == id);
        if (template == null)
        {
            return false;
        }

        _templates.Remove(template);
        await SaveTemplatesAsync();

        _logger.LogInformation("Deleted template '{Name}' ({Id})", template.Name, id);
        return true;
    }

    public async Task RecordTemplateUsageAsync(string id)
    {
        var template = _templates.FirstOrDefault(t => t.Id == id);
        if (template == null)
        {
            _logger.LogWarning("Attempted to record usage for non-existent template {Id}", id);
            return;
        }

        template.LastUsedAt = DateTime.UtcNow;
        template.UsageCount++;

        await SaveTemplatesAsync();

        _logger.LogDebug("Recorded usage for template '{Name}' ({Id}). Total uses: {Count}",
            template.Name, id, template.UsageCount);
    }

    public async Task<bool> ToggleFavoriteAsync(string id)
    {
        var template = _templates.FirstOrDefault(t => t.Id == id);
        if (template == null)
        {
            return false;
        }

        template.IsFavorite = !template.IsFavorite;
        await SaveTemplatesAsync();

        _logger.LogInformation("Toggled favorite for template '{Name}' ({Id}). IsFavorite: {IsFavorite}",
            template.Name, id, template.IsFavorite);

        return template.IsFavorite;
    }

    public Task<IEnumerable<string>> GetCategoriesAsync()
    {
        var categories = _templates
            .Select(t => t.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .Cast<string>();

        return Task.FromResult(categories);
    }
}
