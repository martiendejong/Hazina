using Hazina.AgenticOrchestration.Models;
using Hazina.AgenticOrchestration.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hazina.AgenticOrchestration.Controllers;

[ApiController]
[Route("api/agentic/[controller]")]
public class InstancesController : ControllerBase
{
    private readonly IClaudeInstanceManager _instanceManager;
    private readonly IOutputCaptureService _outputCaptureService;
    private readonly IInteractionService _interactionService;

    public InstancesController(
        IClaudeInstanceManager instanceManager,
        IOutputCaptureService outputCaptureService,
        IInteractionService interactionService)
    {
        _instanceManager = instanceManager;
        _outputCaptureService = outputCaptureService;
        _interactionService = interactionService;
    }

    /// <summary>
    /// Get all active Claude instances
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ClaudeInstance>>> GetActiveInstances()
    {
        var instances = await _instanceManager.GetActiveInstancesAsync();
        return Ok(instances);
    }

    /// <summary>
    /// Get details for a specific instance
    /// </summary>
    [HttpGet("{sessionId}")]
    public async Task<ActionResult<ClaudeInstance>> GetInstanceDetails(string sessionId)
    {
        var instance = await _instanceManager.GetInstanceDetailsAsync(sessionId);

        if (instance == null)
            return NotFound();

        return Ok(instance);
    }

    /// <summary>
    /// Get output history for an instance
    /// </summary>
    [HttpGet("{sessionId}/output")]
    public async Task<ActionResult<List<OutputLine>>> GetOutput(
        string sessionId,
        [FromQuery] int lastN = 100)
    {
        var output = await _outputCaptureService.GetOutputHistoryAsync(sessionId, lastN);
        return Ok(output);
    }

    /// <summary>
    /// Called by Claude CLI when it needs user input
    /// </summary>
    [HttpPost("{sessionId}/await-input")]
    public async Task<ActionResult> NotifyAwaitingInput(
        string sessionId,
        [FromBody] AwaitInputRequest request)
    {
        var interactionId = await _interactionService.NotifyAwaitingInputAsync(sessionId, request);
        return Ok(new { InteractionId = interactionId });
    }

    /// <summary>
    /// Poll endpoint for Claude CLI to check for user response
    /// </summary>
    [HttpGet("{sessionId}/interactions/{interactionId}/response")]
    public async Task<ActionResult<UserResponse>> GetInteractionResponse(
        string sessionId,
        long interactionId)
    {
        var response = await _interactionService.PollForResponseAsync(sessionId, interactionId);

        if (response == null)
        {
            return Ok(new { status = "pending" });
        }

        return Ok(new
        {
            status = "responded",
            response = response.Response,
            respondedAt = response.RespondedAt,
            respondedBy = response.RespondedBy
        });
    }

    /// <summary>
    /// User submits response to an interaction
    /// </summary>
    [HttpPost("{sessionId}/interactions/{interactionId}/respond")]
    public async Task<ActionResult> RespondToInteraction(
        string sessionId,
        long interactionId,
        [FromBody] RespondRequest request)
    {
        var userId = User.Identity?.Name ?? "Anonymous";
        var success = await _interactionService.RespondToInteractionAsync(
            sessionId,
            interactionId,
            request.Response,
            userId);

        if (!success)
        {
            return BadRequest("Interaction not found or already responded");
        }

        return Ok();
    }
}
