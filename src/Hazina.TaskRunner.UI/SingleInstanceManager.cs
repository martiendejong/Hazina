using System;
using System.Threading;

namespace Hazina.TaskRunner.UI;

/// <summary>
/// Ensures only one instance of the application runs at a time
/// </summary>
public class SingleInstanceManager : IDisposable
{
    private readonly Mutex _mutex;
    private bool _disposed;
    private readonly bool _hasHandle;

    public SingleInstanceManager(string uniqueName)
    {
        _mutex = new Mutex(true, uniqueName, out _hasHandle);
    }

    /// <summary>
    /// Checks if this is the first instance
    /// </summary>
    public bool IsFirstInstance => _hasHandle;

    public void Dispose()
    {
        if (_disposed) return;

        if (_hasHandle)
        {
            _mutex.ReleaseMutex();
        }
        _mutex?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
