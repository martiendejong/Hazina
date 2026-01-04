using System;
using System.Threading;
using System.Threading.Tasks;
using Hazina.Observability.LLMLogs.Configuration;
using Hazina.Observability.LLMLogs.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hazina.Observability.LLMLogs.Extensions
{
    /// <summary>
    /// Background service that initializes the LLM logging database on startup
    /// and periodically cleans up old logs.
    /// </summary>
    internal class LLMLoggingInitializationService : BackgroundService
    {
        private readonly ILLMLogRepository _repository;
        private readonly LLMLoggingOptions _options;

        public LLMLoggingInitializationService(
            ILLMLogRepository repository,
            IOptions<LLMLoggingOptions> options)
        {
            _repository = repository;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
                return;

            try
            {
                // Initialize database schema
                await _repository.InitializeAsync(stoppingToken);
                Console.WriteLine("[LLMLogging] Database initialized successfully");

                // Run cleanup periodically (once per day)
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromDays(1), stoppingToken);

                    if (_options.RetentionDays > 0)
                    {
                        try
                        {
                            await _repository.CleanupOldLogsAsync(_options.RetentionDays, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[LLMLogging] Failed to cleanup old logs: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"[LLMLogging] Initialization failed: {ex.Message}");
            }
        }
    }
}
