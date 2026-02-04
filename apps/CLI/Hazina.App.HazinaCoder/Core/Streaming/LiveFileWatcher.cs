using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Hazina.App.HazinaCoder.Core.Streaming;

/// <summary>
/// Watch filesystem for changes and emit real-time notifications.
/// Shows file edits as they happen.
///
/// Improvement #90: Live File Change Notifications (Value: 7/10, Effort: 5/10, Ratio: 1.40)
/// </summary>
public class LiveFileWatcher : IDisposable
{
    private readonly Dictionary<string, FileSystemWatcher> _watchers;
    private readonly StreamingOrchestrator _streamer;

    public event EventHandler<FileChangeEventArgs>? FileChanged;

    public LiveFileWatcher(StreamingOrchestrator streamer)
    {
        _watchers = new Dictionary<string, FileSystemWatcher>();
        _streamer = streamer;
    }

    /// <summary>
    /// Start watching directory for file changes
    /// </summary>
    public void WatchDirectory(string directoryPath, string filter = "*.*", bool includeSubdirectories = true)
    {
        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine($"⚠️ Directory not found: {directoryPath}");
            return;
        }

        if (_watchers.ContainsKey(directoryPath))
        {
            Console.WriteLine($"⚠️ Already watching: {directoryPath}");
            return;
        }

        var watcher = new FileSystemWatcher(directoryPath)
        {
            Filter = filter,
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter = NotifyFilters.FileName |
                           NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite |
                           NotifyFilters.Size
        };

        // Register event handlers
        watcher.Changed += OnFileChanged;
        watcher.Created += OnFileCreated;
        watcher.Deleted += OnFileDeleted;
        watcher.Renamed += OnFileRenamed;

        watcher.EnableRaisingEvents = true;
        _watchers[directoryPath] = watcher;

        Console.WriteLine($"👁️  Watching: {directoryPath}");
    }

    /// <summary>
    /// Stop watching directory
    /// </summary>
    public void StopWatching(string directoryPath)
    {
        if (!_watchers.TryGetValue(directoryPath, out var watcher))
            return;

        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
        _watchers.Remove(directoryPath);

        Console.WriteLine($"👁️  Stopped watching: {directoryPath}");
    }

    /// <summary>
    /// Stop watching all directories
    /// </summary>
    public void StopAll()
    {
        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
        Console.WriteLine("👁️  Stopped all file watchers");
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        HandleFileEvent(e.FullPath, FileChangeType.Modified);
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        HandleFileEvent(e.FullPath, FileChangeType.Created);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        HandleFileEvent(e.FullPath, FileChangeType.Deleted);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        HandleFileEvent(e.FullPath, FileChangeType.Renamed, e.OldFullPath);
    }

    private void HandleFileEvent(string filePath, FileChangeType changeType, string? oldPath = null)
    {
        // Ignore temporary files
        if (filePath.EndsWith(".tmp") || filePath.EndsWith(".swp") || filePath.Contains("~"))
            return;

        // Get file info
        long fileSize = 0;
        if (File.Exists(filePath))
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                fileSize = fileInfo.Length;
            }
            catch
            {
                // Ignore errors (file might be locked)
            }
        }

        // Emit streaming event
        Task.Run(async () =>
        {
            await _streamer.EmitFileChangeAsync(filePath, changeType);
        });

        // Fire local event
        FileChanged?.Invoke(this, new FileChangeEventArgs
        {
            FilePath = filePath,
            OldFilePath = oldPath,
            ChangeType = changeType,
            FileSizeBytes = fileSize,
            Timestamp = DateTime.UtcNow
        });

        // Console output
        var changeSymbol = changeType switch
        {
            FileChangeType.Created => "➕",
            FileChangeType.Modified => "✏️",
            FileChangeType.Deleted => "❌",
            FileChangeType.Renamed => "📝",
            _ => "📄"
        };

        var message = changeType == FileChangeType.Renamed && oldPath != null
            ? $"{changeSymbol} {Path.GetFileName(oldPath)} → {Path.GetFileName(filePath)}"
            : $"{changeSymbol} {Path.GetFileName(filePath)}";

        Console.WriteLine($"   {message}");
    }

    public void Dispose()
    {
        StopAll();
    }
}

/// <summary>
/// File change event args
/// </summary>
public class FileChangeEventArgs : EventArgs
{
    public string FilePath { get; set; } = string.Empty;
    public string? OldFilePath { get; set; }
    public FileChangeType ChangeType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime Timestamp { get; set; }
}
