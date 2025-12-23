using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevGPT.GenerationTools.Services.FileOps.Helpers
{
    public class Retry
    {
        private readonly Action<string> _log;
        public int MaxAttempts { get; }
        public int InitialDelayMs { get; }
        public double Backoff { get; }

        public Retry(Action<string> log, int maxAttempts = 5, int initialDelayMs = 200, double backoff = 2.0)
        {
            _log = log ?? (_ => { });
            MaxAttempts = Math.Max(1, maxAttempts);
            InitialDelayMs = Math.Max(0, initialDelayMs);
            Backoff = Math.Max(1.0, backoff);
        }

        public async Task<T> try5<T>(Func<Task<T>> action)
        {
            var delay = InitialDelayMs;
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex)
                {
                    _log($"Attempt {attempt} failed: {ex.Message}");
                    if (attempt == MaxAttempts) throw;
                    await Task.Delay(delay);
                    delay = (int)(delay * Backoff);
                }
            }
            throw new Exception("Retry failed");
        }

        public async Task try5(Func<Task> action)
        {
            await try5(async () => { await action(); return true; });
        }
    }
}


