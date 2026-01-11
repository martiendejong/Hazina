namespace Hazina.AI.RAG.Graph.Pipeline;

using System.Text;
using System.Text.Json;
using Hazina.AI.Providers.Core;
using Hazina.AI.RAG.Configuration;
using Hazina.AI.RAG.Graph.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// LLM-based relationship extraction between entities.
/// </summary>
public class LLMRelationshipExtractor : IRelationshipExtractor
{
    private readonly ILLMTextService _llmService;
    private readonly ILogger<LLMRelationshipExtractor> _logger;
    private readonly GraphRAGConfig _config;

    private const string RELATIONSHIP_EXTRACTION_PROMPT = @"Analyze the relationships between these entities based on the given text.

Entities:
{entities}

Relationship types to identify: WORKS_FOR, LOCATED_IN, CREATED_BY, INFLUENCED_BY, PART_OF, MEMBER_OF, OWNS, MANAGES, PRODUCES, USES, RELATED_TO

Return ONLY a JSON array with this exact format (no additional text):
[
  {
    ""sourceEntity"": ""entity name"",
    ""targetEntity"": ""entity name"",
    ""relationType"": ""relationship type"",
    ""description"": ""brief description of relationship"",
    ""confidence"": 0.90,
    ""isBidirectional"": false
  }
]

Text:
{text}

JSON array:";

    public LLMRelationshipExtractor(
        ILLMTextService llmService,
        ILogger<LLMRelationshipExtractor> logger,
        GraphRAGConfig config)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<List<GraphRelationship>> ExtractRelationshipsAsync(
        string text,
        List<GraphEntity> entities,
        string? documentId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || entities == null || entities.Count < 2)
        {
            return new List<GraphRelationship>();
        }

        try
        {
            // Build entity list for prompt
            var entityList = new StringBuilder();
            foreach (var entity in entities)
            {
                entityList.AppendLine($"- {entity.Name} ({entity.Type})");
            }

            var prompt = RELATIONSHIP_EXTRACTION_PROMPT
                .Replace("{entities}", entityList.ToString())
                .Replace("{text}", text);

            var response = await _llmService.GetResponseAsync(
                prompt,
                maxTokens: 2000,
                temperature: 0.3f,
                cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(response))
            {
                _logger.LogWarning("Empty response from LLM during relationship extraction");
                return new List<GraphRelationship>();
            }

            // Parse JSON response
            var relationships = ParseRelationshipResponse(response, entities, documentId, text);

            _logger.LogInformation("Extracted {Count} relationships from text", relationships.Count);

            return relationships;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting relationships from text");
            return new List<GraphRelationship>();
        }
    }

    private List<GraphRelationship> ParseRelationshipResponse(
        string response,
        List<GraphEntity> entities,
        string? documentId,
        string sourceText)
    {
        try
        {
            // Clean response
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

            var extractedRelationships = JsonSerializer.Deserialize<List<ExtractedRelationship>>(jsonText);
            if (extractedRelationships == null)
            {
                _logger.LogWarning("Failed to deserialize relationship response");
                return new List<GraphRelationship>();
            }

            var relationships = new List<GraphRelationship>();
            var entityLookup = entities.ToDictionary(e => e.Name, e => e.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var extracted in extractedRelationships)
            {
                // Validate relationship type against schema
                if (_config.Schema.StrictValidation && !_config.Schema.IsValidRelationType(extracted.RelationType))
                {
                    _logger.LogWarning("Skipping relationship with invalid type {Type}",
                        extracted.RelationType);
                    continue;
                }

                // Skip low-confidence relationships
                if (extracted.Confidence < _config.MinRelationshipConfidence)
                {
                    _logger.LogDebug("Skipping low-confidence relationship (confidence: {Confidence})",
                        extracted.Confidence);
                    continue;
                }

                // Find entity IDs
                if (!entityLookup.TryGetValue(extracted.SourceEntity, out var sourceId))
                {
                    _logger.LogWarning("Source entity {Name} not found in entity list",
                        extracted.SourceEntity);
                    continue;
                }

                if (!entityLookup.TryGetValue(extracted.TargetEntity, out var targetId))
                {
                    _logger.LogWarning("Target entity {Name} not found in entity list",
                        extracted.TargetEntity);
                    continue;
                }

                var relationship = new GraphRelationship
                {
                    Id = Guid.NewGuid().ToString(),
                    SourceEntityId = sourceId,
                    TargetEntityId = targetId,
                    RelationType = extracted.RelationType,
                    Description = extracted.Description,
                    Confidence = extracted.Confidence,
                    IsBidirectional = extracted.IsBidirectional,
                    SourceDocumentId = documentId,
                    SourceText = sourceText.Length > 500 ? sourceText.Substring(0, 500) : sourceText,
                    Created = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };

                relationships.Add(relationship);
            }

            return relationships;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response: {Response}", response);
            return new List<GraphRelationship>();
        }
    }

    private class ExtractedRelationship
    {
        public string SourceEntity { get; set; } = string.Empty;
        public string TargetEntity { get; set; } = string.Empty;
        public string RelationType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public double Confidence { get; set; } = 1.0;
        public bool IsBidirectional { get; set; } = false;
    }
}
