using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DevGPT.GenerationTools.Services.FileOps.Helpers
{
    /// <summary>
    /// Thread-safe lock manager for document operations
    /// Fixes memory leak issue by implementing cleanup
    /// </summary>
    public class DocumentLockManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
        private readonly ConcurrentDictionary<string, DateTime> _lastAccessTime;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _lockTimeout;
        private readonly TimeSpan _inactivityTimeout;
        private bool _disposed;

        public DocumentLockManager(
            TimeSpan? lockTimeout = null,
            TimeSpan? inactivityTimeout = null,
            TimeSpan? cleanupInterval = null)
        {
            _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
            _lastAccessTime = new ConcurrentDictionary<string, DateTime>();
            _lockTimeout = lockTimeout ?? TimeSpan.FromMinutes(5);
            _inactivityTimeout = inactivityTimeout ?? TimeSpan.FromHours(1);

            // Cleanup timer to remove unused locks
            var interval = cleanupInterval ?? TimeSpan.FromMinutes(10);
            _cleanupTimer = new Timer(
                CleanupInactiveLocks,
                null,
                interval,
                interval
            );
        }

        /// <summary>
        /// Acquire a lock for a project with timeout
        /// Automatically creates lock if it doesn't exist
        /// </summary>
        public async Task<IDisposable> AcquireLockAsync(
            string projectId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("Project ID cannot be empty", nameof(projectId));

            if (_disposed)
                throw new ObjectDisposedException(nameof(DocumentLockManager));

            // Get or create lock for project
            var semaphore = _locks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));

            // Update last access time
            _lastAccessTime[projectId] = DateTime.UtcNow;

            // Acquire with timeout
            var acquired = await semaphore.WaitAsync(_lockTimeout, cancellationToken);

            if (!acquired)
            {
                throw new TimeoutException(
                    $"Failed to acquire lock for project '{projectId}' within {_lockTimeout.TotalSeconds} seconds");
            }

            // Return disposable that releases the lock
            return new LockReleaser(semaphore, () =>
            {
                _lastAccessTime[projectId] = DateTime.UtcNow;
            });
        }

        /// <summary>
        /// Cleanup locks that haven't been used recently
        /// Prevents memory leak from accumulating locks
        /// </summary>
        private void CleanupInactiveLocks(object state)
        {
            if (_disposed)
                return;

            var now = DateTime.UtcNow;
            var locksToRemove = new System.Collections.Generic.List<string>();

            foreach (var kvp in _lastAccessTime)
            {
                var projectId = kvp.Key;
                var lastAccess = kvp.Value;

                // If lock hasn't been accessed recently and is not currently held
                if (now - lastAccess > _inactivityTimeout)
                {
                    if (_locks.TryGetValue(projectId, out var semaphore))
                    {
                        // Check if lock is available (not held)
                        if (semaphore.CurrentCount > 0)
                        {
                            locksToRemove.Add(projectId);
                        }
                    }
                }
            }

            // Remove inactive locks
            foreach (var projectId in locksToRemove)
            {
                if (_locks.TryRemove(projectId, out var semaphore))
                {
                    semaphore.Dispose();
                    _lastAccessTime.TryRemove(projectId, out _);
                    Console.WriteLine($"Cleaned up lock for inactive project: {projectId}");
                }
            }
        }

        /// <summary>
        /// Manually remove a lock for a project (e.g., when project is deleted)
        /// </summary>
        public void RemoveLock(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                return;

            if (_locks.TryRemove(projectId, out var semaphore))
            {
                semaphore.Dispose();
                _lastAccessTime.TryRemove(projectId, out _);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cleanupTimer?.Dispose();

            // Dispose all semaphores
            foreach (var semaphore in _locks.Values)
            {
                semaphore?.Dispose();
            }

            _locks.Clear();
            _lastAccessTime.Clear();
        }

        /// <summary>
        /// Helper class to release lock when disposed
        /// </summary>
        private class LockReleaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private readonly Action _onRelease;
            private bool _released;

            public LockReleaser(SemaphoreSlim semaphore, Action onRelease)
            {
                _semaphore = semaphore;
                _onRelease = onRelease;
            }

            public void Dispose()
            {
                if (_released)
                    return;

                _released = true;
                _onRelease?.Invoke();
                _semaphore.Release();
            }
        }
    }
}
