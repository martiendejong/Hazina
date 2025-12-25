using Hazina.Tools.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat
{
    public interface IChatStreamService
    {
        Task<ChatConversation> SendChatMessage(string projectId, string chatId, Project project, GeneratorMessage chatMessage, CancellationToken cancel);
        Task<ChatConversation> SendChatMessage(string projectId, string chatId, Project project, GeneratorMessage chatMessage, IEnumerable<ConversationMessage> history, CancellationToken cancel, string userId = "");
        Task<ChatConversation> SendChatMessage(string projectId, string chatId, string userId, Project project, GeneratorMessage chatMessage, IEnumerable<ConversationMessage> history, CancellationToken cancel);
    }
}
