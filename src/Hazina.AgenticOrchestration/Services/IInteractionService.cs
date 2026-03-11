using Hazina.AgenticOrchestration.Models;

namespace Hazina.AgenticOrchestration.Services;

public interface IInteractionService
{
    Task<long> NotifyAwaitingInputAsync(string sessionId, AwaitInputRequest request);
    Task<UserResponse?> PollForResponseAsync(string sessionId, long interactionId);
    Task<bool> RespondToInteractionAsync(string sessionId, long interactionId, string response, string userId);
    Task<List<PendingInteraction>> GetPendingInteractionsAsync();
}
