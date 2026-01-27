using Hazina.AgenticOrchestration.Models;
using Hazina.AgenticOrchestration.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hazina.AgenticOrchestration.Controllers;

[ApiController]
[Route("api/agentic/[controller]")]
public class InteractionsController : ControllerBase
{
    private readonly IInteractionService _interactionService;

    public InteractionsController(IInteractionService interactionService)
    {
        _interactionService = interactionService;
    }

    /// <summary>
    /// Get all pending interactions across all instances
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<List<PendingInteraction>>> GetPendingInteractions()
    {
        var pending = await _interactionService.GetPendingInteractionsAsync();
        return Ok(pending);
    }
}
