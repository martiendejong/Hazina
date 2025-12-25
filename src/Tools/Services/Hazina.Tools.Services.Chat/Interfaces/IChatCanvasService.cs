using Hazina.Tools.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat
{
    public interface IChatCanvasService
    {
        string GetChatUploadsFolder(string projectId, string chatId);
        string GetChatUploadsFolder(string projectId, string chatId, string userId);
        Task<ChatConversation> EditCanvasMessage(string projectId, string chatId, Project project, CanvasMessage message, CancellationToken cancel);
        Task<ChatConversation> EditCanvasMessage(string projectId, string chatId, string userId, Project project, CanvasMessage message, CancellationToken cancel);
    }
}
