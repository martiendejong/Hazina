using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat
{
    public interface IProjectChatNotifier
    {
        Task NotifyChatCreated(string project, string chatId);
        Task NotifyChunkReceived(string project, string subMethod, string contentHook, string chunk, string chatId = null);
        Task NotifyCanvasReceived(string project, string subMethod, string contentHook, string chunk, string chatId = null);
        Task NotifyOperationStatus(string projectId, string chatId, string operation, string status);
        Task NotifyComponentRequested(string projectId, string chatId, string componentName, object data);
        // STAP 38: Event for when an item is generated
        Task NotifyItemGenerated(string projectId, string itemType, string itemId, string title, string status);
    }
}
