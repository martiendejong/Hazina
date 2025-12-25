using System.Threading.Tasks;

namespace DevGPT.GenerationTools.Services.Chat
{
    public interface IProjectChatNotifier
    {
        Task NotifyChatCreated(string project, string chatId);
        Task NotifyChunkReceived(string project, string subMethod, string contentHook, string chunk);
        Task NotifyCanvasReceived(string project, string subMethod, string contentHook, string chunk);
    }
}
