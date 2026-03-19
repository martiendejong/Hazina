using System.Collections.Generic;
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

        /// <summary>
        /// Generate embedding for a single text string
        /// </summary>
        Task<List<double>> GenerateEmbeddingAsync(string text);

        /// <summary>
        /// Compacts project embeddings by removing orphaned entries
        /// </summary>
        /// <returns>Number of orphaned embeddings removed</returns>
        Task<int> CompactProjectEmbeddingsAsync(string projectId);

        /// <summary>
        /// Verifies integrity of project embeddings
        /// </summary>
        /// <returns>List of integrity issues found</returns>
        Task<List<string>> VerifyProjectEmbeddingsIntegrityAsync(string projectId);

        /// <summary>
        /// Repairs integrity issues in project embeddings
        /// </summary>
        /// <returns>Number of entries repaired</returns>
        Task<int> RepairProjectEmbeddingsAsync(string projectId);

        /// <summary>
        /// Batch index multiple files for a project
        /// </summary>
        /// <param name="projectId">Project ID</param>
        /// <param name="files">List of relative file paths to index</param>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of files successfully indexed</returns>
        Task<int> BatchIndexFilesAsync(
            string projectId,
            IEnumerable<string> files,
            Action<int, int>? progressCallback = null,
            CancellationToken cancellationToken = default);
    }
}

