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

    /// <summary>
    /// Join a chat session to receive real-time chat updates
    /// </summary>
    public async Task JoinChatSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat-{sessionId}");
    }

    /// <summary>
    /// Leave a chat session
    /// </summary>
    public async Task LeaveChatSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat-{sessionId}");
    }
}
