namespace Hazina.AgentFactory.Abstractions;

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
    /// Writes all text to a file, creating it if it doesn't exist.
    /// </summary>
    void WriteAllText(string path, string contents);

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

    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

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
