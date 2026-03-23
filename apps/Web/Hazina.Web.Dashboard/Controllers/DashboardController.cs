using Hazina.Web.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hazina.Web.Dashboard.Controllers;

/// <summary>
/// Dashboard REST API controller
/// Provides manual control endpoints for dashboard operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;
    private readonly MetricsCollector _metricsCollector;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        DashboardService dashboardService,
        MetricsCollector metricsCollector,
        ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/dashboard/status
    /// Get current dashboard status
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<DashboardStatus>> GetStatus()
    {
        try
        {
            var status = await _dashboardService.GetStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dashboard status");
            return StatusCode(500, new { error = "Failed to retrieve status", details = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/dashboard/broadcast-status
    /// Manually trigger status broadcast to all clients
    /// </summary>
    [HttpPost("broadcast-status")]
    public async Task<IActionResult> BroadcastStatus()
    {
        try
        {
            await _dashboardService.BroadcastStatusAsync();
            return Ok(new { message = "Status broadcast triggered" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast status");
            return StatusCode(500, new { error = "Failed to broadcast status", details = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/dashboard/metrics/reset
    /// Reset all metrics to zero
    /// </summary>
    [HttpPost("metrics/reset")]
    public IActionResult ResetMetrics()
    {
        try
        {
            _metricsCollector.Reset();
            _logger.LogInformation("Metrics reset via API");
            return Ok(new { message = "Metrics reset successful" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset metrics");
            return StatusCode(500, new { error = "Failed to reset metrics", details = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/dashboard/health
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "Jengo Control Dashboard",
            version = "1.0.0"
        });
    }
}
