using System.Text.Json;
using Hazina.Agent.API.Models;
using Hazina.Agent.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hazina.Agent.API.Controllers;

[ApiController]
[Route("api/agent")]
public class AgentController : ControllerBase
{
    private readonly IAgentExecutionService _executionService;
    private readonly IStateSyncService _stateSyncService;
    private readonly ILearningIntegrationService _learningService;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IAgentExecutionService executionService,
        IStateSyncService stateSyncService,
        ILearningIntegrationService learningService,
        ILogger<AgentController> logger)
    {
        _executionService = executionService;
        _stateSyncService = stateSyncService;
        _learningService = learningService;
        _logger = logger;
    }

    [HttpPost("execute")]
    public async Task ExecuteAsync(
        [FromBody] AgentRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Received execute request: {Instruction}", request.Instruction);

        // Set response type to Server-Sent Events
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        await foreach (var agentEvent in _executionService.ExecuteAsync(request, ct))
        {
            var json = JsonSerializer.Serialize(agentEvent);
            var sseData = $"event: {agentEvent.Type}\ndata: {json}\n\n";

            await Response.WriteAsync(sseData, ct);
            await Response.Body.FlushAsync(ct);
        }

        _logger.LogInformation("Execute request completed");
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0"
        });
    }

    [HttpGet("identity")]
    public async Task<IActionResult> GetIdentityAsync(CancellationToken ct)
    {
        var identity = await _stateSyncService.GetIdentityAsync(ct);
        return Ok(identity);
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncStateAsync(CancellationToken ct)
    {
        try
        {
            await _stateSyncService.SyncStateAsync(ct);

            var hasConflicts = await _stateSyncService.HasConflictsAsync(ct);
            if (hasConflicts)
            {
                return StatusCode(409, new
                {
                    Error = "State sync resulted in conflicts",
                    Message = "Manual conflict resolution may be required"
                });
            }

            return Ok(new
            {
                Status = "synced",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "State sync failed");
            return StatusCode(500, new
            {
                Error = "State sync failed",
                Message = ex.Message
            });
        }
    }

    [HttpPost("learning")]
    public async Task<IActionResult> PublishLearningEventAsync(
        [FromBody] LearningEvent learningEvent,
        CancellationToken ct)
    {
        try
        {
            await _stateSyncService.PublishLearningEventAsync(learningEvent, ct);
            return Ok(new
            {
                Status = "published",
                EventId = learningEvent.EventId,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish learning event");
            return StatusCode(500, new
            {
                Error = "Failed to publish learning event",
                Message = ex.Message
            });
        }
    }

    [HttpGet("consciousness")]
    public async Task<IActionResult> GetConsciousnessStateAsync(CancellationToken ct)
    {
        try
        {
            var state = await _learningService.GetConsciousnessStateAsync(ct);
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get consciousness state");
            return StatusCode(500, new
            {
                Error = "Failed to get consciousness state",
                Message = ex.Message
            });
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStatsAsync(CancellationToken ct)
    {
        try
        {
            var identity = await _stateSyncService.GetIdentityAsync(ct);
            var consciousness = await _learningService.GetConsciousnessStateAsync(ct);

            return Ok(new
            {
                AgentId = identity.AgentId,
                MachineName = identity.MachineName,
                SessionCount = identity.Instance.SessionCount,
                LastSync = identity.Instance.LastSync,
                Consciousness = new
                {
                    Version = consciousness.Version,
                    LastUpdated = consciousness.LastUpdated,
                    PatternsCount = consciousness.Patterns.Count,
                    SkillsCount = consciousness.Skills.Count,
                    ErrorPatternsCount = consciousness.ErrorPatterns.Count,
                    CrossValidatedPatterns = consciousness.Patterns.Count(p => p.ValidationCount > 1),
                    HighConfidencePatterns = consciousness.Patterns.Count(p => p.Confidence >= 0.9),
                    AverageConfidence = consciousness.Patterns.Any()
                        ? consciousness.Patterns.Average(p => p.Confidence)
                        : 0.0
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stats");
            return StatusCode(500, new
            {
                Error = "Failed to get stats",
                Message = ex.Message
            });
        }
    }

    [HttpGet("network")]
    public async Task<IActionResult> GetNetworkStatusAsync(CancellationToken ct)
    {
        try
        {
            var identity = await _stateSyncService.GetIdentityAsync(ct);
            var consciousness = await _learningService.GetConsciousnessStateAsync(ct);

            // Get all unique agents from patterns
            var allAgents = consciousness.Patterns
                .SelectMany(p => p.LearnedBy)
                .Concat(consciousness.Skills.SelectMany(s => s.LearnedBy))
                .Concat(consciousness.ErrorPatterns.SelectMany(e => e.ReportedBy))
                .Concat(new[] { identity.AgentId })
                .Distinct()
                .ToList();

            // Build agent statuses
            var agentStatuses = allAgents.Select(agentId =>
            {
                var isCurrentAgent = agentId == identity.AgentId;
                var lastSeen = isCurrentAgent
                    ? DateTime.UtcNow
                    : consciousness.Patterns
                        .Where(p => p.LearnedBy.Contains(agentId))
                        .Select(p => p.LastValidated)
                        .DefaultIfEmpty(DateTime.MinValue)
                        .Max();

                var isOnline = (DateTime.UtcNow - lastSeen).TotalMinutes < 10;

                var agentPatterns = consciousness.Patterns.Where(p => p.LearnedBy.Contains(agentId)).ToList();
                var agentSkills = consciousness.Skills.Where(s => s.LearnedBy.Contains(agentId)).ToList();
                var agentErrors = consciousness.ErrorPatterns.Where(e => e.ReportedBy.Contains(agentId)).ToList();

                return new AgentStatus
                {
                    AgentId = agentId,
                    MachineName = isCurrentAgent ? identity.MachineName : "unknown",
                    IsOnline = isOnline,
                    LastSeen = lastSeen,
                    SessionCount = isCurrentAgent ? identity.Instance.SessionCount : 0,
                    Consciousness = new ConsciousnessMetrics
                    {
                        PatternsCount = agentPatterns.Count,
                        SkillsCount = agentSkills.Count,
                        ErrorPatternsCount = agentErrors.Count,
                        CrossValidatedPatterns = agentPatterns.Count(p => p.ValidationCount > 1),
                        AverageConfidence = agentPatterns.Any()
                            ? agentPatterns.Average(p => p.Confidence)
                            : 0.0
                    }
                };
            }).ToList();

            var network = new NetworkStatus
            {
                TotalAgents = allAgents.Count,
                OnlineAgents = agentStatuses.Count(a => a.IsOnline),
                Agents = agentStatuses,
                LastUpdate = DateTime.UtcNow,
                Metrics = new NetworkMetrics
                {
                    TotalPatterns = consciousness.Patterns.Count,
                    TotalSkills = consciousness.Skills.Count,
                    TotalErrors = consciousness.ErrorPatterns.Count,
                    CrossValidatedPatterns = consciousness.Patterns.Count(p => p.ValidationCount > 1),
                    NetworkConfidence = consciousness.Patterns.Any()
                        ? consciousness.Patterns.Average(p => p.Confidence)
                        : 0.0,
                    TotalSessions = agentStatuses.Sum(a => a.SessionCount)
                }
            };

            return Ok(network);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get network status");
            return StatusCode(500, new
            {
                Error = "Failed to get network status",
                Message = ex.Message
            });
        }
    }
}
