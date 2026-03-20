using System.ComponentModel.DataAnnotations;
using Hazina.AgenticOrchestration.Extensions;
using Hazina.AgenticOrchestration.Models;
using Hazina.AgenticOrchestration.Services;
using Hazina.AgenticOrchestration.Terminal;
using Hazina.AgenticOrchestration.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hazina.AgenticOrchestration.Controllers;

/// <summary>
/// Controller for spawning and managing agents with beacon injection
/// Inspired by Overstory's sling command
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly ITerminalSessionManager _terminalManager;
    private readonly IBeaconService _beaconService;
    private readonly IAgentIdentityService _identityService;
    private readonly IHookConfigService _hookService;
    private readonly ILogger<AgentController> _logger;
    private readonly string _worktreeRoot;

    public AgentController(
        ITerminalSessionManager terminalManager,
        IBeaconService beaconService,
        IAgentIdentityService identityService,
        IHookConfigService hookService,
        ILogger<AgentController> logger,
        AgenticOrchestrationOptions options)
    {
        _terminalManager = terminalManager;
        _beaconService = beaconService;
        _identityService = identityService;
        _hookService = hookService;
        _logger = logger;
        _worktreeRoot = options.WorktreeRoot;
    }

    /// <summary>
    /// Spawn a new agent with automatic beacon injection
    /// </summary>
    [HttpPost("spawn")]
    public async Task<IActionResult> SpawnAgent([FromBody] SpawnAgentRequest request)
    {
        try
        {
            // Validate inputs
            if (!InputValidator.IsValidAgentName(request.AgentName))
            {
                return BadRequest(new { error = "Invalid agent name. Must match ^[a-zA-Z0-9_-]{1,50}$" });
            }

            if (!InputValidator.IsValidTaskId(request.TaskId))
            {
                return BadRequest(new { error = "Invalid task ID. Must match ^[a-zA-Z0-9_-]{1,200}$" });
            }

            // Validate path safety
            if (!string.IsNullOrEmpty(request.WorktreePath))
            {
                if (!InputValidator.IsPathSafe(request.WorktreePath, _worktreeRoot))
                {
                    return BadRequest(new { error = $"Worktree path must be within {_worktreeRoot}" });
                }
            }

            // 1. Load or create agent identity
            var identity = await _identityService.LoadIdentityAsync(request.AgentName);
            if (identity == null)
            {
                identity = await _identityService.CreateIdentityAsync(new AgentIdentity
                {
                    Name = request.AgentName,
                    Capability = request.Capability,
                    Created = DateTime.UtcNow,
                    ExpertiseDomains = request.ExpertiseDomains ?? new List<string>()
                });
            }

            // 2. Create terminal session
            var sessionConfig = new TerminalSessionConfig
            {
                Command = request.Command ?? "claude",
                Arguments = request.Arguments ?? new[] { "--dangerously-skip-permissions" },
                WorkingDirectory = request.WorkingDirectory
            };

            var session = await _terminalManager.CreateSessionAsync(sessionConfig);

            // 3. Install hooks (if worktree path provided)
            if (!string.IsNullOrEmpty(request.WorktreePath))
            {
                await _hookService.InstallHooksAsync(request.WorktreePath, request.AgentName, request.HookDebounceMs);
            }

            // 4. Send beacon with double-Enter workaround
            var beaconOptions = new BeaconOptions
            {
                AgentName = request.AgentName,
                Capability = request.Capability,
                TaskId = request.TaskId,
                ParentAgent = request.ParentAgent,
                Depth = request.Depth,
                CustomProtocol = request.CustomStartupProtocol
            };

            var beaconResult = await _beaconService.SendBeaconAsync(session.SessionId, beaconOptions);

            if (!beaconResult.Success)
            {
                _logger.LogWarning("Beacon injection failed for {AgentName}: {Error}", request.AgentName, beaconResult.Error);
            }

            return Ok(new SpawnAgentResponse
            {
                SessionId = session.SessionId,
                AgentName = request.AgentName,
                Capability = request.Capability,
                TaskId = request.TaskId,
                BeaconSent = beaconResult.Success,
                BeaconError = beaconResult.Error,
                StartedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to spawn agent {AgentName}", request.AgentName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List all active agents
    /// </summary>
    [HttpGet]
    public IActionResult ListAgents()
    {
        try
        {
            var sessions = _terminalManager.GetAllSessions();

            var agents = sessions.Select(s => new
            {
                s.SessionId,
                Title = s.Title ?? s.Command,
                s.IsRunning,
                s.StartedAt
            }).ToList();

            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list agents");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get agent details
    /// </summary>
    [HttpGet("{sessionId}")]
    public IActionResult GetAgent(string sessionId)
    {
        try
        {
            var session = _terminalManager.GetSession(sessionId);

            if (session == null)
            {
                return NotFound(new { error = "Agent not found" });
            }

            return Ok(new
            {
                session.SessionId,
                Title = session.Title ?? session.Command,
                session.IsRunning,
                session.StartedAt,
                session.ExitCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent {SessionId}", sessionId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Terminate an agent
    /// </summary>
    [HttpDelete("{sessionId}")]
    public async Task<IActionResult> TerminateAgent(string sessionId)
    {
        try
        {
            await _terminalManager.RemoveSessionAsync(sessionId);
            _logger.LogInformation("Terminated agent session {SessionId}", sessionId);
            return Ok(new { message = "Agent terminated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate agent {SessionId}", sessionId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Send a nudge to an agent
    /// </summary>
    [HttpPost("{sessionId}/nudge")]
    public async Task<IActionResult> NudgeAgent(string sessionId, [FromBody] NudgeRequest request)
    {
        try
        {
            var session = _terminalManager.GetSession(sessionId);
            if (session == null)
            {
                return NotFound(new { error = "Agent not found" });
            }

            var message = $"[NUDGE from {request.From ?? "orchestrator"}] {request.Message}";

            // Send message with double-Enter workaround
            await session.WriteInputAsync(message);
            await Task.Delay(500);
            await session.WriteInputAsync("");

            _logger.LogInformation("Nudged agent {SessionId}: {Message}", sessionId, message);

            return Ok(new { message = "Nudge sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to nudge agent {SessionId}", sessionId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class SpawnAgentRequest
{
    [Required]
    [RegularExpression(@"^[a-zA-Z0-9_-]{1,50}$", ErrorMessage = "AgentName must contain only alphanumeric, underscore, or hyphen characters (1-50 chars)")]
    public required string AgentName { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Capability { get; set; }

    [Required]
    [RegularExpression(@"^[a-zA-Z0-9_-]{1,200}$")]
    public required string TaskId { get; set; }

    [MaxLength(50)]
    public string? ParentAgent { get; set; }

    [Range(0, 10)]
    public int Depth { get; set; }

    [MaxLength(500)]
    public string? WorktreePath { get; set; }

    [MaxLength(200)]
    public string? Command { get; set; }

    public string[]? Arguments { get; set; }

    [MaxLength(500)]
    public string? WorkingDirectory { get; set; }

    [MaxLength(1000)]
    public string? CustomStartupProtocol { get; set; }

    public List<string>? ExpertiseDomains { get; set; }

    [Range(0, 30000)]
    public int HookDebounceMs { get; set; } = 5000;
}

public class SpawnAgentResponse
{
    public required string SessionId { get; set; }
    public required string AgentName { get; set; }
    public required string Capability { get; set; }
    public required string TaskId { get; set; }
    public bool BeaconSent { get; set; }
    public string? BeaconError { get; set; }
    public DateTime StartedAt { get; set; }
}

public class NudgeRequest
{
    [Required]
    [MaxLength(1000)]
    public required string Message { get; set; }

    [MaxLength(50)]
    public string? From { get; set; }
}
