using System.Threading.Tasks;

namespace Hazina.Tools.Services.Embeddings
{
    public interface IEmbeddingsService
    {
        Task RefreshProjectEmbeddings(string projectId, bool force = false);
        Task RefreshGlobalEmbeddings();

        Task EmbedProjectFile(string projectId, string relativeFilePath);

        Task EmbedChatUpload(string projectId, string chatId, string relativeFileName, string userId = null);
        Task PromoteChatFileToProject(string projectId, string chatId, string filePrefix, string userId = null);
        Task DemoteChatFileFromProject(string projectId, string chatId, string filePrefix);
    }
}

