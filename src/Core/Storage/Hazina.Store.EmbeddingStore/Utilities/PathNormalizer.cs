namespace Hazina.Store.EmbeddingStore.Utilities;

/// <summary>
/// Provides cross-platform path normalization utilities for Hazina storage implementations.
/// Ensures consistent path handling across Windows, macOS, and Linux.
/// </summary>
public static class PathNormalizer
{
    /// <summary>
    /// Normalizes a path to use the current platform's directory separator.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>A normalized path with platform-specific separators.</returns>
    /// <remarks>
    /// This method replaces all forward slashes (/) and backslashes (\) with
    /// the platform's native directory separator (Path.DirectorySeparatorChar).
    /// </remarks>
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        // Replace both types of separators with the platform-specific one
        return path
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Combines path segments and normalizes the result for the current platform.
    /// </summary>
    /// <param name="paths">The path segments to combine.</param>
    /// <returns>A normalized combined path.</returns>
    public static string Combine(params string[] paths)
    {
        if (paths == null || paths.Length == 0)
            return string.Empty;

        // Normalize each segment first to handle mixed separators
        var normalizedPaths = paths.Select(Normalize).ToArray();

        // Use Path.Combine which is already platform-aware
        return Path.Combine(normalizedPaths);
    }

    /// <summary>
    /// Ensures a path is absolute and normalized.
    /// </summary>
    /// <param name="path">The path to make absolute.</param>
    /// <param name="basePath">Optional base path for relative paths. Defaults to current directory.</param>
    /// <returns>An absolute, normalized path.</returns>
    public static string MakeAbsolute(string path, string? basePath = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        path = Normalize(path);

        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        var baseDir = basePath ?? Environment.CurrentDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, path));
    }

    /// <summary>
    /// Converts a path to use forward slashes (/) for storage/serialization.
    /// This provides a platform-independent representation suitable for storing in databases or config files.
    /// </summary>
    /// <param name="path">The path to convert.</param>
    /// <returns>A path using forward slashes as separators.</returns>
    public static string ToStorageFormat(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        return path.Replace('\\', '/');
    }

    /// <summary>
    /// Converts a storage format path (with forward slashes) back to the platform's native format.
    /// </summary>
    /// <param name="path">The storage format path to convert.</param>
    /// <returns>A path using the platform's native directory separator.</returns>
    public static string FromStorageFormat(string path)
    {
        return Normalize(path);
    }
}
