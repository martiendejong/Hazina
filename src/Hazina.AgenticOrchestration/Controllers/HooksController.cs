using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Hazina.AgenticOrchestration.Services;

namespace Hazina.AgenticOrchestration.Controllers;

/// <summary>
/// Controller for managing Claude Code hooks
/// Inspired by Overstory's hooks management
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HooksController : ControllerBase
{
    private readonly IHookConfigService _hookService;
    private readonly ILogger<HooksController> _logger;

    public HooksController(IHookConfigService hookService, ILogger<HooksController> logger)
    {
        _hookService = hookService;
        _logger = logger;
    }

    /// <summary>
    /// Install hooks to a worktree
    /// </summary>
    [HttpPost("install")]
    public async Task<IActionResult> InstallHooks([FromBody] InstallHooksRequest request)
    {
        try
        {
            await _hookService.InstallHooksAsync(request.WorktreePath, request.AgentName, request.DebounceMs);

            _logger.LogInformation("Installed hooks for {AgentName} at {WorktreePath}", request.AgentName, request.WorktreePath);

            return Ok(new { message = "Hooks installed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install hooks for {AgentName}", request.AgentName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Uninstall hooks from a worktree
    /// </summary>
    [HttpPost("uninstall")]
    public async Task<IActionResult> UninstallHooks([FromBody] UninstallHooksRequest request)
    {
        try
        {
            await _hookService.UninstallHooksAsync(request.WorktreePath);

            _logger.LogInformation("Uninstalled hooks from {WorktreePath}", request.WorktreePath);

            return Ok(new { message = "Hooks uninstalled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uninstall hooks from {WorktreePath}", request.WorktreePath);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Check if hooks are installed
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> CheckHooksStatus([FromQuery] string worktreePath)
    {
        try
        {
            var installed = await _hookService.AreHooksInstalledAsync(worktreePath);

            return Ok(new { installed, worktreePath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check hooks status for {WorktreePath}", worktreePath);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate hooks configuration (preview)
    /// </summary>
    [HttpPost("preview")]
    public async Task<IActionResult> PreviewHooksConfig([FromBody] PreviewHooksRequest request)
    {
        try
        {
            var config = await _hookService.GenerateHooksConfigAsync(request.AgentName, request.DebounceMs);

            return Ok(new { config });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate hooks preview for {AgentName}", request.AgentName);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class InstallHooksRequest
{
    public required string WorktreePath { get; set; }
    public required string AgentName { get; set; }
    public int DebounceMs { get; set; } = 5000;
}

public class UninstallHooksRequest
{
    public required string WorktreePath { get; set; }
}

public class PreviewHooksRequest
{
    public required string AgentName { get; set; }
    public int DebounceMs { get; set; } = 5000;
}
