using Hazina.Tools.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat
{
    public interface IChatImageService
    {
        Task<ChatConversation> GenerateImage(string projectId, string chatId, Project project, GeneratorMessage chatMessage, CancellationToken cancel, bool isImageSet);
        Task<ChatConversation> GenerateImage(string projectId, string chatId, string userId, Project project, GeneratorMessage chatMessage, CancellationToken cancel, bool isImageSet);
        Task<ChatConversation> GenerateImage(string projectId, string chatId, Project project, GeneratorMessage chatMessage, CancellationToken cancel);
        Task<ChatConversation> GenerateImage(string projectId, string chatId, string userId, Project project, GeneratorMessage chatMessage, CancellationToken cancel);
    }
}
