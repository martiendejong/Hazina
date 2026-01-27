using Microsoft.AspNetCore.SignalR;

namespace Hazina.AgenticOrchestration.Hubs;

/// <summary>
/// SignalR hub for real-time communication between web clients and Claude instances
/// </summary>
public class ClaudeOrchestrationHub : Hub
{
    public async Task SubscribeToInstance(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"instance-{sessionId}");
    }

    public async Task UnsubscribeFromInstance(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"instance-{sessionId}");
    }

    public async Task JoinOrchestrators()
    {
        // Join the group of users who can see all interactions
        await Groups.AddToGroupAsync(Context.ConnectionId, "agentic-orchestrators");
    }

    public async Task LeaveOrchestrators()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "agentic-orchestrators");
    }
}
