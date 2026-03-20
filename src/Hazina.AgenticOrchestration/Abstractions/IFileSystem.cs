namespace Hazina.AgenticOrchestration.Abstractions;

/// <summary>
/// Abstraction for file system operations to enable testability and portability.
/// Inject this instead of calling System.IO.File / System.IO.Directory directly.
/// </summary>
public interface IFileSystem
{
    /// <summary>Checks if a file exists at the specified path.</summary>
    bool FileExists(string path);

    /// <summary>Checks if a directory exists at the specified path.</summary>
    bool DirectoryExists(string path);

    /// <summary>Reads all text from a file.</summary>
    string ReadAllText(string path);

    /// <summary>Reads all text from a file asynchronously.</summary>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Reads all lines from a file asynchronously.</summary>
    Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Writes all text to a file, creating it if it doesn't exist.</summary>
    void WriteAllText(string path, string contents);

    /// <summary>Writes all text to a file asynchronously.</summary>
    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);

    /// <summary>Appends all text to a file asynchronously.</summary>
    Task AppendAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file.</summary>
    void DeleteFile(string path);

    /// <summary>Moves a file from source to destination.</summary>
    void MoveFile(string source, string dest, bool overwrite = false);

    /// <summary>Creates a directory (and all parent directories) if it doesn't exist.</summary>
    void CreateDirectory(string path);

    /// <summary>Gets files in a directory matching the specified pattern.</summary>
    string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

    /// <summary>Gets the current working directory.</summary>
    string GetCurrentDirectory();
}

/// <summary>
/// Default implementation of IFileSystem using System.IO.
/// </summary>
public class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(path, cancellationToken);
    public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllLinesAsync(path, cancellationToken);
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
    public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default) =>
        File.WriteAllTextAsync(path, contents, cancellationToken);
    public Task AppendAllTextAsync(string path, string contents, CancellationToken cancellationToken = default) =>
        File.AppendAllTextAsync(path, contents, cancellationToken);
    public void DeleteFile(string path) => File.Delete(path);
    public void MoveFile(string source, string dest, bool overwrite = false) => File.Move(source, dest, overwrite);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly) =>
        Directory.GetFiles(path, searchPattern, searchOption);
    public string GetCurrentDirectory() => Directory.GetCurrentDirectory();
}
