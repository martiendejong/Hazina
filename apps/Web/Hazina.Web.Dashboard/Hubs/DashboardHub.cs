using Microsoft.AspNetCore.SignalR;

namespace Hazina.Web.Dashboard.Hubs;

/// <summary>
/// SignalR hub for real-time dashboard updates
/// Pushes agent status, metrics, and events to connected clients
/// </summary>
public class DashboardHub : Hub
{
    private readonly ILogger<DashboardHub> _logger;

    public DashboardHub(ILogger<DashboardHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client requests agent status update
    /// </summary>
    public async Task RequestStatus()
    {
        _logger.LogInformation("Status requested by {ConnectionId}", Context.ConnectionId);
        // Dashboard service will push status via BroadcastStatus
    }

    /// <summary>
    /// Client sends control command (start/stop agent, etc.)
    /// </summary>
    public async Task<CommandResult> SendCommand(string command, object? parameters)
    {
        _logger.LogInformation("Command received: {Command} from {ConnectionId}", command, Context.ConnectionId);

        // Process command and return result
        return new CommandResult
        {
            Success = true,
            Message = $"Command {command} processed"
        };
    }
}

public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}
