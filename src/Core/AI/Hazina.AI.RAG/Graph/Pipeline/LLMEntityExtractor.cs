namespace Hazina.AI.RAG.Graph.Pipeline;

using System.Text.Json;
using Hazina.AI.Providers.Core;
using Hazina.AI.RAG.Configuration;
using Hazina.AI.RAG.Graph.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// LLM-based entity extraction using structured prompts.
/// </summary>
public class LLMEntityExtractor : IEntityExtractor
{
    private readonly ILLMTextService _llmService;
    private readonly ILogger<LLMEntityExtractor> _logger;
    private readonly GraphRAGConfig _config;

    private const string ENTITY_EXTRACTION_PROMPT = @"Extract named entities from the following text.
Identify entities of these types: Person, Organization, Location, Concept, Event, Product, Document, Topic.

Return ONLY a JSON array with this exact format (no additional text):
[
  {
    ""name"": ""entity name"",
    ""type"": ""entity type"",
    ""description"": ""brief description"",
    ""properties"": {},
    ""confidence"": 0.95
  }
]

Text to analyze:
{text}

JSON array:";

    public LLMEntityExtractor(
        ILLMTextService llmService,
        ILogger<LLMEntityExtractor> logger,
        GraphRAGConfig config)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<List<GraphEntity>> ExtractEntitiesAsync(
        string text,
        string? documentId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<GraphEntity>();
        }

        try
        {
            var prompt = ENTITY_EXTRACTION_PROMPT.Replace("{text}", text);

            var response = await _llmService.GetResponseAsync(
                prompt,
                maxTokens: 2000,
                temperature: 0.3f, // Lower temperature for more consistent extraction
                cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(response))
            {
                _logger.LogWarning("Empty response from LLM during entity extraction");
                return new List<GraphEntity>();
            }

            // Parse JSON response
            var entities = ParseEntityResponse(response, documentId);

            _logger.LogInformation("Extracted {Count} entities from text", entities.Count);

            return entities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting entities from text");
            return new List<GraphEntity>();
        }
    }

    private List<GraphEntity> ParseEntityResponse(string response, string? documentId)
    {
        try
        {
            // Clean response (remove markdown code blocks if present)
            var jsonText = response.Trim();
            if (jsonText.StartsWith("```json"))
            {
                jsonText = jsonText.Substring(7);
            }
            if (jsonText.StartsWith("```"))
            {
                jsonText = jsonText.Substring(3);
            }
            if (jsonText.EndsWith("```"))
            {
                jsonText = jsonText.Substring(0, jsonText.Length - 3);
            }
            jsonText = jsonText.Trim();

            var extractedEntities = JsonSerializer.Deserialize<List<ExtractedEntity>>(jsonText);
            if (extractedEntities == null)
            {
                _logger.LogWarning("Failed to deserialize entity response");
                return new List<GraphEntity>();
            }

            var entities = new List<GraphEntity>();
            foreach (var extracted in extractedEntities)
            {
                // Validate entity type against schema
                if (_config.Schema.StrictValidation && !_config.Schema.IsValidEntityType(extracted.Type))
                {
                    _logger.LogWarning("Skipping entity {Name} with invalid type {Type}",
                        extracted.Name, extracted.Type);
                    continue;
                }

                // Skip low-confidence entities
                if (extracted.Confidence < _config.MinEntityConfidence)
                {
                    _logger.LogDebug("Skipping low-confidence entity {Name} (confidence: {Confidence})",
                        extracted.Name, extracted.Confidence);
                    continue;
                }

                var entity = new GraphEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = extracted.Name,
                    Type = extracted.Type,
                    Description = extracted.Description,
                    Properties = extracted.Properties ?? new Dictionary<string, object>(),
                    Confidence = extracted.Confidence,
                    Created = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };

                if (!string.IsNullOrWhiteSpace(documentId))
                {
                    entity.SourceDocuments.Add(documentId);
                }

                entities.Add(entity);
            }

            return entities;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response: {Response}", response);
            return new List<GraphEntity>();
        }
    }

    private class ExtractedEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Dictionary<string, object>? Properties { get; set; }
        public double Confidence { get; set; } = 1.0;
    }
}
