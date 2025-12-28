using System.Collections.Generic;

namespace Hazina.Tools.Services.Embeddings
{
    public interface IEmbeddingsWorkQueue
    {
        void EnqueueEmbedProjectFile(string projectId, string fileName);
        void EnqueueExtractAndEmbedChatUpload(string projectId, string chatId, string fileName, string userId = null);
        void EnqueuePromoteChatFileToProject(string projectId, string chatId, string filePrefix, string userId = null);
        void EnqueueDemoteChatFileFromProject(string projectId, string chatId, string filePrefix);
        void EnqueueRefreshProject(string projectId, bool force = false);
        Dictionary<string, object> GetStatus();
    }
}
