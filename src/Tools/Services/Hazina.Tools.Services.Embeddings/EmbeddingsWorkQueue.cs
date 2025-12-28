using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Hazina.Tools.Services.Embeddings
{
    public class EmbeddingsWorkQueue : IEmbeddingsWorkQueue
    {
        private readonly ILogger<EmbeddingsWorkQueue> _logger;
        internal readonly BlockingCollection<Func<IEmbeddingsService, Task>> WorkItems = new(new ConcurrentQueue<Func<IEmbeddingsService, Task>>());
        internal int ProcessedCount = 0;
        internal int ErrorCount = 0;

        public EmbeddingsWorkQueue(ILogger<EmbeddingsWorkQueue> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void EnqueueEmbedProjectFile(string projectId, string fileName)
        {
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(fileName))
                return;

            WorkItems.Add(async (embeddingsService) =>
            {
                await embeddingsService.EmbedProjectFile(projectId, fileName);
                _logger.LogInformation("Embedded project file: {ProjectId}/{FileName}", projectId, fileName);
            });
        }

        public void EnqueueExtractAndEmbedChatUpload(string projectId, string chatId, string fileName, string userId = null)
        {
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(fileName))
                return;

            WorkItems.Add(async (embeddingsService) =>
            {
                await embeddingsService.EmbedChatUpload(projectId, chatId, fileName, userId);
                _logger.LogInformation("Embedded chat upload: {ProjectId}/{ChatId}/{FileName}", projectId, chatId, fileName);
            });
        }

        public void EnqueuePromoteChatFileToProject(string projectId, string chatId, string filePrefix, string userId = null)
        {
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(filePrefix))
                return;

            WorkItems.Add(async (embeddingsService) =>
            {
                await embeddingsService.PromoteChatFileToProject(projectId, chatId, filePrefix, userId);
                _logger.LogInformation("Promoted chat file to project: {ProjectId}/{ChatId}/{FilePrefix}", projectId, chatId, filePrefix);
            });
        }

        public void EnqueueDemoteChatFileFromProject(string projectId, string chatId, string filePrefix)
        {
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(filePrefix))
                return;

            WorkItems.Add(async (embeddingsService) =>
            {
                await embeddingsService.DemoteChatFileFromProject(projectId, chatId, filePrefix);
                _logger.LogInformation("Demoted chat file from project: {ProjectId}/{ChatId}/{FilePrefix}", projectId, chatId, filePrefix);
            });
        }

        public void EnqueueRefreshProject(string projectId, bool force = false)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                return;

            WorkItems.Add(async (embeddingsService) =>
            {
                await embeddingsService.RefreshProjectEmbeddings(projectId, force);
                _logger.LogInformation("Refreshed project embeddings: {ProjectId} (force={Force})", projectId, force);
            });
        }

        public Dictionary<string, object> GetStatus()
        {
            return new Dictionary<string, object>
            {
                ["queueLength"] = WorkItems.Count,
                ["processedCount"] = ProcessedCount,
                ["errorCount"] = ErrorCount,
                ["isRunning"] = true
            };
        }
    }
}
