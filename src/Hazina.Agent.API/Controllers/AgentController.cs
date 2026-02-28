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
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IAgentExecutionService executionService,
        ILogger<AgentController> logger)
    {
        _executionService = executionService;
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
}
