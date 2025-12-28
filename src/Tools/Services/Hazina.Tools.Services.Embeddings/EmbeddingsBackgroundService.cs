using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hazina.Tools.Services.Embeddings
{
    public class EmbeddingsBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly EmbeddingsWorkQueue _workQueue;
        private readonly ILogger<EmbeddingsBackgroundService> _logger;

        public EmbeddingsBackgroundService(
            IServiceProvider serviceProvider,
            EmbeddingsWorkQueue workQueue,
            ILogger<EmbeddingsBackgroundService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _workQueue = workQueue ?? throw new ArgumentNullException(nameof(workQueue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Embeddings Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for work item with cancellation support
                    if (_workQueue.WorkItems.TryTake(out var workItem, 1000, stoppingToken))
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var embeddingsService = scope.ServiceProvider.GetRequiredService<IEmbeddingsService>();

                        try
                        {
                            await workItem(embeddingsService);
                            Interlocked.Increment(ref _workQueue.ProcessedCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref _workQueue.ErrorCount);
                            _logger.LogError(ex, "Error processing embeddings work item");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in embeddings background service loop");
                    await Task.Delay(1000, stoppingToken);
                }
            }

            _logger.LogInformation("Embeddings Background Service stopped");
        }
    }
}
