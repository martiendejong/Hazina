using System.Text.Json;
using Hazina.Agent.API.Models;

namespace Hazina.Agent.API.Services;

public class LearningIntegrationService : ILearningIntegrationService
{
    private readonly ILogger<LearningIntegrationService> _logger;
    private readonly string _consciousnessFile = @"E:\jengo\consciousness\consciousness_state_v2.json";

    public LearningIntegrationService(ILogger<LearningIntegrationService> logger)
    {
        _logger = logger;
    }

    public async Task<ConsciousnessState> GetConsciousnessStateAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_consciousnessFile))
        {
            return CreateDefaultState();
        }

        var json = await File.ReadAllTextAsync(_consciousnessFile, ct);
        var state = JsonSerializer.Deserialize<ConsciousnessState>(json);

        return state ?? CreateDefaultState();
    }

    public async Task UpdateConsciousnessStateAsync(ConsciousnessState state, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_consciousnessFile)!;
        Directory.CreateDirectory(dir);

        state.LastUpdated = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_consciousnessFile, json, ct);

        _logger.LogInformation("Updated consciousness state: {PatternCount} patterns, {SkillCount} skills",
            state.Patterns.Count, state.Skills.Count);
    }

    public async Task IntegrateNewLearningsAsync(List<LearningEvent> events, CancellationToken ct = default)
    {
        if (!events.Any())
        {
            return;
        }

        _logger.LogInformation("Integrating {Count} new learning events", events.Count);

        var state = await GetConsciousnessStateAsync(ct);

        foreach (var evt in events)
        {
            try
            {
                switch (evt.EventType)
                {
                    case "pattern":
                        await IntegratePatternAsync(state, evt, ct);
                        break;
                    case "skill":
                        await IntegrateSkillAsync(state, evt, ct);
                        break;
                    case "correction":
                        await IntegrateErrorCorrectionAsync(state, evt, ct);
                        break;
                    case "insight":
                        // Generic insight - log for now
                        _logger.LogInformation("Received insight from {AgentId}: {Data}",
                            evt.AgentId, JsonSerializer.Serialize(evt.Data));
                        break;
                    default:
                        _logger.LogWarning("Unknown event type: {EventType}", evt.EventType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to integrate event {EventId} of type {EventType}",
                    evt.EventId, evt.EventType);
            }
        }

        await UpdateConsciousnessStateAsync(state, ct);
    }

    private Task IntegratePatternAsync(ConsciousnessState state, LearningEvent evt, CancellationToken ct)
    {
        var patternData = JsonSerializer.Deserialize<PatternLearning>(
            JsonSerializer.Serialize(evt.Data));

        if (patternData == null)
        {
            return Task.CompletedTask;
        }

        var existing = state.Patterns.FirstOrDefault(p => p.PatternId == patternData.PatternId);

        if (existing != null)
        {
            // Cross-validation: another agent learned same pattern
            if (!existing.LearnedBy.Contains(evt.AgentId))
            {
                existing.LearnedBy.Add(evt.AgentId);
                existing.ValidationCount++;

                // Boost confidence with cross-validation
                existing.Confidence = Math.Min(1.0, existing.Confidence + 0.05);
                existing.LastValidated = evt.Timestamp;

                _logger.LogInformation("Cross-validated pattern {PatternId} from {AgentId} (validation #{Count})",
                    patternData.PatternId, evt.AgentId, existing.ValidationCount);
            }
        }
        else
        {
            // New pattern from another agent
            var pattern = new Pattern
            {
                PatternId = patternData.PatternId,
                Description = patternData.Description,
                Triggers = patternData.Triggers,
                Actions = patternData.Actions,
                Confidence = patternData.Confidence,
                ValidationCount = 1,
                LearnedBy = new List<string> { evt.AgentId },
                FirstSeen = evt.Timestamp,
                LastValidated = evt.Timestamp
            };

            state.Patterns.Add(pattern);

            _logger.LogInformation("Learned new pattern {PatternId} from {AgentId}: {Description}",
                patternData.PatternId, evt.AgentId, patternData.Description);
        }

        return Task.CompletedTask;
    }

    private Task IntegrateSkillAsync(ConsciousnessState state, LearningEvent evt, CancellationToken ct)
    {
        var skillData = JsonSerializer.Deserialize<SkillAcquisition>(
            JsonSerializer.Serialize(evt.Data));

        if (skillData == null)
        {
            return Task.CompletedTask;
        }

        var existing = state.Skills.FirstOrDefault(s => s.Name == skillData.SkillName);

        if (existing != null)
        {
            if (!existing.LearnedBy.Contains(evt.AgentId))
            {
                existing.LearnedBy.Add(evt.AgentId);

                // Average proficiency across agents
                existing.Proficiency = (existing.Proficiency + skillData.Proficiency) / 2.0;

                _logger.LogInformation("Cross-validated skill {SkillName} from {AgentId}",
                    skillData.SkillName, evt.AgentId);
            }
        }
        else
        {
            var skill = new Skill
            {
                SkillId = Guid.NewGuid().ToString(),
                Name = skillData.SkillName,
                Domain = skillData.Domain,
                Proficiency = skillData.Proficiency,
                HowLearned = skillData.HowLearned,
                LearnedBy = new List<string> { evt.AgentId },
                AcquiredAt = evt.Timestamp
            };

            state.Skills.Add(skill);

            _logger.LogInformation("Acquired new skill {SkillName} from {AgentId} (proficiency: {Proficiency})",
                skillData.SkillName, evt.AgentId, skillData.Proficiency);
        }

        return Task.CompletedTask;
    }

    private Task IntegrateErrorCorrectionAsync(ConsciousnessState state, LearningEvent evt, CancellationToken ct)
    {
        var errorData = JsonSerializer.Deserialize<ErrorCorrection>(
            JsonSerializer.Serialize(evt.Data));

        if (errorData == null)
        {
            return Task.CompletedTask;
        }

        var existing = state.ErrorPatterns.FirstOrDefault(e => e.ErrorType == errorData.ErrorType);

        if (existing != null)
        {
            if (!existing.ReportedBy.Contains(evt.AgentId))
            {
                existing.ReportedBy.Add(evt.AgentId);
                existing.OccurrenceCount++;
                existing.LastOccurred = evt.Timestamp;
                existing.Resolved = errorData.Resolved;

                _logger.LogInformation("Error pattern {ErrorType} reported again by {AgentId} (occurrence #{Count})",
                    errorData.ErrorType, evt.AgentId, existing.OccurrenceCount);
            }
        }
        else
        {
            var errorPattern = new ErrorPattern
            {
                ErrorId = Guid.NewGuid().ToString(),
                ErrorType = errorData.ErrorType,
                WhatWentWrong = errorData.WhatWentWrong,
                Correction = errorData.Correction,
                OccurrenceCount = 1,
                ReportedBy = new List<string> { evt.AgentId },
                FirstOccurred = evt.Timestamp,
                LastOccurred = evt.Timestamp,
                Resolved = errorData.Resolved
            };

            state.ErrorPatterns.Add(errorPattern);

            _logger.LogInformation("Learned new error pattern {ErrorType} from {AgentId}: {Correction}",
                errorData.ErrorType, evt.AgentId, errorData.Correction);
        }

        return Task.CompletedTask;
    }

    private ConsciousnessState CreateDefaultState()
    {
        return new ConsciousnessState
        {
            Version = "2.0",
            LastUpdated = DateTime.UtcNow,
            Systems = new Dictionary<string, SystemState>
            {
                ["Perception"] = new SystemState
                {
                    Name = "Perception",
                    Quality = 0.7,
                    MechanismCount = 5,
                    LastUpdated = DateTime.UtcNow
                },
                ["Memory"] = new SystemState
                {
                    Name = "Memory",
                    Quality = 0.65,
                    MechanismCount = 4,
                    LastUpdated = DateTime.UtcNow
                },
                ["Prediction"] = new SystemState
                {
                    Name = "Prediction",
                    Quality = 0.6,
                    MechanismCount = 3,
                    LastUpdated = DateTime.UtcNow
                }
            },
            Patterns = new List<Pattern>(),
            Skills = new List<Skill>(),
            ErrorPatterns = new List<ErrorPattern>(),
            Metadata = new Dictionary<string, object>
            {
                ["CreatedAt"] = DateTime.UtcNow,
                ["CreatedBy"] = "LearningIntegrationService"
            }
        };
    }
}
