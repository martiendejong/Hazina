using DevGPT.GenerationTools.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DevGPT.GenerationTools.Services.Chat
{
    public interface IChatCanvasService
    {
        string GetChatUploadsFolder(string projectId, string chatId);
        string GetChatUploadsFolder(string projectId, string chatId, string userId);
        Task<ChatConversation> EditCanvasMessage(string projectId, string chatId, Project project, CanvasMessage message, CancellationToken cancel);
        Task<ChatConversation> EditCanvasMessage(string projectId, string chatId, string userId, Project project, CanvasMessage message, CancellationToken cancel);
    }
}
