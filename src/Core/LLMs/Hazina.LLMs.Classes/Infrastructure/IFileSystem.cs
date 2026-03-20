namespace Hazina.LLMs.Infrastructure;

/// <summary>
/// Abstraction for file system operations to enable testability and portability.
/// Allows mocking file system in tests and supports alternative storage backends.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Checks if a directory exists at the specified path.
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Reads all text from a file.
    /// </summary>
    string ReadAllText(string path);

    /// <summary>
    /// Reads all text from a file asynchronously.
    /// </summary>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all bytes from a file asynchronously.
    /// </summary>
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes all text to a file, creating it if it doesn't exist.
    /// </summary>
    void WriteAllText(string path, string contents);

    /// <summary>
    /// Writes all text to a file asynchronously, creating it if it doesn't exist.
    /// </summary>
    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes all bytes to a file asynchronously, creating it if it doesn't exist.
    /// </summary>
    Task WriteAllBytesAsync(string path, byte[] contents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full path for a given path.
    /// </summary>
    string GetFullPath(string path);

    /// <summary>
    /// Gets files in a directory matching the specified pattern.
    /// </summary>
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);

    /// <summary>
    /// Gets subdirectories in a directory.
    /// </summary>
    string[] GetDirectories(string path);

    /// <summary>
    /// Creates a directory if it doesn't exist.
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Gets file info for a path.
    /// </summary>
    IFileInfo GetFileInfo(string path);

    /// <summary>
    /// Gets directory info for a path.
    /// </summary>
    IDirectoryInfo GetDirectoryInfo(string path);

    /// <summary>
    /// Combines path segments in a platform-independent way.
    /// </summary>
    string PathCombine(params string[] paths);

    /// <summary>
    /// Gets the directory name from a path.
    /// </summary>
    string? GetDirectoryName(string path);

    /// <summary>
    /// Gets the file name from a path.
    /// </summary>
    string? GetFileName(string path);

    /// <summary>
    /// Gets the file extension from a path.
    /// </summary>
    string? GetExtension(string path);

    /// <summary>
    /// Gets the file name without extension from a path.
    /// </summary>
    string? GetFileNameWithoutExtension(string path);

    /// <summary>
    /// Opens a file for writing, creating it if it doesn't exist.
    /// </summary>
    Stream OpenWrite(string path);

    /// <summary>
    /// Opens a file for reading.
    /// </summary>
    Stream OpenRead(string path);

    /// <summary>
    /// Deletes a file.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Deletes a directory.
    /// </summary>
    void DeleteDirectory(string path, bool recursive = false);
}

/// <summary>
/// Abstraction for file information.
/// </summary>
public interface IFileInfo
{
    string Name { get; }
    string FullName { get; }
    long Length { get; }
    DateTime LastWriteTimeUtc { get; }
    DateTime CreationTimeUtc { get; }
    bool Exists { get; }
    string Extension { get; }
}

/// <summary>
/// Abstraction for directory information.
/// </summary>
public interface IDirectoryInfo
{
    string Name { get; }
    string FullName { get; }
    DateTime LastWriteTimeUtc { get; }
    DateTime CreationTimeUtc { get; }
    bool Exists { get; }
}

/// <summary>
/// Default implementation using System.IO.
/// </summary>
public class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        => File.ReadAllTextAsync(path, cancellationToken);

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
        => File.ReadAllBytesAsync(path, cancellationToken);

    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
        => File.WriteAllTextAsync(path, contents, cancellationToken);

    public Task WriteAllBytesAsync(string path, byte[] contents, CancellationToken cancellationToken = default)
        => File.WriteAllBytesAsync(path, contents, cancellationToken);

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        => Directory.GetFiles(path, searchPattern, searchOption);

    public string[] GetDirectories(string path) => Directory.GetDirectories(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public IFileInfo GetFileInfo(string path) => new PhysicalFileInfo(new FileInfo(path));

    public IDirectoryInfo GetDirectoryInfo(string path) => new PhysicalDirectoryInfo(new DirectoryInfo(path));

    public string PathCombine(params string[] paths) => Path.Combine(paths);

    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);

    public string? GetFileName(string path) => Path.GetFileName(path);

    public string? GetExtension(string path) => Path.GetExtension(path);

    public string? GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);

    public Stream OpenWrite(string path) => File.OpenWrite(path);

    public Stream OpenRead(string path) => File.OpenRead(path);

    public void DeleteFile(string path) => File.Delete(path);

    public void DeleteDirectory(string path, bool recursive = false) => Directory.Delete(path, recursive);
}

/// <summary>
/// Physical file info implementation.
/// </summary>
public class PhysicalFileInfo : IFileInfo
{
    private readonly FileInfo _fileInfo;

    public PhysicalFileInfo(FileInfo fileInfo)
    {
        _fileInfo = fileInfo;
    }

    public string Name => _fileInfo.Name;
    public string FullName => _fileInfo.FullName;
    public long Length => _fileInfo.Length;
    public DateTime LastWriteTimeUtc => _fileInfo.LastWriteTimeUtc;
    public DateTime CreationTimeUtc => _fileInfo.CreationTimeUtc;
    public bool Exists => _fileInfo.Exists;
    public string Extension => _fileInfo.Extension;
}

/// <summary>
/// Physical directory info implementation.
/// </summary>
public class PhysicalDirectoryInfo : IDirectoryInfo
{
    private readonly DirectoryInfo _dirInfo;

    public PhysicalDirectoryInfo(DirectoryInfo dirInfo)
    {
        _dirInfo = dirInfo;
    }

    public string Name => _dirInfo.Name;
    public string FullName => _dirInfo.FullName;
    public DateTime LastWriteTimeUtc => _dirInfo.LastWriteTimeUtc;
    public DateTime CreationTimeUtc => _dirInfo.CreationTimeUtc;
    public bool Exists => _dirInfo.Exists;
}
